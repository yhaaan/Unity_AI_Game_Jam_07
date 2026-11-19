using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// A light touch of overgrowth for the lab room (Resources/Lab/Decor):
    ///   * walls: one small shelf fungus low on an end wall
    ///   * pit lips: a thin thorny vine spilling over each floor edge that drops into a shaft or pool
    /// Pure decoration (no colliders), seeded by the scene so a stage always looks the same.
    /// </summary>
    public static class StageDecor
    {
        const int FaceOrder = 1;       // over the floor slab and deck, under plants (3)
        const int WallOrder = 3;       // over the walls (2)

        static readonly Color Face = new(.84f, .8f, .9f, 1f);

        public static void Dress(Transform holder, StageRuntime stage, List<Vector2> keepClear, System.Random rng)
        {
            var room = stage.transform.Find("Course Boundaries");
            var leftWall = room ? room.Find("Left Boundary") : null;
            var rightWall = room ? room.Find("Right Boundary") : null;
            if (!leftWall || !rightWall) return;
            float left = leftWall.GetComponent<Collider2D>().bounds.max.x;
            float right = rightWall.GetComponent<Collider2D>().bounds.min.x;

            var decor = new GameObject("Overgrowth").transform;
            decor.SetParent(holder, false);

            // Restraint: growth only where it would start — low on the walls near the floor and over the lips
            // of pits. Nothing hangs from the ceiling, and the middle of the room stays clean.

            // ---- walls: a shelf fungus low on one wall (its mounting strip is the sprite's left side) ----
            bool onLeft = rng.NextDouble() < .5;
            float fy = 1.05f + (float)rng.NextDouble() * .4f;
            if (onLeft) Place(decor, "deco_bracket_fungus", new Vector2(left - .08f, fy), new Vector2(0, .5f), .62f, false, false, WallOrder, Face);
            else Place(decor, "deco_bracket_fungus", new Vector2(right + .08f, fy), new Vector2(1, .5f), .62f, false, true, WallOrder, Face);

            // ---- pit lips: a thorny vine spilling over each floor edge that drops into a shaft or pool ----
            foreach (var surface in stage.GetComponentsInChildren<BoxCollider2D>())
            {
                if (surface.isTrigger || surface.attachedRigidbody || surface.name != "Island Surface") continue;
                var b = surface.bounds;
                float face = b.max.y - LabRoomArt.DeckHeight;
                if (b.min.x > left + .5f)    // left edge is a drop
                    Place(decor, "deco_thorn_vine", new Vector2(b.min.x - .25f, face + .1f), new Vector2(0, 1), 1.15f, true, true, FaceOrder, Face, squash: .75f);
                if (b.max.x < right - .5f)   // right edge is a drop
                    Place(decor, "deco_thorn_vine", new Vector2(b.max.x + .25f, face + .1f), new Vector2(1, 1), 1.15f, true, false, FaceOrder, Face, squash: .75f);
            }
        }

        /// <summary>
        /// Puts a decor sprite so that the point <paramref name="pivot"/> (0..1 across its bounds) lands on
        /// <paramref name="anchor"/>. <paramref name="size"/> is its height, or its width when sizeIsWidth.
        /// squash scales only the height (flatter swags), stretch only the height of tall pieces (longer roots).
        /// </summary>
        static void Place(Transform parent, string name, Vector2 anchor, Vector2 pivot, float size, bool sizeIsWidth, bool flip,
            int order, Color tint, float squash = 1f, float stretch = 1f)
        {
            var sprite = Resources.Load<Sprite>("Lab/Decor/" + name);
            if (!sprite) return;
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = tint;
            float s = size / (sizeIsWidth ? sprite.bounds.size.x : sprite.bounds.size.y);
            sr.transform.localScale = new Vector3(flip ? -s : s, s * squash * stretch, 1f);
            var b = sr.bounds;
            var at = new Vector2(Mathf.Lerp(b.min.x, b.max.x, pivot.x), Mathf.Lerp(b.min.y, b.max.y, pivot.y));
            sr.transform.position += (Vector3)(anchor - at);
        }

        static bool Free(float a, float b, List<Vector2> blocked)
        {
            foreach (var r in blocked) if (a < r.y && b > r.x) return false;
            return true;
        }
    }
}
