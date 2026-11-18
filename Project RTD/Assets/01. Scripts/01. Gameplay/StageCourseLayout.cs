using System.Linq;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Extend the approaches, never the puzzle, player, bridge or water volumes.
    public static class StageCourseLayout
    {
        public const float Width = 24.8f;
        const string Marker = "Course Boundaries";
        // The floor art reaches this far below the walkway, past the lowest point the camera may show.
        const float FloorDepth = 6f;
        // Spawn distance from the left wall that fits the whole entrance door (drawn 0.6 left of the spawn, ~1.75 half-width).
        const float EntranceClearance = 2.5f;
        // Lab-floor tint for the pit below a gap between two floors.
        static readonly Color PitTint = new(.55f, .58f, .82f, 1f);

        public static void Apply(StageRuntime stage)
        {
            if (stage.transform.Find(Marker) || !stage.SpawnPoint || !stage.Player) return;
            var floors = stage.GetComponentsInChildren<BoxCollider2D>().Where(c => c.name == "Island Surface").OrderBy(c => c.bounds.min.x).ToArray();
            if (floors.Length == 0) return;
            var first = floors[0]; var last = floors[floors.Length - 1];
            float oldLeft = first.bounds.min.x, oldRight = last.bounds.max.x;
            float left = (oldLeft + oldRight - Width) / 2f, right = left + Width;
            var holder = new GameObject(Marker).transform;
            holder.SetParent(stage.transform, false);
            var floorArt = ProjectRTD.Core.SharedMenuArt.Loaded ? ProjectRTD.Core.SharedMenuArt.Loaded.labFloor : null;
            foreach (var floor in floors)
            {
                var b = floor.bounds;
                float a = floor == first ? left : b.min.x, z = floor == last ? right : b.max.x;
                float sx = floor.transform.lossyScale.x;
                floor.offset += Vector2.right * (((a + z) * .5f - b.center.x) / sx);
                floor.size = new Vector2((z - a) / Mathf.Abs(sx), floor.size.y);
                if (!floorArt) continue;
                FloorArt(holder, floorArt, "Lab Floor", a, z, b.max.y, Color.white, 1);
                foreach (var sr in floor.GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
            }
            if (floorArt)
            {
                // A pit between two floors shows the same floor, sunk and shaded, instead of the room behind.
                for (int i = 1; i < floors.Length; i++)
                {
                    float a = floors[i - 1].bounds.max.x, z = floors[i].bounds.min.x;
                    if (z - a > .05f) FloorArt(holder, floorArt, "Pit Floor", a, z, floors[i].bounds.max.y - .9f, PitTint, -10);
                }
                foreach (var sr in first.transform.parent.GetComponentsInChildren<SpriteRenderer>())
                    if (sr.name is "Rock Base" or "Rock Face" or "Rock Shading") sr.enabled = false;
            }
            var delta = Vector3.right * (left - oldLeft);
            // The entrance door stands just left of the spawn; keep all of it on screen.
            float spawnX = stage.SpawnPoint.position.x + delta.x;
            delta.x += Mathf.Clamp(spawnX, left + EntranceClearance, right - .6f) - spawnX;
            stage.SpawnPoint.position += delta;
            stage.Player.position += delta;
            if (stage.Player.TryGetComponent<Rigidbody2D>(out var body)) body.position = stage.Player.position;
            var start = first.transform.parent.Find("Start Marker");
            if (start) start.position += delta;
            var goal = first.transform.parent.Find("Goal");
            if (goal) goal.position += Vector3.right * (right - oldRight);
            AddWall(holder, left, first.bounds.max.y, true);
            AddWall(holder, right, last.bounds.max.y, false);
            Physics2D.SyncTransforms();
            StageOverviewCamera.Install(stage, left, right, Mathf.Min(first.bounds.max.y, last.bounds.max.y));
        }

        // floor.png's band, 9-sliced so its top rim keeps its thickness and only the face below stretches.
        static void FloorArt(Transform holder, Sprite sprite, string name, float a, float z, float top, Color tint, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(holder, false);
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = order;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(z - a, FloorDepth);
            sr.transform.position = new Vector3((a + z) * .5f, top - FloorDepth * .5f, 0);
        }

        // Invisible: the camera stops at this edge, so the screen border itself is the wall.
        static void AddWall(Transform holder, float edge, float ground, bool left)
        {
            const float width = 2.4f, height = 30f;
            var go = new GameObject(left ? "Left Boundary" : "Right Boundary");
            go.transform.SetParent(holder, false);
            go.transform.position = new Vector3(edge + (left ? -1 : 1)*width*.5f, ground, 0);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width, height); box.offset = Vector2.up * (height * .5f - 4f);
        }
    }
}
