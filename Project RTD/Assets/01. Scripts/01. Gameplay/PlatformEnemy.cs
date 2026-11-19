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
        [SerializeField, Tooltip("Drone only: heads returned to spawn. Scientists pick a random target head instead.")]
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
        [SerializeField, Min(0), Tooltip("Scientists open fire on their target from this far away.")] private float shootRange = 6f;
        [SerializeField, Min(0), Tooltip("Gun raise before the shot leaves the barrel.")] private float shotWindup = 0.35f;
        [SerializeField, Tooltip("Muzzle offset from the feet when facing right.")] private Vector2 muzzle = new(0.62f, 0.95f);
        private ChimeraGroove.ScientistAnimator scientist;
        private ChimeraGroove.DroneAnimator drone;
        private float nextAttack;

        [Header("Scientist target bubble")]
        [SerializeField, Tooltip("Tail tip of the thought bubble, relative to the scientist's feet.")]
        private Vector3 bubbleOffset = new Vector3(0f, 1.85f, 0f);
        [SerializeField, Min(.01f)] private float bubbleScale = .22f;
        private AnimalPartDefinition targetHead;
        private ChimeraGroove.Bubble.ThoughtBubble bubble;
        internal AnimalPartDefinition TargetHead => targetHead;

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
                PickTargetHead();
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

            if (!Attacks(player.Current)) return;

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
            float range = scientist ? Mathf.Max(noticeRange, shootRange) : noticeRange;
            bool near = player && Mathf.Abs(player.position.x - transform.position.x) < range
                        && Mathf.Abs(player.position.y - transform.position.y) < 2f;
            if (drone) drone.state = near ? ChimeraGroove.DroneState.Alert : ChimeraGroove.DroneState.Scan;
            if (!scientist) return;
            scientist.state = ChimeraGroove.ScientistState.Walk;
            if (!near || Time.time < nextAttack || !Attacks(PlayerBuild(player))) return;
            direction = player.position.x > transform.position.x ? 1 : -1; // turn to face the player
            scientist.Attack();
            nextAttack = Time.time + attackCooldown;
            StartCoroutine(Shoot(direction));
        }

        System.Collections.IEnumerator Shoot(int facing)
        {
            yield return new WaitForSeconds(shotWindup);
            if (PatrolPaused) yield break;
            ScientistBullet.Fire(transform.position + new Vector3(muzzle.x * facing, muzzle.y, 0f), facing);
        }

        /// <summary>Whether this enemy goes after a player wearing <paramref name="build"/>.</summary>
        internal bool Attacks(PlayerPartBuild build)
        {
            if (build == null) return false;
            var head = build.Head;
            if (kind == Kind.Drone)
                return islandGuard ? head.Animal == targetAnimal : Array.IndexOf(matchingHeads, head) >= 0;
            if (islandGuard) return head.Animal is not (PartAnimal.Bear or PartAnimal.Lion);
            // Scientists only chase the head shown in their thought bubble; everyone else walks past.
            return targetHead && head.Animal == targetHead.Animal;
        }

        static PlayerPartBuild PlayerBuild(Transform player) =>
            player && player.TryGetComponent(out PlayerPartLoadout loadout) ? loadout.Current : null;

        void PickTargetHead()
        {
            var player = StageRuntime.Instance ? StageRuntime.Instance.Player : null;
            var catalog = player && player.TryGetComponent(out PlayerPartLoadout loadout) ? loadout.Catalog : null;
            var heads = catalog ? System.Linq.Enumerable.ToArray(catalog.ForSlot(PartSlot.Head)) : Array.Empty<AnimalPartDefinition>();
            if (heads.Length == 0) { Debug.LogWarning("과학자 목표 머리를 고를 파츠 카탈로그가 없습니다.", this); return; }
            targetHead = heads[UnityEngine.Random.Range(0, heads.Length)];
            if (!targetHead.Sprite) return;

            var holder = new GameObject("Target Bubble");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = bubbleOffset;
            bubble = holder.AddComponent<ChimeraGroove.Bubble.ThoughtBubble>();
            bubble.showOnStart = false;
            bubble.scale = bubbleScale;
            bubble.sortingOrder = 30;
            bubble.Show(targetHead.Sprite);
        }
    }
}
