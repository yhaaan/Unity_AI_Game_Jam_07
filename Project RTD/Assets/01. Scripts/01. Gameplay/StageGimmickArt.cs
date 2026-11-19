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
        [SerializeField, Tooltip("Press door: the frame follows how far this plate has opened its door.")]
        MassPressurePlate2D pressPlate;
        [SerializeField, Tooltip("9-slice drawing: the borders (px, left/bottom/right/top) keep their size.")]
        bool sliced;
        [SerializeField] Vector4 border;
        [SerializeField] float pixelsPerUnit = 100;
        [SerializeField] Vector2 slicedSize;
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
                frames[i] = Sprite.Create(texture, rect, Vector2.one * .5f, pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
                frames[i].hideFlags = HideFlags.DontSave;
            }
            if (sliced) { view.drawMode = SpriteDrawMode.Sliced; view.size = slicedSize; }
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
            Upgrade(stage);
        }

        // Cave roof and press door: re-run safely on every load, so stages baked before this art get it in Play
        // (Tools/RTD/Bake Stage Layout saves it into the scenes).
        public static void Upgrade(StageRuntime stage)
        {
            var art = SharedMenuArt.Loaded;
            if (!art) return;
            foreach (var roof in stage.GetComponentsInChildren<BoxCollider2D>(true))
                if (roof.name == "Cave Roof") DressCaveRoof(roof, art.caveRoof);
            foreach (var plate in stage.GetComponentsInChildren<MassPressurePlate2D>(true))
                DressPressDoor(stage, plate, art.pressSheet, art.pressGrid);
        }

        // cave 1.png: the hazard-striped bottom band is the low ceiling's edge. 9-sliced so the band keeps the
        // art's proportions and only the flat panel above it stretches to the roof's height.
        static readonly Vector4 CaveBorder = new Vector4(20, 150, 20, 20);
        static void DressCaveRoof(BoxCollider2D roof, Texture2D texture)
        {
            if (!texture) return;
            bool done = false;
            foreach (var old in roof.GetComponentsInChildren<StageGimmickArt>(true))
                if (old.texture == texture) done = true;
                else StageArt.Remove(old.gameObject); // the previous cupboard pair
            if (done) return;
            var b = roof.bounds;
            Hide(roof.transform);
            var lintel = roof.transform.parent ? roof.transform.parent.Find("Cave Lintel") : null;
            if (lintel) Hide(lintel);
            var go = new GameObject(texture.name);
            go.transform.position = b.center;
            var view = go.AddComponent<SpriteRenderer>();
            view.sortingOrder = 3;
            var visual = go.AddComponent<StageGimmickArt>();
            visual.texture = texture;
            visual.sliced = true;
            visual.pixelsPerUnit = texture.width / b.size.x; // the band keeps the art's aspect at the roof's width
            visual.border = CaveBorder * texture.width / 1132f; // measured on the 1132 px source
            visual.slicedSize = b.size;
            visual.Cut();
            view.sprite = visual.frames[0];
            go.transform.SetParent(roof.transform, true);
            visual.enabled = false;
        }

        // press_retract_sheet.png, measured on one 1024 x 2752 frame of the source: the opaque art spans
        // x 155..867, starts at y 165, and the press foot ends at y 2685 (frame 0) .. 2067 (last frame).
        const float PressLeft = 155f / 1024, PressRight = 867f / 1024, PressTop = 165f / 2752;
        const float FootClosed = 2685f / 2752, FootOpen = 2067f / 2752;
        const string PressName = "Press Door";

        // The plate's "Powered Door" becomes a ceiling press: the art hangs from the room's ceiling down to the
        // floor, the plate drives its frames, and the door's collider covers the press and rises with its foot.
        static void DressPressDoor(StageRuntime stage, MassPressurePlate2D plate, Texture2D texture, Vector2Int grid)
        {
            var door = plate.Door;
            if (!texture || grid.x < 1 || grid.y < 1 || !door || door.name != "Powered Door" || !door.parent) return;
            if (!door.TryGetComponent(out BoxCollider2D box) || door.parent.Find(PressName)) return;

            Vector3 closed = plate.ClosedDoorPosition;
            float floor = closed.y + (box.offset.y - box.size.y / 2) * door.lossyScale.y;
            var overview = Object.FindFirstObjectByType<StageOverviewCamera>(FindObjectsInactive.Include);
            float ceiling = floor + (overview ? overview.roomHeight : 12.35f);

            float frameH = (ceiling - floor) / (FootClosed - PressTop);
            float frameW = frameH * (texture.width / (float)grid.x) / (texture.height / (float)grid.y);
            float width = frameW * (PressRight - PressLeft);
            // the art's top edge meets the ceiling, its middle sits on the door's x
            var center = new Vector3(closed.x - frameW * ((PressLeft + PressRight) / 2 - .5f), ceiling + frameH * (PressTop - .5f), 0);

            Hide(door);
            var housing = door.parent.Find("Door Housing");
            if (housing)
            {
                Hide(housing);
                if (housing.TryGetComponent(out BoxCollider2D housingBox)) housingBox.enabled = false;
            }

            // One collider for the whole press: from its foot (closed: the floor) up to the ceiling.
            float sx = Mathf.Abs(door.lossyScale.x), sy = Mathf.Abs(door.lossyScale.y);
            float height = ceiling - floor;
            box.size = new Vector2(width / sx, height / sy);
            box.offset = new Vector2(box.offset.x, (floor + height / 2 - closed.y) / sy);
            plate.SetOpenRise(frameH * (FootClosed - FootOpen));

            var visual = Make(door.parent, texture, grid, center, new Vector2(frameW, frameH), 4);
            visual.name = PressName;
            visual.pressPlate = plate;
            visual.enabled = true;
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
            Make(box.transform, texture, Vector2Int.one, box.bounds.center + Vector3.down * sink, box.bounds.size, StagePowerCrate2D.SortingOrder,
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
            if (pressPlate)
            {
                view.sprite = frames[Mathf.RoundToInt(pressPlate.DoorOpen * (frames.Length - 1))];
                return;
            }
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
