using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ChimeraGroove.Bubble
{
    /// <summary>
    /// Comic thought bubble of any size (Resources/ThoughtBubble). The GameObject's position is the tip of the
    /// tail (the smallest circle), so put it right above a character's head, or set <see cref="follow"/>.
    ///   Show("text") pops it in (auto-sized to the text unless size is set), Hide() pops it out.
    /// Top and bottom halves repeat their own seamless bump tile; the middle band stretches for extra lines.
    /// </summary>
    public class ThoughtBubble : MonoBehaviour
    {
        public const float PPU = 100f;
        public enum Tail { Right, Left, None }

        [TextArea] public string text = "?";
        [Tooltip("picture drawn in the cloud instead of text (e.g. a part sprite)")] public Sprite icon;
        [Tooltip("share of the cloud's inner area the icon fills")] public float iconFill = .9f;
        [Tooltip("cloud size in art pixels; 0 = fit the text")] public Vector2 size = Vector2.zero;
        [Tooltip("world size: 1 = 100 art px per unit")] public float scale = .25f;
        public Tail tail = Tail.Right;
        public TMP_FontAsset font;
        [Tooltip("text height in art pixels")] public float fontSize = 64f;
        public Color textColor = new Color32(0x2a, 0x24, 0x20, 0xff);
        public string sortingLayerName = "Default";
        public int sortingOrder = 50;
        public Transform follow;
        public Vector3 followOffset = new Vector3(.2f, 1.1f, 0f);
        public bool showOnStart = true;
        [Tooltip("seconds; 0 = stay until Hide()")] public float autoHideAfter = 0f;
        public float bob = .06f;

        public bool Visible { get; private set; }
        public event Action Hidden;

#pragma warning disable 0649
        [Serializable] class Cloud { public float w, h, split, band; public float[] top_cut, bot_cut; }
        [Serializable] class Meta
        {
            public Cloud cloud; public float[] tail_big, tail_small, small_from_big;
            public int[] prof_bot_left, prof_bot_mid, prof_bot_right, prof_tail_big_top;
        }
#pragma warning restore 0649
        static Meta meta;
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<Sprite, Sprite> trimmed = new Dictionary<Sprite, Sprite>();
        const float Tuck = 10f;                        // the big circle tucks this far under the cloud's real bottom edge
        static readonly Vector2 SmallIn = new Vector2(-6f, -14f);

        Transform pivot, art;
        TextMeshPro label;
        float w, h, shownAt = -10f, hideAt = -10f, time;
        int order;

        static Sprite S(string n)
        {
            if (!sprites.TryGetValue(n, out var s) || s == null) sprites[n] = s = Resources.Load<Sprite>("ThoughtBubble/" + n);
            return s;
        }

        void Start() { if (showOnStart) Show(text); }

        /// <summary>Sets the text, rebuilds the bubble and pops it in.</summary>
        public void Show(string newText)
        {
            text = newText;
            Rebuild();
            Visible = true; shownAt = time; hideAt = -10f;
            if (pivot) pivot.gameObject.SetActive(true);
        }

        /// <summary>Shows a picture (no text) in the cloud and pops it in.</summary>
        public void Show(Sprite picture)
        {
            icon = picture;
            Show(string.Empty);
        }

        public void Hide()
        {
            if (!Visible) return;
            Visible = false; hideAt = time;
        }

        public void Rebuild()
        {
            if (meta == null)
            {
                var json = Resources.Load<TextAsset>("ThoughtBubble/bubble");
                if (json == null) { Debug.LogError("ThoughtBubble: missing Resources/ThoughtBubble/bubble.json"); return; }
                meta = JsonUtility.FromJson<Meta>(json.text);
            }
            if (pivot) Destroy(pivot.gameObject);
            pivot = new GameObject("Bubble").transform;          // pop / bob pivot = tail tip = this transform
            pivot.SetParent(transform, false);
            art = new GameObject("Art").transform;
            art.SetParent(pivot, false);
            order = 0;

            var C = meta.cloud;
            // label first so the cloud can be sized around it
            label = new GameObject("Text").AddComponent<TextMeshPro>();
            label.transform.SetParent(pivot, false);
            if (font == null) font = TMP_Settings.defaultFontAsset;
            if (font != null) label.font = font;
            label.text = text;
            label.color = textColor;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = fontSize / PPU * scale * 10f;        // TMP world font size: 10 ≈ 1 unit
            label.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            label.sortingOrder = sortingOrder + 30;
            Vector2 pref = label.GetPreferredValues(text);
            w = size.x > 0f ? size.x : Mathf.Max(C.w * .75f, pref.x * PPU / scale + 300f);
            h = size.y > 0f ? size.y : Mathf.Max(C.h * .8f, pref.y * PPU / scale + 300f);
            w = Mathf.Max(C.w * .75f, w); h = Mathf.Max(C.h * .8f, h);

            float topH = C.split, botH = C.h - C.split - C.band, bandH = Mathf.Max(C.band, h - topH - botH);
            // tail tip (bottom of the small circle) is the origin
            var ts = meta.tail_small; var tb = meta.tail_big;
            float bx = Mathf.Round(w - (C.w - tb[0])), by = BigTailY(bx, bandH);
            float tsx = bx + meta.small_from_big[0] + SmallIn.x, tsy = by + meta.small_from_big[1] + SmallIn.y;
            float tipX = tsx + ts[2] / 2f, tipY = tsy + ts[3] - 6f;
            if (tail == Tail.None) { tipX = w * .5f; tipY = h; }
            art.localScale = new Vector3(tail == Tail.Left ? -scale : scale, scale, 1f);
            art.localPosition = Vector3.zero;
            Vector2 P(float px, float py) => new Vector2((px - tipX) / PPU, (tipY - py) / PPU);

            if (tail != Tail.None)
            {
                Piece("tail-big", bx, by, tb[2], tb[3], P);
                Piece("tail-small", tsx, tsy, ts[2], ts[3], P);
            }
            Row("top", 0f, topH, C.top_cut, P);
            float l = C.top_cut[0], r = C.w - C.top_cut[1];
            Piece("band-left", 0f, topH - 1f, l, bandH + 2f, P);
            Piece("band-mid", l - 1f, topH - 1f, w - l - r + 2f, bandH + 2f, P);
            Piece("band-right", w - r, topH - 1f, r, bandH + 2f, P);
            Row("bot", topH + bandH, botH, C.bot_cut, P);

            float cx = tail == Tail.Left ? w * .52f : w * .48f;
            var tp = P(tail == Tail.Left ? w - cx : cx, (topH + bandH + botH) * .47f) * scale;
            if (tail == Tail.Left) tp.x = -tp.x;
            label.transform.localPosition = tp;
            label.rectTransform.sizeDelta = new Vector2(w * .8f / PPU * scale, h * .7f / PPU * scale);
            if (icon) AddIcon(tp, label.rectTransform.sizeDelta);
            Apply();
        }

        void AddIcon(Vector2 center, Vector2 box)
        {
            var sr = new GameObject("Icon").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(pivot, false);
            var pic = Trim(icon);
            sr.sprite = pic;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder + 30;
            Vector2 size = pic.bounds.size;
            float s = size.x > 0f && size.y > 0f ? Mathf.Min(box.x / size.x, box.y / size.y) * iconFill : 1f;
            sr.transform.localScale = new Vector3(s, s, 1f);
            sr.transform.localPosition = (Vector3)(center - (Vector2)pic.bounds.center * s);
        }

        /// <summary>
        /// Same picture without its transparent margin, so a small drawing on a big canvas still fills the cloud.
        /// Reads the alpha back through the GPU, so it also works for textures that are not CPU-readable.
        /// </summary>
        static Sprite Trim(Sprite source)
        {
            if (trimmed.TryGetValue(source, out var cached) && cached) return cached;
            var tex = source.texture;
            var r = source.textureRect;
            int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y), rw = Mathf.RoundToInt(r.width), rh = Mathf.RoundToInt(r.height);
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var read = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
            Color32[] px;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(x0, y0, rw, rh), 0, 0, false);
                px = read.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Destroy(read);
            }
            int minX = rw, minY = rh, maxX = -1, maxY = -1;
            for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                    if (px[y * rw + x].a > 8)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            var result = maxX < minX ? source : Sprite.Create(tex,
                new Rect(x0 + minX, y0 + minY, maxX - minX + 1, maxY - minY + 1),
                new Vector2(.5f, .5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            trimmed[source] = result;
            return result;
        }

        /// <summary>bottom outline row of the assembled cloud at column X (same tiling as Row)</summary>
        float BottomAt(int X, float bandH)
        {
            var C = meta.cloud;
            float L = C.bot_cut[0], R = C.w - C.bot_cut[1], M = C.bot_cut[1] - C.bot_cut[0];
            int v;
            if (X < L) v = At(meta.prof_bot_left, X);
            else if (X >= w - R) v = At(meta.prof_bot_right, Mathf.FloorToInt(X - (w - R)));
            else
            {
                float mid = w - L - R; int n = Mathf.Max(1, Mathf.RoundToInt(mid / M));
                int i = Mathf.Min(n - 1, Mathf.FloorToInt((X - L) / (mid / n)));
                float a = Mathf.Round(L + mid * i / n), b = Mathf.Round(L + mid * (i + 1) / n);
                v = At(meta.prof_bot_mid, Mathf.Min((int)M - 1, Mathf.FloorToInt((X - a) * M / (b - a + 1f))));
            }
            return v < 0 ? -1f : C.split + bandH + v;
        }
        static int At(int[] a, int i) => a == null || i < 0 || i >= a.Length ? -1 : a[i];

        /// <summary>y of the big tail circle so it sits right against the cloud's bottom edge</summary>
        float BigTailY(float bx, float bandH)
        {
            float best = float.MinValue; var top = meta.prof_tail_big_top;
            for (int c = 0; top != null && c < top.Length; c++)
            {
                if (top[c] < 0) continue;
                float cb = BottomAt((int)bx + c, bandH);
                if (cb >= 0f) best = Mathf.Max(best, cb - top[c] - Tuck);
            }
            return best > float.MinValue ? best : h - (meta.cloud.h - meta.tail_big[1]);
        }

        void Row(string half, float y, float rowH, float[] cut, Func<float, float, Vector2> P)
        {
            var C = meta.cloud;
            float L = cut[0], R = C.w - cut[1], M = cut[1] - cut[0];
            float mid = w - L - R;
            int n = Mathf.Max(1, Mathf.RoundToInt(mid / M));
            Piece(half + "-left", 0f, y, L, rowH, P);
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Round(L + mid * i / n), b = Mathf.Round(L + mid * (i + 1) / n);
                Piece(half + "-mid", a - .5f, y, b - a + 1f, rowH, P);   // tiny overlap, no hairline
            }
            Piece(half + "-right", w - R, y, R, rowH, P);
        }

        void Piece(string name, float x, float y, float pw, float ph, Func<float, float, Vector2> P)
        {
            var sp = S(name);
            if (sp == null) { Debug.LogWarning("ThoughtBubble: missing sprite " + name); return; }
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(art, false);
            sr.sprite = sp;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder + order++;
            sr.transform.localPosition = P(x + pw / 2f, y + ph / 2f);
            sr.transform.localScale = new Vector3(pw / sp.rect.width, ph / sp.rect.height, 1f);
        }

        void LateUpdate()
        {
            time += Time.deltaTime;
            if (follow) transform.position = follow.position + followOffset;
            if (Visible && autoHideAfter > 0f && time - shownAt > autoHideAfter) Hide();
            Apply();
        }

        void Apply()
        {
            if (!pivot) return;
            float s;
            if (Visible)
            {
                float a = time - shownAt, u = Mathf.Clamp01(a / .38f);
                s = Back(u);
            }
            else
            {
                float a = time - hideAt, u = Mathf.Clamp01(a / .2f);
                s = 1f - u * u;
                if (u >= 1f && pivot.gameObject.activeSelf) { pivot.gameObject.SetActive(false); Hidden?.Invoke(); }
            }
            pivot.localScale = new Vector3(s, s, 1f);
            float b = Visible ? Mathf.Sin(time * 2.6f) * bob : 0f;
            pivot.localPosition = new Vector3(0f, b, 0f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, Visible ? Mathf.Sin(time * 1.9f + .7f) * 1.2f * Mathf.Clamp01(bob * 20f) : 0f);
        }

        static float Back(float u) { const float c1 = 1.9f, c3 = c1 + 1f; float v = u - 1f; return 1f + c3 * v * v * v + c1 * v * v; }
    }
}
