using TMPro;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class StageStatGate2D : MonoBehaviour
    {
        public enum Condition { HighJump, LowCeiling }
        [SerializeField] private Condition condition;
        private PlayerPartLoadout loadout;
        private BoxCollider2D barrier;
        private bool canPass;

        // ---- Low ceiling: make the invisible height check visible and readable ----
        // The barrier is a box from the floor up to the roof. Show it as laser curtains at both tunnel mouths
        // (red = too tall, faint green = fits) with a hazard stripe on the roof's lower edge, flash it when a tall
        // build bumps into it, and let builds that fit duck under the roof.
        static readonly Color Blocked = new(1f, 0.22f, 0.18f, 1f), Clear = new(0.45f, 1f, 0.6f, 0.35f);
        const int SortingOrder = 6; // above the roof cupboards (3), below the foreground props
        static Sprite beam, stripes;
        SpriteRenderer[] curtains;
        float flashUntil, nextBumpText;
        Transform visual;
        float visualBaseY, crouch;
        bool crouchApplied;

        private void Start()
        {
            barrier = GetComponent<BoxCollider2D>();
            if (!StageRuntime.Instance || !StageRuntime.Instance.Player ||
                !StageRuntime.Instance.Player.TryGetComponent(out loadout)) return;
            if (condition == Condition.LowCeiling) BuildCeilingMarkers();
            loadout.Changed += Apply;
            Apply(loadout.Current);
        }

        private void Apply(PlayerPartBuild build)
        {
            canPass = build != null && (condition == Condition.HighJump ? build.Jump >= 3 : build.Height is >= 0 and <= 2);
            barrier.enabled = !canPass;
        }

        private void OnDestroy()
        {
            if (loadout) loadout.Changed -= Apply;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (condition != Condition.LowCeiling || canPass || !collision.collider.CompareTag("Player")) return;
            flashUntil = Time.time + 0.25f;
            if (Time.time < nextBumpText) return;
            nextBumpText = Time.time + 1.4f;
            BumpText(collision.collider.bounds);
        }

        private void LateUpdate()
        {
            if (condition != Condition.LowCeiling || curtains == null) return;
            UpdateCurtains();
            UpdateCrouch();
        }

        void UpdateCurtains()
        {
            bool flash = Time.time < flashUntil;
            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 14f, 0f);
            foreach (var curtain in curtains)
            {
                var c = canPass ? Clear : Blocked;
                if (!canPass) c.a = flash ? 1f : 0.75f * flicker;
                curtain.color = c;
                curtain.transform.localScale = new Vector3(flash ? 1.8f : 1f, 1f, 1f);
            }
        }

        // Builds that fit bow down while under the roof, so it is obvious they squeeze through.
        void UpdateCrouch()
        {
            if (!visual) return;
            var player = StageRuntime.Instance ? StageRuntime.Instance.Player : null;
            if (!player) return;
            var b = barrier.bounds;
            bool under = canPass && player.position.x > b.min.x - 0.4f && player.position.x < b.max.x + 0.4f;
            crouch = Mathf.MoveTowards(crouch, under ? 1f : 0f, Time.deltaTime * 6f);
            if (crouch > 0f)
            {
                var s = visual.localScale;
                s.y = visualBaseY * (1f - 0.22f * crouch);
                visual.localScale = s;
                crouchApplied = true;
            }
            else if (crouchApplied)
            {
                var s = visual.localScale;
                s.y = visualBaseY;
                visual.localScale = s;
                crouchApplied = false;
            }
        }

        void BuildCeilingMarkers()
        {
            if (!beam) beam = MakeBeam();
            if (!stripes) stripes = MakeStripes();
            var b = barrier.bounds;
            curtains = new[] { Curtain("Height Laser L", b.min.x, b), Curtain("Height Laser R", b.max.x, b) };

            // Hazard stripe along the roof's lower edge: the clearance line.
            var strip = new GameObject("Clearance Stripe").AddComponent<SpriteRenderer>();
            strip.transform.SetParent(transform, false);
            strip.transform.position = new Vector3(b.center.x, b.max.y - 0.07f, 0f);
            strip.sprite = stripes;
            strip.drawMode = SpriteDrawMode.Tiled;
            strip.size = new Vector2(b.size.x + 0.1f, 0.16f);
            strip.sortingOrder = SortingOrder;

            var visualPart = StageRuntime.Instance.Player.GetComponentInChildren<PlayerPartVisual>(true);
            if (visualPart) { visual = visualPart.transform; visualBaseY = visual.localScale.y; }
        }

        SpriteRenderer Curtain(string name, float x, Bounds b)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.position = new Vector3(x, b.center.y, 0f);
            sr.sprite = beam;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(0.28f, b.size.y);
            sr.sortingOrder = SortingOrder;
            return sr;
        }

        void BumpText(Bounds player)
        {
            var go = new GameObject("Too Tall");
            go.transform.position = new Vector3(player.center.x, player.max.y + 0.35f, 0f);
            var text = go.AddComponent<TextMeshPro>();
            text.text = "키가 너무 커요!";
            text.fontSize = 3.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Blocked;
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(11, 26, 58, 255);
            text.sortingOrder = 50;
            text.rectTransform.sizeDelta = new Vector2(6f, 1f);
            go.AddComponent<FloatAway>();
        }

        // Rises, pops, and fades out.
        sealed class FloatAway : MonoBehaviour
        {
            float born;
            TMP_Text text;
            Vector3 start;
            void Start() { born = Time.time; text = GetComponent<TMP_Text>(); start = transform.position; }
            void Update()
            {
                float k = (Time.time - born) / 0.9f;
                if (k >= 1f) { Destroy(gameObject); return; }
                float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.1f, k / 0.15f) : Mathf.Lerp(1.1f, 1f, Mathf.Min(1f, (k - 0.15f) / 0.2f));
                transform.localScale = Vector3.one * pop;
                transform.position = start + Vector3.up * (0.5f * k);
                var c = text.color; c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f; text.color = c;
            }
        }

        // Vertical laser: bright core with a soft glow, sliced so it stretches to any height.
        static Sprite MakeBeam()
        {
            const int w = 16, h = 16;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float core = d < 0.22f ? 1f : 0f;
                float glow = Mathf.Pow(Mathf.Clamp01(1f - d), 2f) * 0.55f;
                byte a = (byte)(255 * Mathf.Max(core, glow));
                byte white = (byte)(core > 0f ? 255 : 255);
                px[y * w + x] = new Color32(white, white, white, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(0, 4, 0, 4));
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        // Yellow/black diagonal hazard stripes, tileable horizontally.
        static Sprite MakeStripes()
        {
            const int w = 32, h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color32[w * h];
            Color32 yellow = new(255, 200, 20, 255), ink = new(11, 26, 58, 255);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = y == 0 || y == h - 1 ? ink : ((x + y) / 8) % 2 == 0 ? yellow : ink;
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 50f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }
}
