using System;
using ProjectRTD.Core;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PlatformEnemy : MonoBehaviour
    {
        public enum Kind { Drone, Scientist }
        [SerializeField] private Kind kind;
        [SerializeField, Tooltip("Drone only: heads returned to spawn. Scientists check canPass instead.")]
        private AnimalPartDefinition[] matchingHeads = Array.Empty<AnimalPartDefinition>();
        [SerializeField] private bool islandGuard;
        [SerializeField] private TMPro.TMP_Text targetLabel;
        private PartAnimal targetAnimal;
        internal PartAnimal TargetAnimal => targetAnimal;
        [SerializeField, Min(0)] private float speed = 1.1f;
        [SerializeField] private LayerMask platforms = Physics2D.DefaultRaycastLayers;
        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private CardTurn turn;
        private Collider2D platform;
        private int direction = -1;

        [Header("Animated art (optional child)")]
        [SerializeField, Tooltip("Child with ScientistAnimator or DroneAnimator. When set, the flat sprite is hidden at runtime.")]
        private Transform animatedArt;
        [SerializeField, Min(0)] private float noticeRange = 2.6f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 2.2f;
        private ChimeraGroove.ScientistAnimator scientist;
        private ChimeraGroove.DroneAnimator drone;
        private float nextAttack;

        private void Awake()
        {
            if (!islandGuard || kind != Kind.Drone) return;
            var animals = (PartAnimal[])Enum.GetValues(typeof(PartAnimal));
            targetAnimal = animals[UnityEngine.Random.Range(0, animals.Length)];
            if (targetLabel) targetLabel.text = $"TARGET: {targetAnimal.ToString().ToUpperInvariant()}";
        }

        StageRuntime patrolStage;
        Vector2 patrolStart;
        bool PatrolPaused => patrolStage && (patrolStage.IsRespawning || patrolStage.InDoorway);

        void ResetPatrol()
        {
            body.position = patrolStart;
            transform.position = new Vector3(patrolStart.x, patrolStart.y, transform.position.z);
            direction = -1;
            nextAttack = 0f;
        }

        void OnDestroy()
        {
            if (patrolStage) patrolStage.ResetMechanics -= ResetPatrol;
        }

        private void Start()
        {
            if (islandGuard) return;
            body = GetComponent<Rigidbody2D>();
            if (kind == Kind.Scientist)
            {
                patrolStage = GetComponentInParent<StageRuntime>();
                patrolStart = body.position;
                if (patrolStage) patrolStage.ResetMechanics += ResetPatrol;
            }
            hitbox = GetComponent<BoxCollider2D>();
            var visual = transform.Find("Visual");
            if (animatedArt)
            {
                scientist = animatedArt.GetComponent<ChimeraGroove.ScientistAnimator>();
                drone = animatedArt.GetComponent<ChimeraGroove.DroneAnimator>();
            }
            bool animated = scientist || drone;
            if (visual && visual.TryGetComponent<SpriteRenderer>(out var flat)) flat.enabled = !animated;
            var facingTarget = animated ? animatedArt : visual;
            if (facingTarget) turn = new CardTurn(facingTarget);
            var filter = new ContactFilter2D(); filter.SetLayerMask(platforms); filter.useTriggers = false;
            var hits = new System.Collections.Generic.List<RaycastHit2D>();
            Physics2D.Raycast(body.position + Vector2.up * .15f, Vector2.down, filter, hits, 1.5f);
            foreach (var hit in hits)
                if (!hit.rigidbody && hit.normal.y > .8f) { platform = hit.collider; break; }
        }

        private void FixedUpdate()
        {
            if (!platform || PatrolPaused) return;
            var bounds = platform.bounds;
            float margin = Mathf.Min(bounds.extents.x, hitbox.bounds.extents.x);
            float left = bounds.min.x + margin, right = bounds.max.x - margin;
            float x = Mathf.Clamp(body.position.x + direction * speed * Time.fixedDeltaTime,left,right);
            if (x <= left) direction = 1; else if (x >= right) direction = -1;
            if (scientist && scientist.IsAttacking) x = body.position.x;
            body.MovePosition(new Vector2(x,bounds.max.y));
        }

        private void LateUpdate() => turn?.Tick(direction, Time.deltaTime);

        private void OnTriggerEnter2D(Collider2D other)
        {
            StageRuntime stage = StageRuntime.Instance;
            if (!stage || stage.IsRespawning || !other.attachedRigidbody ||
                !other.attachedRigidbody.TryGetComponent(out PlayerPartLoadout player) ||
                stage.Player != player.transform || player.Current == null)
                return;

            var head = player.Current.Head;
            bool attacks = kind == Kind.Drone
                ? (islandGuard ? head.Animal == targetAnimal : Array.IndexOf(matchingHeads, head) >= 0)
                : (islandGuard ? head.Animal is not (PartAnimal.Bear or PartAnimal.Lion) : !player.Current.HasEffect(PartEffect.CanPass));
            if (!attacks) return;

            if (islandGuard)
            {
                SoundManager.Play(Sfx.Scare);
            }
            else
            {
                player.GetComponentInChildren<PlayerPartVisual>()?.PlayScare();
            }
            stage.ReturnToStart();
        }

        private void Update()
        {
            if (PatrolPaused || (!scientist && !drone)) return;
            var player = StageRuntime.Instance ? StageRuntime.Instance.Player : null;
            bool near = player && Mathf.Abs(player.position.x - transform.position.x) < noticeRange
                        && Mathf.Abs(player.position.y - transform.position.y) < 2f;
            if (drone) drone.state = near ? ChimeraGroove.DroneState.Alert : ChimeraGroove.DroneState.Scan;
            if (!scientist) return;
            scientist.state = ChimeraGroove.ScientistState.Walk;
            if (!near || Time.time < nextAttack) return;
            direction = player.position.x > transform.position.x ? 1 : -1; // turn to face the player
            scientist.Attack();
            nextAttack = Time.time + attackCooldown;
        }

    }
}
