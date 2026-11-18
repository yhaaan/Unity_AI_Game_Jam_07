using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // One outline around the whole player chimera: every part sprite gets flat-colour copies pushed out in
    // eight directions and drawn just behind the player, so inner seams stay hidden and only the rim shows.
    // Runs after PlayerPartVisual so it sees this frame's rig and facing.
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class PlayerOutline : MonoBehaviour
    {
        [SerializeField] private Material silhouette;
        [SerializeField, Min(0f), Tooltip("Outline width in world units.")] private float width = 0.045f;
        [SerializeField, Tooltip("Copies draw at the lowest player sorting order minus this.")] private int orderBelow = 1;

        private static readonly Vector2[] Directions =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new(.7071f, .7071f), new(-.7071f, .7071f), new(.7071f, -.7071f), new(-.7071f, -.7071f),
        };

        private readonly Dictionary<SpriteRenderer, SpriteRenderer[]> copies = new();
        private readonly HashSet<SpriteRenderer> ownCopies = new();
        private readonly List<SpriteRenderer> found = new();
        private readonly List<SpriteRenderer> stale = new();
        private MaterialPropertyBlock block;
        private float pulseStart = float.NegativeInfinity, pulseLength = 1f;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Briefly thickens the outline and flashes it white, easing back to the material colour.
        public void Pulse(float seconds)
        {
            pulseStart = Time.unscaledTime;
            pulseLength = Mathf.Max(0.05f, seconds);
        }

        private void LateUpdate()
        {
            if (!silhouette) return;
            GetComponentsInChildren(false, found);
            int lowest = int.MaxValue;
            foreach (var sr in found)
                if (!ownCopies.Contains(sr) && sr.enabled && sr.sprite) lowest = Mathf.Min(lowest, sr.sortingOrder);
            if (lowest == int.MaxValue) return;

            float pulse = 1f - Mathf.Clamp01((Time.unscaledTime - pulseStart) / pulseLength);
            pulse *= pulse;
            float drawWidth = width * (1f + 1.6f * pulse);
            block ??= new MaterialPropertyBlock();
            block.SetColor(ColorId, Color.Lerp(silhouette.GetColor(ColorId), Color.white, pulse));

            foreach (var source in found)
            {
                if (ownCopies.Contains(source)) continue;
                if (!copies.TryGetValue(source, out var set)) copies[source] = set = Create(source);
                bool show = source.enabled && source.sprite;
                Vector3 scale = source.transform.lossyScale;
                for (int i = 0; i < set.Length; i++)
                {
                    var copy = set[i];
                    copy.enabled = show;
                    if (!show) continue;
                    copy.sprite = source.sprite;
                    copy.flipX = source.flipX; copy.flipY = source.flipY;
                    copy.sortingLayerID = source.sortingLayerID;
                    copy.sortingOrder = lowest - orderBelow;
                    copy.SetPropertyBlock(block);
                    var offset = Directions[i] * drawWidth; // world space, so it ignores the rig's scale and facing flip
                    copy.transform.localPosition = new Vector3(Safe(offset.x, scale.x), Safe(offset.y, scale.y), 0f);
                }
            }

            // Rig layers are destroyed when the parts change; drop their (already destroyed) copies.
            stale.Clear();
            foreach (var pair in copies) if (!pair.Key) stale.Add(pair.Key);
            foreach (var key in stale)
            {
                foreach (var copy in copies[key]) ownCopies.Remove(copy);
                copies.Remove(key);
            }
        }

        private SpriteRenderer[] Create(SpriteRenderer source)
        {
            var set = new SpriteRenderer[Directions.Length];
            for (int i = 0; i < set.Length; i++)
            {
                var go = new GameObject("Outline");
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(source.transform, false);
                var copy = go.AddComponent<SpriteRenderer>();
                copy.sharedMaterial = silhouette;
                set[i] = copy;
                ownCopies.Add(copy);
            }
            return set;
        }

        private static float Safe(float world, float scale) => Mathf.Abs(scale) > 1e-5f ? world / scale : 0f;

        private void OnDisable()
        {
            foreach (var set in copies.Values)
                foreach (var copy in set)
                    if (copy) Destroy(copy.gameObject);
            copies.Clear();
            ownCopies.Clear();
        }
    }
}
