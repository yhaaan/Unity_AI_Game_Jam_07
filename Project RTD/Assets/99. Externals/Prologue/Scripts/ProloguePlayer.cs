using System;
using UnityEngine;

namespace ChimeraGroove.Prologue
{
    /// <summary>
    /// Shows the prologue one cut at a time. Tap / click / Space / Enter / → goes to the next cut.
    /// Drop it on an empty GameObject in a scene with an orthographic camera and press Play.
    /// Cuts 1–4 slide in like comic panels; cut 5 arrives through the full-screen dust wipe.
    /// </summary>
    public class ProloguePlayer : MonoBehaviour
    {
        public string[] cuts = { "cut1-breakout", "cut2-the-look", "cut3-team-up", "cut4-dust-cloud", "cut5-chimera-roll" };
        [Tooltip("true = enter this cut through the full-screen dust wipe instead of a panel slide")]
        public bool[] dustBefore = { false, false, false, false, true };

        public Camera viewCamera;
        [Range(.5f, 1f)] public float fill = .92f;
        public bool comicFrame = true;
        public Color paper = new Color32(0xfb, 0xf3, 0xe2, 0xff);
        public Color ink = new Color32(0x1d, 0x1b, 0x26, 0xff);
        public bool handleInput = true;
        public bool autoAdvance = false;
        public float autoHoldSeconds = 2.2f;
        public bool loopAtEnd = false;
        public float slideSeconds = .6f;

        /// <summary>Fires with the cut index once a cut is fully on screen.</summary>
        public event Action<int> CutShown;
        /// <summary>Fires when Next() is called on the last cut (and loopAtEnd is off).</summary>
        public event Action Finished;

        public int Current { get; private set; }
        public bool Busy => mode != Mode.Idle;
        public PrologueCut CurrentCut => panels != null ? panels[Current].cut : null;

        class Panel { public Transform root; public PrologueCut cut; public float w, h; }
        Panel[] panels;
        PrologueDust dust;
        enum Mode { Idle, Slide, Dust }
        Mode mode;
        int from, to, dir;
        float tt;
        bool switched;
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
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4); // 1 × 1 unit
                return square;
            }
        }

        Vector2 View()
        {
            float h = viewCamera.orthographicSize * 2f;
            return new Vector2(h * viewCamera.aspect, h);
        }

        void Build()
        {
            panels = new Panel[cuts.Length];
            for (int i = 0; i < cuts.Length; i++)
            {
                int baseOrder = (i + 1) * 1000;
                var root = new GameObject("Panel " + cuts[i]).transform;
                root.SetParent(transform, false);
                var cutGo = new GameObject(cuts[i]);
                cutGo.transform.SetParent(root, false);
                var cut = cutGo.AddComponent<PrologueCut>();
                cut.cutName = cuts[i];
                cut.playOnStart = false;
                cut.sortingOrderBase = baseOrder;
                cut.Build();
                float w = cut.SizePx.x / PrologueCut.PPU, h = cut.SizePx.y / PrologueCut.PPU;

                // clip the cut to its own rectangle (parts slide in from outside it)
                var maskGo = new GameObject("mask");
                maskGo.transform.SetParent(root, false);
                maskGo.transform.localScale = new Vector3(w, h, 1f);
                var mask = maskGo.AddComponent<SpriteMask>();
                mask.sprite = Square;
                mask.isCustomRangeActive = true;
                mask.backSortingOrder = baseOrder - 1;
                mask.frontSortingOrder = baseOrder + 899;
                cut.SetMaskInteraction(SpriteMaskInteraction.VisibleInsideMask);

                if (comicFrame)
                {
                    float m = .1f * w / 19.2f * 1.4f, b = .06f * w / 19.2f * 1.4f;
                    Rect(root, "shadow", new Vector2(.08f, -.1f), new Vector2(w + 2 * m, h + 2 * m), new Color(0, 0, 0, .35f), baseOrder - 3);
                    Rect(root, "paper", Vector2.zero, new Vector2(w + 2 * m, h + 2 * m), paper, baseOrder - 2);
                    Rect(root, "ink-top", new Vector2(0, h / 2), new Vector2(w + b, b), ink, baseOrder + 950);
                    Rect(root, "ink-bottom", new Vector2(0, -h / 2), new Vector2(w + b, b), ink, baseOrder + 950);
                    Rect(root, "ink-left", new Vector2(-w / 2, 0), new Vector2(b, h + b), ink, baseOrder + 950);
                    Rect(root, "ink-right", new Vector2(w / 2, 0), new Vector2(b, h + b), ink, baseOrder + 950);
                }
                root.gameObject.SetActive(false);
                panels[i] = new Panel { root = root, cut = cut, w = w, h = h };
            }
            var dustGo = new GameObject("Dust Transition");
            dustGo.transform.SetParent(transform, false);
            dust = dustGo.AddComponent<PrologueDust>();
            dust.Show(false);
        }

        static void Rect(Transform parent, string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.sprite = Square; sr.color = color; sr.sortingOrder = order;
        }

        float FitScale(Panel p)
        {
            var v = View();
            return Mathf.Min(v.x * fill / p.w, (v.y * fill) / p.h);
        }

        void Place(Panel p, float ox, float oy, float rotDeg)
        {
            float k = FitScale(p);
            var c = viewCamera.transform.position;
            p.root.position = new Vector3(c.x + ox, c.y + oy, transform.position.z);
            p.root.localRotation = Quaternion.Euler(0, 0, rotDeg);
            p.root.localScale = new Vector3(k, k, 1f);
        }

        /// <summary>Jumps straight to a cut (no transition) and restarts its animation.</summary>
        public void Show(int i)
        {
            for (int j = 0; j < panels.Length; j++) panels[j].root.gameObject.SetActive(j == i);
            Current = i;
            panels[i].cut.Play();
            Place(panels[i], 0, 0, 0);
            mode = Mode.Idle;
            CutShown?.Invoke(i);
        }

        public void Replay() => panels[Current].cut.Play();

        public void Next()
        {
            if (Busy) return;
            if (Current < panels.Length - 1) GoTo(Current + 1);
            else if (loopAtEnd) GoTo(0);
            else Finished?.Invoke();
        }

        public void Previous() { if (!Busy && Current > 0) GoTo(Current - 1); }

        public void GoTo(int i)
        {
            if (Busy || i == Current || i < 0 || i >= panels.Length) return;
            from = Current; to = i; dir = i > Current ? 1 : -1; tt = 0f; switched = false;
            bool useDust = i == Current + 1 && i < dustBefore.Length && dustBefore[i];
            mode = useDust ? Mode.Dust : Mode.Slide;
            if (mode == Mode.Slide)
            {
                panels[to].root.gameObject.SetActive(true);
                panels[to].cut.Play();
            }
            else dust.Show(true);
        }

        void Update()
        {
            if (panels == null) return;
            if (handleInput && PressedNext()) Next();
            var v = View();

            if (mode == Mode.Slide)
            {
                tt += Time.deltaTime;
                float u = Mathf.Min(1f, tt / slideSeconds);
                float o = PrologueMath.Ease("in", Mathf.Min(1f, u * 1.6f));
                Place(panels[from], -dir * o * v.x * 1.15f, -o * .3f, dir * o * .12f * Mathf.Rad2Deg);
                float inn = PrologueMath.Ease("back", u);
                Place(panels[to], dir * (1f - inn) * v.x * 1.1f, 0f, -dir * (1f - inn) * .1f * Mathf.Rad2Deg);
                if (u >= 1f)
                {
                    panels[from].root.gameObject.SetActive(false);
                    Current = to; mode = Mode.Idle;
                    CutShown?.Invoke(Current);
                }
            }
            else if (mode == Mode.Dust)
            {
                tt += Time.deltaTime;
                float p = Mathf.Min(1f, tt / PrologueDust.Duration);
                if (!switched && p >= PrologueDust.SwitchAt)
                {
                    switched = true;
                    panels[from].root.gameObject.SetActive(false);
                    panels[to].root.gameObject.SetActive(true);
                    panels[to].cut.Play();
                    Place(panels[to], 0, 0, 0);
                    Current = to;
                }
                var c = viewCamera.transform.position;
                dust.transform.position = new Vector3(c.x, c.y, transform.position.z);
                dust.Evaluate(p, v.x, v.y);
                if (p >= 1f)
                {
                    dust.Show(false);
                    mode = Mode.Idle;
                    CutShown?.Invoke(Current);
                }
            }
            else
            {
                Place(panels[Current], 0, 0, 0); // follows screen size changes
                var cut = panels[Current].cut;
                if (autoAdvance && cut.time > cut.Intro + autoHoldSeconds) Next();
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
