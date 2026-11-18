using System.Linq;
using ChimeraGroove.Plants;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Extend the approaches, never the puzzle, player, bridge or water volumes.
    public static class StageCourseLayout
    {
        public const float Width = 24.8f;
        const string Marker = "Course Boundaries";
        static readonly string[] Islands = { "dirt-pipe", "lab-rock", "root-fossil", "sand-fossil", "dirt-roots", "sand-arch", "slate", "red-strata" };

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
            int.TryParse(stage.gameObject.scene.name.Replace("Stage", ""), out int level);
            int skin = Mathf.Max(0, level - 1);
            foreach (var floor in floors)
            {
                var b = floor.bounds;
                float a = floor == first ? left : b.min.x, z = floor == last ? right : b.max.x;
                float sx = floor.transform.lossyScale.x;
                floor.offset += Vector2.right * (((a + z) * .5f - b.center.x) / sx);
                floor.size = new Vector2((z - a) / Mathf.Abs(sx), floor.size.y);
                // Use two panels on long runs instead of stretching a single island silhouette.
                int panels = Mathf.CeilToInt((z-a) / 15f);
                for (int i = 0; i < panels; i++)
                {
                    float w = (z-a) / panels;
                    var sprite = Resources.Load<Sprite>("Islands/" + Islands[(skin++) % Islands.Length]);
                    if (!sprite) continue;
                    var sr = new GameObject(sprite.name).AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(holder, false); sr.sprite = sprite;
                    sr.sortingOrder = 1;
                    float height = Mathf.Clamp(w * sprite.bounds.size.y / sprite.bounds.size.x, 1.4f, 3.3f);
                    sr.transform.localScale = new Vector3(w / sprite.bounds.size.x, height / sprite.bounds.size.y, 1);
                    sr.transform.position = new Vector3(a + (i+.5f)*w, b.max.y - height*.5f, 0);
                }
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
        }

        static void AddWall(Transform holder, float edge, float ground, bool left)
        {
            const float width = 2.4f, height = 6.4f;
            var go = new GameObject(left ? "Left Boundary" : "Right Boundary");
            go.transform.SetParent(holder, false);
            go.transform.position = new Vector3(edge + (left ? -1 : 1)*width*.5f, ground, 0);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width, height + 4); box.offset = Vector2.up * ((height-4)*.5f);
            if (left)
            {
                var art = go.AddComponent<BrickWall>(); art.width = width; art.addCollider = false;
            }
            else
            {
                var art = go.AddComponent<JumpWall>(); art.style = JumpWall.Style.MossyCliff;
                art.width = width; art.height = height;
            }
        }
    }
}
