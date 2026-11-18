using System;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class PlatformEnemy : MonoBehaviour
    {
        public enum Kind { Drone, Scientist }
        [SerializeField] private Kind kind;
        [SerializeField, Tooltip("Drone: heads returned to spawn. Scientist: heads that defeat this enemy.")]
        private AnimalPartDefinition[] matchingHeads = Array.Empty<AnimalPartDefinition>();
        [SerializeField, Min(0)] private float speed = 1.1f;
        [SerializeField] private LayerMask platforms = Physics2D.DefaultRaycastLayers;
        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private SpriteRenderer art;
        private Collider2D platform;
        private int direction = -1;

        private void Start()
        {
            body = GetComponent<Rigidbody2D>(); hitbox = GetComponent<BoxCollider2D>(); art = GetComponent<SpriteRenderer>();
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
            art.flipX = direction > 0;
            body.MovePosition(new Vector2(x,bounds.max.y));
        }

        private void OnTriggerEnter2D(Collider2D other) => CheckHead(other);
        private void OnTriggerStay2D(Collider2D other) => CheckHead(other);
        private void CheckHead(Collider2D other)
        {
            if (!other.attachedRigidbody || !other.attachedRigidbody.TryGetComponent<PlayerPartLoadout>(out var player)) return;
            var head = player.Current?.Head;
            if (!head || Array.IndexOf(matchingHeads, head) < 0) return;
            if (kind == Kind.Scientist) gameObject.SetActive(false);
            else if (StageRuntime.Instance && StageRuntime.Instance.Player == player.transform) StageRuntime.Instance.RespawnPlayer();
        }
    }
}
