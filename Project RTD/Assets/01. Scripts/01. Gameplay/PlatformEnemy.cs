using System;
using ProjectRTD.Core;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class PlatformEnemy : MonoBehaviour
    {
        public enum Kind { Drone, Scientist }
        [SerializeField] private Kind kind;
        [SerializeField, Tooltip("Drone only: heads returned to spawn. Scientists check canPass instead.")]
        private AnimalPartDefinition[] matchingHeads = Array.Empty<AnimalPartDefinition>();
        [SerializeField, Min(0)] private float speed = 1.1f;
        [SerializeField] private LayerMask platforms = Physics2D.DefaultRaycastLayers;
        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private SpriteRenderer art;
        private Collider2D platform;
        private int direction = -1;

        [Header("Animated art (optional child)")]
        [SerializeField, Tooltip("Child with ScientistAnimator or DroneAnimator. When set, the flat sprite is hidden at runtime.")]
        private Transform animatedArt;
        [SerializeField, Min(0)] private float noticeRange = 2.6f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 2.2f;
        private ChimeraGroove.ScientistAnimator scientist;
        private ChimeraGroove.DroneAnimator drone;
        private float artScaleX, nextAttack;

        private void Start()
        {
            body = GetComponent<Rigidbody2D>(); hitbox = GetComponent<BoxCollider2D>(); art = GetComponent<SpriteRenderer>();
            if (animatedArt)
            {
                scientist = animatedArt.GetComponent<ChimeraGroove.ScientistAnimator>();
                drone = animatedArt.GetComponent<ChimeraGroove.DroneAnimator>();
                artScaleX = Mathf.Abs(animatedArt.localScale.x);
                art.enabled = !scientist && !drone;
            }
            var filter = new ContactFilter2D(); filter.SetLayerMask(platforms); filter.useTriggers = false;
            var hits = new System.Collections.Generic.List<RaycastHit2D>();
            Physics2D.Raycast(body.position + Vector2.up * .15f, Vector2.down, filter, hits, 1.5f);
            foreach (var hit in hits)
                if (!hit.rigidbody && hit.normal.y > .8f) { platform = hit.collider; break; }
        }

        private void FixedUpdate()
        {
            if (!platform) return;
            var bounds = platform.bounds;
            float margin = Mathf.Min(bounds.extents.x, hitbox.bounds.extents.x);
            float left = bounds.min.x + margin, right = bounds.max.x - margin;
            float x = Mathf.Clamp(body.position.x + direction * speed * Time.fixedDeltaTime,left,right);
            if (x <= left) direction = 1; else if (x >= right) direction = -1;
            bool attacking = scientist && scientist.IsAttacking;
            if (attacking) x = body.position.x; // plant feet while swinging or firing
            art.flipX = direction > 0;
            if (animatedArt)
            {
                var scale = animatedArt.localScale;
                scale.x = direction > 0 ? -artScaleX : artScaleX; // art faces left by default
                animatedArt.localScale = scale;
            }
            body.MovePosition(new Vector2(x,bounds.max.y));
        }

        private void Update()
        {
            if (!scientist && !drone) return;
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

        private void OnTriggerEnter2D(Collider2D other) => CheckHead(other);
        private void OnTriggerStay2D(Collider2D other) => CheckHead(other);
        private void CheckHead(Collider2D other)
        {
            if (!other.attachedRigidbody || !other.attachedRigidbody.TryGetComponent<PlayerPartLoadout>(out var player)) return;
            var head = player.Current?.Head;
            if (!head) return;
            if (kind == Kind.Scientist)
            {
                if (player.Current.HasEffect(PartEffect.CanPass)) return;

            }
            else if (Array.IndexOf(matchingHeads, head) < 0) return;
            if (StageRuntime.Instance && StageRuntime.Instance.Player == player.transform) StageRuntime.Instance.RespawnPlayer();
        }
    }
}
