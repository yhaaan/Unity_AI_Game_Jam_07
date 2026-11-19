using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Extend the approaches, never the puzzle, player, bridge or water volumes.
    public static class StageCourseLayout
    {
        public const float Width = 24.8f;
        const string Marker = "Course Boundaries";

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
            var spans = new List<LabRoomArt.Span>();
            foreach (var floor in floors)
            {
                var b = floor.bounds;
                float a = floor == first ? left : b.min.x, z = floor == last ? right : b.max.x;
                float sx = floor.transform.lossyScale.x;
                floor.offset += Vector2.right * (((a + z) * .5f - b.center.x) / sx);
                floor.size = new Vector2((z - a) / Mathf.Abs(sx), floor.size.y);
                spans.Add(new LabRoomArt.Span(a, z, b.max.y));
                foreach (var sr in floor.GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
            }
            foreach (var sr in first.transform.parent.GetComponentsInChildren<SpriteRenderer>())
                if (sr.name is "Rock Base" or "Rock Face" or "Rock Shading") sr.enabled = false;
            var delta = Vector3.right * (left - oldLeft);
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

            // the whole course becomes one closed lab room: rail floors on pillars, shafts or pools in the gaps,
            // pillar walls at both ends and a rail ceiling
            var pools = new List<Bounds>();
            int water = LayerMask.NameToLayer("Water");
            foreach (var col in stage.GetComponentsInChildren<Collider2D>())
                if (col.gameObject.layer == water) pools.Add(col.bounds);
            LabRoomArt.BuildRoom(holder, spans, left, right, WallWidth, pools);

            // pools become toxic sludge: hide the old blue basin overlay and its waterline, keep the swim volumes
            if (pools.Count > 0)
            {
                foreach (var sr in stage.GetComponentsInChildren<SpriteRenderer>())
                    if (sr.name is "Water Basin" or "Waterline") sr.enabled = false;
                foreach (var pool in pools) LabRoomArt.ToxicPool(holder, pool);
            }
        }

        const float WallWidth = 2.4f;

        // collider only: the room's pillar walls are drawn by LabRoomArt
        static void AddWall(Transform holder, float edge, float ground, bool left)
        {
            const float height = 6.4f;
            var go = new GameObject(left ? "Left Boundary" : "Right Boundary");
            go.transform.SetParent(holder, false);
            go.transform.position = new Vector3(edge + (left ? -1 : 1) * WallWidth * .5f, ground, 0);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(WallWidth, height + 4); box.offset = Vector2.up * ((height - 4) * .5f);
        }
    }
}
