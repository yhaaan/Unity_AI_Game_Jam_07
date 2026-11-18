using System.Linq;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Extend the approaches, never the puzzle, player, bridge or water volumes.
    // Run once per stage from the editor (Tools/RTD/Bake Stage Layout) and saved into the scene, so the
    // walls, spawn and floors are real scene objects a developer can move in the Scene view.
    public static class StageCourseLayout
    {
        public const float Width = 24.8f;
        const string Marker = "Course Boundaries";
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
            if (!floorArt) Debug.LogWarning("StageCourseLayout: SharedMenuArt.labFloor not loaded, floors keep their placeholder art.");
            foreach (var floor in floors)
            {
                var b = floor.bounds;
                float a = floor == first ? left : b.min.x, z = floor == last ? right : b.max.x;
                float sx = floor.transform.lossyScale.x;
                floor.offset += Vector2.right * (((a + z) * .5f - b.center.x) / sx);
                floor.size = new Vector2((z - a) / Mathf.Abs(sx), floor.size.y);
                if (!floorArt) continue;
                FloorArt(holder, floorArt, "Lab Floor", floor, null, Color.white, 1);
            }
            if (floorArt)
            {
                // A pit between two floors shows the same floor, sunk and shaded, instead of the room behind.
                for (int i = 1; i < floors.Length; i++)
                    if (floors[i].bounds.min.x - floors[i - 1].bounds.max.x > .05f)
                        FloorArt(holder, floorArt, "Pit Floor", floors[i - 1], floors[i], PitTint, -10);
                RetirePlaceholders(stage);
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
            float ground = Mathf.Min(first.bounds.max.y, last.bounds.max.y);
            var leftWall = AddWall(holder, left, ground, true);
            var rightWall = AddWall(holder, right, ground, false);
            Physics2D.SyncTransforms();
            StageOverviewCamera.Install(stage, leftWall, rightWall);
        }

        // The grey island placeholders the lab floor replaces (their sprites and the rock pieces).
        public static void RetirePlaceholders(StageRuntime stage)
        {
            foreach (var floor in stage.GetComponentsInChildren<BoxCollider2D>(true))
                if (floor.name == "Island Surface")
                    foreach (var sr in floor.GetComponentsInChildren<SpriteRenderer>(true)) StageArt.Retire(sr);
            foreach (var sr in stage.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr && sr.name is "Rock Base" or "Rock Face" or "Rock Shading") StageArt.Retire(sr);
        }

        // floor.png's band, 9-sliced so its top rim keeps its thickness; LabFloorArt keeps it on its collider(s).
        static void FloorArt(Transform holder, Sprite sprite, string name, BoxCollider2D from, BoxCollider2D to, Color tint, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = order;
            sr.drawMode = SpriteDrawMode.Sliced;
            var art = go.AddComponent<LabFloorArt>();
            art.from = from; art.to = to; art.pit = to;
        }

        // Invisible: the camera stops at this edge, so the screen border itself is the wall.
        static BoxCollider2D AddWall(Transform holder, float edge, float ground, bool left)
        {
            const float width = 2.4f, height = 30f;
            var go = new GameObject(left ? "Left Boundary" : "Right Boundary");
            go.transform.SetParent(holder, false);
            go.transform.position = new Vector3(edge + (left ? -1 : 1)*width*.5f, ground, 0);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width, height); box.offset = Vector2.up * (height * .5f - 4f);
            return box;
        }
    }
}
