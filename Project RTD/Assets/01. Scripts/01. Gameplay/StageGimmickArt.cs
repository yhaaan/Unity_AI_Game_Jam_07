using ProjectRTD.Core;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Visual children only: the authored bodies, triggers and travel distances stay untouched.
    public sealed class StageGimmickArt : MonoBehaviour
    {
        Sprite[] frames;
        SpriteRenderer view;
        MassPressurePlate2D plate;
        internal static StageGimmickArt Portal(Transform holder, Vector3 floor, SharedMenuArt art, string name)
        {
            var tex = art.doorSheet;
            var grid = art.doorGrid;
            const float height = 2.65f;
            float aspect = (float)tex.width * grid.y / (tex.height * grid.x);
            var door = Make(holder, tex, grid, floor + Vector3.up * (height / 2), new Vector2(height * aspect, height), 4);
            door.name = name;
            door.enabled = false; // StageGoal owns the playback clock, not proximity.
            return door;
        }

        internal void ShowProgress(float t) => view.sprite = frames[Mathf.RoundToInt(Mathf.Clamp01(t) * (frames.Length - 1))];
        float frame;

        public static void Install(StageRuntime stage)
        {
            var art = SharedMenuArt.Loaded;
            if (!art) return;
            foreach (var crate in stage.GetComponentsInChildren<StagePowerCrate2D>())
                DressCrate(crate.GetComponent<BoxCollider2D>(), art.crate, art.crateBody);
            foreach (var plate in stage.GetComponentsInChildren<MassPressurePlate2D>())
            {
                var visual = DressCollider(plate.GetComponent<BoxCollider2D>(), art.plateSheet, art.plateGrid);
                if (visual) visual.plate = plate;
            }
            foreach (var roof in stage.GetComponentsInChildren<BoxCollider2D>())
            {
                if (roof.name != "Cave Roof" || !art.cupboardLeft || !art.cupboardRight) continue;
                var b = roof.bounds;
                Hide(roof.transform);
                var lintel = roof.transform.parent.Find("Cave Lintel");
                if (lintel) Hide(lintel);
                Vector2 size = new Vector2(b.size.x / 2, b.size.y);
                Make(roof.transform, art.cupboardLeft, Vector2Int.one, b.center - Vector3.right * size.x / 2, size, 3);
                Make(roof.transform, art.cupboardRight, Vector2Int.one, b.center + Vector3.right * size.x / 2, size, 3);
            }

        }

        static void Hide(Transform root)
        {
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
        }

        // The crate art is cropped to its opaque body so the sprite and the collider are the same box.
        static void DressCrate(BoxCollider2D box, Texture2D texture, RectInt body)
        {
            if (!box || !texture) return;
            if (body.width <= 0 || body.height <= 0) body = new RectInt(0, 0, texture.width, texture.height);
            Hide(box.transform);
            // Sink the art slightly so it sits in the island's grass instead of hovering on the collider line.
            const float sink = 0.15f;
            Make(box.transform, texture, Vector2Int.one, box.bounds.center + Vector3.down * sink, box.bounds.size, 4,
                new Rect(body.x, body.y, body.width, body.height));
        }

        static StageGimmickArt DressCollider(BoxCollider2D box, Texture2D texture, Vector2Int grid)
        {
            if (!box || !texture || grid.x < 1 || grid.y < 1) return null;
            Hide(box.transform);
            // Colliders are authored at the frame's aspect, so the art fills them exactly.
            return Make(box.transform, texture, grid, box.bounds.center, box.bounds.size, 4);
        }

        static StageGimmickArt Make(Transform parent, Texture2D tex, Vector2Int grid, Vector3 center, Vector2 size, int order, Rect? crop = null)
        {
            var go = new GameObject(tex.name);
            go.transform.position = center;
            var visual = go.AddComponent<StageGimmickArt>();
            visual.view = go.AddComponent<SpriteRenderer>();
            visual.frames = new Sprite[grid.x * grid.y];
            float w = (float)tex.width / grid.x, h = (float)tex.height / grid.y;
            for (int i = 0; i < visual.frames.Length; ++i)
            {
                var rect = crop ?? new Rect(i % grid.x * w, (grid.y - 1 - i / grid.x) * h, w, h);
                visual.frames[i] = Sprite.Create(tex, rect, Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            }
            visual.view.sprite = visual.frames[0];
            visual.view.sortingOrder = order;
            if (crop.HasValue) { w = crop.Value.width; h = crop.Value.height; }
            go.transform.localScale = new Vector3(size.x * 100 / w, size.y * 100 / h, 1);
            go.transform.SetParent(parent, true);
            visual.enabled = visual.frames.Length > 1;
            return visual;
        }

        void Update()
        {
            bool active = plate && plate.IsPressed;
            frame = Mathf.MoveTowards(frame, active ? frames.Length - 1 : 0, Time.deltaTime * 12);
            view.sprite = frames[Mathf.RoundToInt(frame)];
        }

        void OnDestroy()
        {
            if (frames != null) foreach (var sprite in frames) if (sprite) Destroy(sprite);
        }
    }
}
