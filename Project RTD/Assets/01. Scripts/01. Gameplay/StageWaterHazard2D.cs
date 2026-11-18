using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class StageWaterHazard2D : MonoBehaviour
    {
        private PlayerPartLoadout loadout;
        private BoxCollider2D gate;
        private Collider2D playerCollider;

        private void Start()
        {
            gate = GetComponent<BoxCollider2D>();
            var stage = GetComponentInParent<StageRuntime>();
            if (!stage || !stage.Player || !stage.Player.TryGetComponent(out loadout)) return;
            playerCollider = loadout.GetComponent<Collider2D>();
            // This is the crossing guard, not the separate Water-layer buoyancy trigger.
            // Extend it above jump height so it can't serve as a bridge for non-swimmers.
            gate.offset += Vector2.up * 5f;
            gate.size += Vector2.up * 10f;
            gate.isTrigger = false;
            loadout.Changed += Refresh;
            Refresh(loadout.Current);
        }

        private void Refresh(PlayerPartBuild build)
        {
            bool block = build?.HasEffect(PartEffect.Float) != true;
            if (block && !gate.enabled && playerCollider)
            {
                // Losing swim mid-crossing restores the player to the nearest bank before closing.
                gate.enabled = true;
                var shore = gate.bounds;
                var p = playerCollider.bounds;
                if (shore.Intersects(p))
                {
                    float dx = p.center.x < shore.center.x ? shore.min.x - p.max.x - .15f : shore.max.x - p.min.x + .15f;
                    var rb = playerCollider.attachedRigidbody;
                    rb.position += new Vector2(dx, Mathf.Max(0, transform.position.y - p.min.y));
                    rb.linearVelocity = Vector2.zero;
                }
            }
            gate.enabled = block;
        }

        private void OnDestroy()
        {
            if (loadout) loadout.Changed -= Refresh;
        }
    }
}
