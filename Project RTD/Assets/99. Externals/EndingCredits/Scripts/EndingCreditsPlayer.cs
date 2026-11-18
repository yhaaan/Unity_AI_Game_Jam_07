using System;
using ChimeraGroove.Prologue;
using TMPro;
using UnityEngine;

namespace ChimeraGroove.Ending
{
    /// <summary>
    /// Ending-credit roll: 6 cards (1920×1080 layout), each with 1–2 animated comic panels on the left
    /// and names on the right. Panels are PrologueCut instances reading Resources/EndingCredits/&lt;cut&gt;.
    /// Names come from Resources/EndingCredits/credits.json (edit that file).
    /// Drop on an empty GameObject in a scene with an orthographic camera and press Play.
    /// </summary>
    public class EndingCreditsPlayer : MonoBehaviour
    {
        public const string Root = "EndingCredits";
        static readonly string[] CutNames = { "01-breakout", "02-the-look", "03-team-up", "04-dust-cloud", "05-whose-feet",
            "06-left-foot", "07-hay-crash", "08-duck-tracks", "09-sneak-away", "10-home", "11-goodnight" };

        public Camera viewCamera;
        public bool handleInput = true;
        public bool autoAdvance = true;
        [Tooltip("seconds a card stays after its last panel finished its intro")]
        public float holdSeconds = 5.5f;
        public bool loopAtEnd = false;
        public TMP_FontAsset font;
        public Color background = new Color32(0xfd, 0xf6, 0xe8, 0xff);
        public Color ink = new Color32(0x1d, 0x1b, 0x26, 0xff);
        public Color titleColor = new Color32(0xe8, 0x57, 0x4a, 0xff);
        public Color roleColor = new Color32(0x7c, 0x73, 0x89, 0xff);

        public event Action<int> CardShown;
        public event Action Finished;
        public int Current { get; private set; }

        const float PPU = PrologueCut.PPU, W = 1920f, H = 1080f;
        const float PanelIn = .55f, PanelGap = .45f, TextAt = .5f, Swap = .7f;

#pragma warning disable 0649 // filled by JsonUtility
        // ---------------- json ----------------
        [Serializable] class Shot { public int shot; public int[] origin_px; public int[] render_size_px; }
        [Serializable] class Layout { public string layout; public Shot[] shots; public int[] credit_area_px; }
        [Serializable] class LayoutList { public Layout[] cards; }
        [Serializable] class Line { public string role, name; }
        [Serializable] class CardText { public string title; public Line[] lines; }
        [Serializable] class Credits { public CardText[] cards; }

#pragma warning restore 0649
        class Panel { public Transform root; public PrologueCut cut; public Vector2 home; public float k; }
        class Card { public Transform root; public Panel[] panels; public TextMeshPro[] texts; public float[] textY; public float len; }

        Card[] cards;
        Transform stage;
        float t, swapU = -1f, tFrom;
        int from;
        bool ended;
        static Sprite square;

        void Start()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            Build();
            Show(0);
        }

        static Sprite Square
        {
            get
            {
                if (square != null) return square;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[16]; for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px); tex.Apply();
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
                return square;
            }
        }

        static Vector2 Px(float x, float y) => new Vector2((x - W / 2f) / PPU, -(y - H / 2f) / PPU);

        static SpriteRenderer Rect(Transform parent, string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.sprite = Square; sr.color = color; sr.sortingOrder = order;
            return sr;
        }

        void Build()
        {
            var layoutText = Resources.Load<TextAsset>(Root + "/layout");
            var creditText = Resources.Load<TextAsset>(Root + "/credits");
            if (layoutText == null) { Debug.LogError("EndingCredits: missing Resources/EndingCredits/layout.json"); return; }
            var layout = JsonUtility.FromJson<LayoutList>("{\"cards\":" + layoutText.text + "}").cards;
            var credits = creditText != null ? JsonUtility.FromJson<Credits>(creditText.text) : new Credits();
            if (font == null) font = TMP_Settings.defaultFontAsset;

            stage = new GameObject("Stage").transform;
            stage.SetParent(transform, false);
            Rect(stage, "background", Vector2.zero, new Vector2(W * 3f / PPU, H * 3f / PPU), background, -10);

            cards = new Card[layout.Length];
            int panelNo = 0;
            for (int c = 0; c < layout.Length; c++)
            {
                var L = layout[c];
                var card = new Card { root = new GameObject("Card " + (c + 1)).transform, panels = new Panel[L.shots.Length] };
                card.root.SetParent(stage, false);
                float longestIntro = 0f;
                for (int j = 0; j < L.shots.Length; j++, panelNo++)
                {
                    var s = L.shots[j];
                    float x = s.origin_px[0], y = s.origin_px[1], w = s.render_size_px[0], h = s.render_size_px[1];
                    int baseOrder = (panelNo + 1) * 1000;
                    var root = new GameObject("Panel " + s.shot).transform;
                    root.SetParent(card.root, false);
                    var home = Px(x + w / 2f, y + h / 2f);
                    root.localPosition = home;

                    var cutGo = new GameObject(CutNames[s.shot - 1]);
                    cutGo.transform.SetParent(root, false);
                    var cut = cutGo.AddComponent<PrologueCut>();
                    cut.cutName = CutNames[s.shot - 1];
                    cut.resourcesRoot = Root;
                    cut.playOnStart = false;
                    cut.sortingOrderBase = baseOrder;
                    cut.Build();
                    float k = w / cut.SizePx.x; // cut px → panel px
                    cutGo.transform.localScale = new Vector3(k, k, 1f);
                    longestIntro = Mathf.Max(longestIntro, cut.Intro + j * PanelGap);

                    float uw = w / PPU, uh = h / PPU, m = 10f / PPU, b = 6f / PPU;
                    var maskGo = new GameObject("mask");
                    maskGo.transform.SetParent(root, false);
                    maskGo.transform.localScale = new Vector3(uw, uh, 1f);
                    var mask = maskGo.AddComponent<SpriteMask>();
                    mask.sprite = Square;
                    mask.isCustomRangeActive = true;
                    mask.backSortingOrder = baseOrder - 1;
                    mask.frontSortingOrder = baseOrder + 899;
                    cut.SetMaskInteraction(SpriteMaskInteraction.VisibleInsideMask);

                    Rect(root, "shadow", new Vector2(.1f, -.14f), new Vector2(uw, uh), new Color(ink.r, ink.g, ink.b, .18f), baseOrder - 3);
                    Rect(root, "paper", Vector2.zero, new Vector2(uw + 2 * m, uh + 2 * m), Color.white, baseOrder - 2);
                    Rect(root, "ink-top", new Vector2(0, uh / 2), new Vector2(uw + b, b), ink, baseOrder + 950);
                    Rect(root, "ink-bottom", new Vector2(0, -uh / 2), new Vector2(uw + b, b), ink, baseOrder + 950);
                    Rect(root, "ink-left", new Vector2(-uw / 2, 0), new Vector2(b, uh + b), ink, baseOrder + 950);
                    Rect(root, "ink-right", new Vector2(uw / 2, 0), new Vector2(b, uh + b), ink, baseOrder + 950);
                    card.panels[j] = new Panel { root = root, cut = cut, home = home, k = k };
                }
                card.len = longestIntro + holdSeconds;
                BuildText(card, L.credit_area_px, credits.cards != null && c < credits.cards.Length ? credits.cards[c] : null);
                card.root.gameObject.SetActive(false);
                cards[c] = card;
            }
        }

        void BuildText(Card card, int[] area, CardText txt)
        {
            if (txt == null) { card.texts = new TextMeshPro[0]; card.textY = new float[0]; return; }
            float cx = (area[0] + area[2]) / 2f, maxW = (area[2] - area[0] - 90f) / PPU;
            var rows = new System.Collections.Generic.List<(string s, float size, Color col, FontStyles st, float h)>();
            rows.Add((txt.title, 64f, titleColor, FontStyles.Bold, 110f));
            if (txt.lines != null)
                foreach (var l in txt.lines)
                {
                    rows.Add(((l.role ?? "").ToUpperInvariant(), 30f, roleColor, FontStyles.Bold, 52f));
                    rows.Add((l.name ?? "", 52f, ink, FontStyles.Bold, 92f));
                }
            float total = 0f; foreach (var r in rows) total += r.h;
            float y = H / 2f - total / 2f;
            card.texts = new TextMeshPro[rows.Count];
            card.textY = new float[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var tm = new GameObject("text " + i).AddComponent<TextMeshPro>();
                tm.transform.SetParent(card.root, false);
                if (font != null) tm.font = font;
                tm.text = r.s;
                tm.fontStyle = r.st;
                tm.color = r.col;
                tm.alignment = TextAlignmentOptions.Center;
                tm.textWrappingMode = TextWrappingModes.NoWrap;
                tm.enableAutoSizing = true;           // long titles shrink to fit the credit column
                tm.fontSizeMax = r.size / 10f;
                tm.fontSizeMin = r.size / 30f;
                tm.rectTransform.sizeDelta = new Vector2(maxW, r.h / PPU);
                tm.sortingOrder = 30000;
                card.textY[i] = Px(cx, y + r.h / 2f).y;
                tm.transform.localPosition = new Vector3(Px(cx, 0).x, card.textY[i], 0f);
                card.texts[i] = tm;
                y += r.h;
            }
        }

        // ---------------- control ----------------
        public void Show(int i)
        {
            if (cards == null) return;
            for (int c = 0; c < cards.Length; c++) cards[c].root.gameObject.SetActive(c == i);
            Current = i; t = 0f; swapU = -1f; ended = false;
            Pose(cards[i], 0f, 0f, 1f);
            CardShown?.Invoke(i);
        }

        public void Replay() => t = 0f;

        public void Next()
        {
            if (cards == null || swapU >= 0f) return;
            if (Current < cards.Length - 1) GoTo(Current + 1);
            else if (loopAtEnd) GoTo(0);
            else if (!ended) { ended = true; Finished?.Invoke(); }
        }

        public void Previous() { if (Current > 0) GoTo(Current - 1); }

        public void GoTo(int i)
        {
            if (cards == null || swapU >= 0f || i < 0 || i >= cards.Length || i == Current) return;
            from = Current; tFrom = t;
            Current = i; t = 0f; swapU = 0f; ended = false;
            cards[i].root.gameObject.SetActive(true);
            CardShown?.Invoke(i);
        }

        void Update()
        {
            if (cards == null) return;
            if (handleInput && PressedNext()) Next();
            Fit();
            float dt = Time.deltaTime;
            if (swapU >= 0f)
            {
                swapU += dt / Swap; tFrom += dt;
                float u = Mathf.Min(1f, swapU), e = PrologueMath.Ease("io", u);
                int dir = Current > from ? 1 : -1;
                Pose(cards[from], tFrom, -dir * e * W / PPU, 1f - u);
                t = Mathf.Max(0f, u - .35f) * Swap;
                Pose(cards[Current], t, dir * (1f - e) * W / PPU * .25f, u);
                if (u >= 1f) { cards[from].root.gameObject.SetActive(false); swapU = -1f; }
            }
            else
            {
                t += dt;
                Pose(cards[Current], t, 0f, 1f);
                if (autoAdvance && t > cards[Current].len) Next();
            }
        }

        /// <summary>Scales the 1920×1080 stage to fit the camera view.</summary>
        void Fit()
        {
            if (viewCamera == null) return;
            float vh = viewCamera.orthographicSize * 2f, vw = vh * viewCamera.aspect;
            float s = Mathf.Min(vw / (W / PPU), vh / (H / PPU));
            var c = viewCamera.transform.position;
            stage.position = new Vector3(c.x, c.y, transform.position.z);
            stage.localScale = new Vector3(s, s, 1f);
        }

        void Pose(Card card, float time, float dx, float alpha)
        {
            card.root.localPosition = new Vector3(dx, 0f, 0f);
            for (int j = 0; j < card.panels.Length; j++)
            {
                var p = card.panels[j];
                float pt = time - j * PanelGap, k = pt / PanelIn;
                bool on = k > 0f;
                if (p.root.gameObject.activeSelf != on) p.root.gameObject.SetActive(on);
                if (!on) continue;
                float e = PrologueMath.Ease("back", Mathf.Min(1f, k));
                p.root.localPosition = p.home + new Vector2(0f, (1f - e) * 1.4f);
                p.root.localRotation = Quaternion.Euler(0f, 0f, (1f - e) * .06f * Mathf.Rad2Deg);
                p.cut.playing = false;
                p.cut.time = pt;
                p.cut.Apply();
            }
            for (int i = 0; i < card.texts.Length; i++)
            {
                float k = Mathf.Clamp01((time - TextAt - i * .12f) / .45f), e = PrologueMath.Ease("out", k);
                var tm = card.texts[i];
                var col = tm.color; col.a = e * alpha; tm.color = col;
                var lp = tm.transform.localPosition;
                tm.transform.localPosition = new Vector3(lp.x, card.textY[i] - (1f - e) * .4f, 0f);
            }
        }

        static bool PressedNext()
        {
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) return true;
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) return true;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.RightArrow);
#else
            return false;
#endif
        }
    }
}
