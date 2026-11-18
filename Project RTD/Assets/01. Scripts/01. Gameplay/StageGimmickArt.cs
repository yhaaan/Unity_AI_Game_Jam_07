using ProjectRTD.Core;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Visual children only: the authored bodies, triggers and travel distances stay untouched.
    // The frames are cut from the texture whenever the object loads (also in edit mode), so art baked
    // into a scene shows in the Scene view before Play. The cut sprites themselves are never saved.
    [ExecuteAlways]
    public sealed class StageGimmickArt : MonoBehaviour
    {
        [SerializeField] Texture2D texture;
        [SerializeField] Vector2Int grid = Vector2Int.one;
        [SerializeField] bool cropped;
        [SerializeField] Rect crop;
        [SerializeField] MassPressurePlate2D plate;
        Sprite[] frames;
        SpriteRenderer view;
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

        internal void ShowProgress(float t)
        {
            if (!Cut()) return;
            view.sprite = frames[Mathf.RoundToInt(Mathf.Clamp01(t) * (frames.Length - 1))];
        }

        void Awake()
        {
            if (Cut()) view.sprite = frames[0];
        }

        bool Cut()
        {
            if (frames != null && frames.Length > 0) return true;
            if (!view) view = GetComponent<SpriteRenderer>();
            if (!view || !texture || grid.x < 1 || grid.y < 1) return false;
            frames = new Sprite[cropped ? 1 : grid.x * grid.y];
            float w = (float)texture.width / grid.x, h = (float)texture.height / grid.y;
            for (int i = 0; i < frames.Length; ++i)
            {
                var rect = cropped ? crop : new Rect(i % grid.x * w, (grid.y - 1 - i / grid.x) * h, w, h);
                frames[i] = Sprite.Create(texture, rect, Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
                frames[i].hideFlags = HideFlags.DontSave;
            }
            return true;
        }
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
                if (visual) { visual.plate = plate; visual.enabled = true; }
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
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>()) StageArt.Retire(sr);
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
            var view = go.AddComponent<SpriteRenderer>();
            view.sortingOrder = order;
            var visual = go.AddComponent<StageGimmickArt>();
            visual.texture = tex;
            visual.grid = grid;
            visual.cropped = crop.HasValue;
            if (crop.HasValue) visual.crop = crop.Value;
            if (visual.Cut()) view.sprite = visual.frames[0];
            float w = crop?.width ?? (float)tex.width / grid.x, h = crop?.height ?? (float)tex.height / grid.y;
            go.transform.localScale = new Vector3(size.x * 100 / w, size.y * 100 / h, 1);
            go.transform.SetParent(parent, true);
            visual.enabled = false; // only pressure plates animate on their own (Install turns them on)
            return visual;
        }

        void Update()
        {
            if (!Application.isPlaying || !Cut()) return;
            bool active = plate && plate.IsPressed;
            frame = Mathf.MoveTowards(frame, active ? frames.Length - 1 : 0, Time.deltaTime * 12);
            view.sprite = frames[Mathf.RoundToInt(frame)];
        }

        void OnDestroy()
        {
            if (frames == null) return;
            foreach (var sprite in frames)
                if (sprite) { if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite); }
        }
    }
}
