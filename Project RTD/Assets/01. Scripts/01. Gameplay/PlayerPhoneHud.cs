using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // Bottom-right "phone" that shows the current build: heavy toggle, power and jump pips, swim / fly abilities.
    // Builds its own UI under the stage's "Stage HUD" canvas, so no scene edits are needed.
    [DisallowMultipleComponent]
    public sealed class PlayerPhoneHud : MonoBehaviour
    {
        const string CanvasName = "Stage HUD";
        const float W = 250f, H = 360f;

        static readonly Color Outline = Hex(0x0B1A3A), Rim = Hex(0x2F9FCF), Shell = Hex(0xA68DD2),
            Screen = Hex(0x0A1A40), Glow = Hex(0x3DEBF7), RowLine = Hex(0x4DA6E6), Yellow = Hex(0xFFC814),
            Teal = Hex(0x07535C), Ice = Hex(0xBDF6FF), Mint = Hex(0x6FD3AE);
        const float Tilt = -4f;

        static Sprite round, circle;

        PlayerPartLoadout loadout;
        RectTransform root, body, knob, shadow;
        Image screenGlow, track, rollBand;
        static Texture2D scanlines;
        RawImage scanImage;
        CanvasGroup screenFlicker;
        readonly Image[] powerPips = new Image[3], jumpPips = new Image[3];
        Image swimButton, wingButton, swimIcon, wingIcon;
        int power = -1, jump = -1, heavy = -1, swim = -1, wing = -1;
        Coroutine knobMove;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (_, _) => Attach();
            Attach();
        }

        // Only stages have a "Stage HUD". The phone gets its own world-space canvas on the camera so it is drawn
        // with the scene and therefore goes through the Curved World pass and the CRT post-processing.
        static void Attach()
        {
            var cam = Camera.main;
            if (!cam || FindFirstObjectByType<PlayerPhoneHud>(FindObjectsInactive.Include)) return;
            bool stage = false;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) stage |= canvas.name == CanvasName;
            if (!stage) return;

            var host = new GameObject("Phone HUD Canvas", typeof(RectTransform));
            host.layer = 0;
            host.transform.SetParent(cam.transform, false);
            var layers = SortingLayer.layers;
            var phoneCanvas = host.AddComponent<Canvas>();
            phoneCanvas.renderMode = RenderMode.WorldSpace;
            phoneCanvas.worldCamera = cam;
            phoneCanvas.sortingLayerID = layers[layers.Length - 1].id;
            phoneCanvas.sortingOrder = 32000;
            host.AddComponent<FollowCamera>().Init(cam);
            var phone = new GameObject("Player Phone", typeof(RectTransform));
            phone.layer = 0;
            phone.transform.SetParent(host.transform, false);
            phone.AddComponent<PlayerPhoneHud>();
        }

        // Keeps the world-space canvas exactly covering the orthographic view at 1080 units tall.
        sealed class FollowCamera : MonoBehaviour
        {
            const float RefHeight = 1080f;
            Camera cam;
            RectTransform rect;

            public void Init(Camera camera) { cam = camera; rect = (RectTransform)transform; LateUpdate(); }

            void LateUpdate()
            {
                if (!cam) return;
                float worldH = cam.orthographic ? cam.orthographicSize * 2f : 2f * 5f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                rect.sizeDelta = new Vector2(RefHeight * cam.aspect, RefHeight);
                rect.localScale = Vector3.one * (worldH / RefHeight);
                rect.localPosition = new Vector3(0f, 0f, 5f);
                rect.localRotation = Quaternion.identity;
            }
        }

        void Awake()
        {
            root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = new Vector2(-100f, 120f);
            root.sizeDelta = new Vector2(W, H);
            root.localScale = Vector3.one * 0.88f;
            gameObject.AddComponent<CanvasGroup>().alpha = 0.94f;
            Build();
        }

        void OnEnable()
        {
            if (!loadout) loadout = FindFirstObjectByType<PlayerPartLoadout>();
            if (loadout) { loadout.Changed += Show; Show(loadout.Current); }
            StartCoroutine(SlideIn());
        }

        void OnDisable()
        {
            if (loadout) loadout.Changed -= Show;
        }

        void Update()
        {
            float t = Time.unscaledTime;
            body.localRotation = Quaternion.Euler(0f, 0f, Tilt + Mathf.Sin(t * 1.3f) * 1f);
            body.anchoredPosition = new Vector2(0f, Mathf.Sin(t * 0.9f) * 3f);
            shadow.localRotation = body.localRotation;
            shadow.anchoredPosition = new Vector2(10f, -10f - body.anchoredPosition.y * 0.5f);
            screenGlow.color = WithAlpha(Glow, 0.7f + 0.15f * Mathf.Sin(t * 2.4f));

            float faceH = ((RectTransform)scanImage.transform).rect.height;
            scanImage.uvRect = new Rect(0f, -t * 0.6f, 1f, faceH / 4f);
            var band = (RectTransform)rollBand.transform;
            float span = faceH + 60f;
            band.anchoredPosition = new Vector2(0f, faceH + 30f - Mathf.Repeat(t * 70f, span));
            screenFlicker.alpha = 0.93f + 0.05f * Mathf.PerlinNoise(t * 9f, 0.3f) + (Random.value < 0.01f ? -0.12f : 0f);
        }

        // ---------- state ----------

        void Show(PlayerPartBuild build)
        {
            int p = build?.Power ?? 0, j = build?.Jump ?? 0;
            int h = build != null && build.Weight >= 3 ? 1 : 0;
            int s = build?.HasEffect(PartEffect.Float) == true ? 1 : 0;
            int f = build?.HasEffect(PartEffect.Glide) == true ? 1 : 0;
            bool first = power < 0;
            bool changed = !first && (p != power || j != jump || h != heavy || s != swim || f != wing);

            if (p != power) SetPips(powerPips, p, first ? 0 : power);
            if (j != jump) SetPips(jumpPips, j, first ? 0 : jump);
            if (h != heavy) SetHeavy(h == 1, first);
            if (s != swim) SetAbility(swimButton, swimIcon, s == 1, first);
            if (f != wing) SetAbility(wingButton, wingIcon, f == 1, first);
            power = p; jump = j; heavy = h; swim = s; wing = f;
            if (changed) StartCoroutine(Wobble());
        }

        void SetPips(Image[] pips, int level, int before)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                bool on = i < level;
                pips[i].transform.GetChild(0).gameObject.SetActive(on);
                pips[i].transform.GetChild(1).gameObject.SetActive(!on);
                if (on && i >= before) StartCoroutine(Pop(pips[i].transform, 0.08f * (i - before)));
                else if (!on && i < before) StartCoroutine(Pop(pips[i].transform, 0f, 0.6f));
            }
        }

        void SetHeavy(bool on, bool instant)
        {
            float x = on ? 26f : -26f;
            track.color = on ? Teal : Screen;
            if (knobMove != null) StopCoroutine(knobMove);
            if (instant) knob.anchoredPosition = new Vector2(x, 0f);
            else knobMove = StartCoroutine(SlideKnob(x));
        }

        void SetAbility(Image button, Image icon, bool on, bool instant)
        {
            button.color = on ? Glow : WithAlpha(RowLine, 0.35f);
            button.transform.GetChild(0).GetComponent<Image>().color = on ? Teal : Screen;
            icon.color = on ? Ice : WithAlpha(Ice, 0.18f);
            if (!instant && on) StartCoroutine(Pop(button.transform, 0f, 1f, 1.25f));
        }

        // ---------- animation ----------

        IEnumerator SlideIn()
        {
            var to = new Vector2(-100f, 120f);
            var from = to + Vector2.down * (H + 60f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.65f)
            {
                root.anchoredPosition = Vector2.LerpUnclamped(from, to, BackOut(t));
                yield return null;
            }
            root.anchoredPosition = to;
        }

        IEnumerator SlideKnob(float x)
        {
            float from = knob.anchoredPosition.x;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                knob.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, x, BackOut(t)), 0f);
                knob.localScale = new Vector3(1f + 0.25f * Mathf.Sin(t * Mathf.PI), 1f - 0.12f * Mathf.Sin(t * Mathf.PI), 1f);
                yield return null;
            }
            knob.anchoredPosition = new Vector2(x, 0f);
            knob.localScale = Vector3.one;
        }

        static IEnumerator Pop(Transform target, float delay, float from = 0f, float peak = 1.35f)
        {
            target.localScale = Vector3.one * from;
            for (float d = 0f; d < delay; d += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                float s = t < 0.5f ? Mathf.Lerp(from, peak, t * 2f) : Mathf.Lerp(peak, 1f, (t - 0.5f) * 2f);
                target.localScale = Vector3.one * s;
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        IEnumerator Wobble()
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
            {
                float k = Mathf.Sin(t * Mathf.PI * 5f) * (1f - t);
                body.localScale = new Vector3(1f + 0.06f * k, 1f - 0.06f * k, 1f);
                yield return null;
            }
            body.localScale = Vector3.one;
        }

        static float BackOut(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * (2.7f * t + 1.7f);
        }

        // ---------- construction ----------

        void Build()
        {
            round ??= MakeRound(64, 22, false);
            circle ??= MakeRound(64, 32, true);

            // Soft contact shadow so the phone sits on the scene instead of floating on top of it.
            shadow = Rect("Shadow", root, Vector2.zero, Vector2.zero, true);
            shadow.pivot = new Vector2(0.5f, 0f);
            for (int i = 0; i < 4; i++) Panel("Blur " + i, shadow, -6f + i * 4f, WithAlpha(Color.black, 0.14f), 56f - i * 4f);

            body = Rect("Body", root, Vector2.zero, Vector2.zero, true);
            body.pivot = new Vector2(0.5f, 0f);

            // Shell: dark outline → blue rim → dark line → lilac bezel.
            Panel("Outline", body, 0f, Outline, 48f);
            Panel("Rim", body, 5f, Rim, 44f);
            Panel("Rim Line", body, 13f, Outline, 38f);
            Panel("Bezel", body, 15f, Shell, 36f);
            Side("Button A", 0.72f, 30f);
            Side("Button B", 0.55f, 30f);

            var slot = Fixed("Speaker", body, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(46f, 10f), Outline, round);
            slot.pivot = new Vector2(0.5f, 0.5f);
            var home = Fixed("Home", body, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(54f, 26f), Outline, round);
            Fixed("Face", home, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 18f), Mint, round);

            // Screen with pulsing cyan border.
            var screen = Panel("Screen Outline", body, 0f, Outline, 26f);
            Inset(screen, 26f, 52f, 26f, 54f);
            screenGlow = Panel("Screen Glow", screen, 3f, Glow, 23f).GetComponent<Image>();
            var face = Panel("Screen", screen, 7f, Screen, 19f);

            const float rowH = 50f, gap = 6f;
            for (int i = 0; i < 4; i++)
            {
                var row = Panel("Row " + i, face, 0f, RowLine, 20f);
                row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(0.5f, 1f);
                row.offsetMin = new Vector2(8f, -(10f + i * (rowH + gap) + rowH));
                row.offsetMax = new Vector2(-8f, -(10f + i * (rowH + gap)));
                var inner = Panel("Inner", row, 2f, Screen, 18f);
                Icon(inner, new[] { "phone_weight", "phone_heart", "phone_jump", "phone_star" }[i]);
                switch (i)
                {
                    case 0: BuildToggle(inner); break;
                    case 1: BuildPips(inner, powerPips); break;
                    case 2: BuildPips(inner, jumpPips); break;
                    case 3:
                        wingButton = AbilityButton(inner, "Fly", -6f, "phone_wing", out wingIcon);
                        swimButton = AbilityButton(inner, "Swim", -64f, "phone_swim", out swimIcon);
                        break;
                }
            }

            // Extra CRT on the little screen: scanlines that crawl downward, a rolling bright band, and flicker.
            scanlines ??= MakeScanlines();
            var lines = Rect("Scanlines", face, Vector2.zero, Vector2.zero, true);
            scanImage = lines.gameObject.AddComponent<RawImage>();
            scanImage.texture = scanlines;
            scanImage.color = WithAlpha(Color.black, 0.32f);
            scanImage.raycastTarget = false;
            var band = Rect("Roll Band", face, Vector2.zero, Vector2.zero, false);
            band.anchorMin = new Vector2(0f, 0f); band.anchorMax = new Vector2(1f, 0f); band.pivot = new Vector2(0.5f, 0.5f);
            band.sizeDelta = new Vector2(0f, 26f);
            rollBand = band.gameObject.AddComponent<Image>();
            rollBand.color = WithAlpha(Ice, 0.07f);
            rollBand.raycastTarget = false;
            screenFlicker = face.gameObject.AddComponent<CanvasGroup>();
        }

        static Texture2D MakeScanlines()
        {
            var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 120), new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 0) });
            tex.Apply();
            return tex;
        }

        void Side(string name, float y, float h)
        {
            var b = Fixed(name, body, new Vector2(1f, y), new Vector2(4f, 0f), new Vector2(12f, h), Outline, round);
            b.SetAsFirstSibling();
            Fixed("Face", b, new Vector2(0.5f, 0.5f), new Vector2(-1f, 0f), new Vector2(7f, h - 5f), Shell, round);
        }

        void Icon(RectTransform row, string sprite)
        {
            var shadow = Fixed("Icon Shadow", row, new Vector2(0f, 0.5f), new Vector2(31f, -2f), new Vector2(40f, 40f), WithAlpha(Color.black, 0.55f), Load(sprite));
            shadow.GetComponent<Image>().preserveAspect = true;
            var icon = Fixed("Icon", row, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(40f, 40f), Yellow, Load(sprite));
            icon.GetComponent<Image>().preserveAspect = true;
        }

        void BuildToggle(RectTransform row)
        {
            var outline = Fixed("Toggle", row, new Vector2(1f, 0.5f), new Vector2(-50f, 0f), new Vector2(80f, 36f), Glow, round);
            track = Fixed("Track", outline, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 30f), Teal, round).GetComponent<Image>();
            knob = Fixed("Knob", outline, new Vector2(0.5f, 0.5f), new Vector2(-26f, 0f), new Vector2(34f, 34f), Outline, circle);
            Ball(knob, 30f);
        }

        void BuildPips(RectTransform row, Image[] pips)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = Fixed("Pip " + i, row, new Vector2(1f, 0.5f), new Vector2(-22f - (2 - i) * 32f, 0f), new Vector2(30f, 30f), Color.clear, circle);
                pips[i] = pip.GetComponent<Image>();
                var full = Fixed("Full", pip, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f), Outline, circle);
                Ball(full, 26f);
                var empty = Fixed("Empty", pip, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f), Glow, circle);
                Fixed("Hole", empty, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(19f, 19f), Screen, circle);
            }
        }

        static void Ball(RectTransform parent, float size)
        {
            Fixed("Ball", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), Yellow, circle);
            Fixed("Shine", parent, new Vector2(0.5f, 0.5f), new Vector2(-size * 0.17f, size * 0.17f), new Vector2(size * 0.28f, size * 0.2f), WithAlpha(Color.white, 0.9f), circle)
                .localRotation = Quaternion.Euler(0f, 0f, 35f);
        }

        // Returns the border image (the button root); child 0 is the fill.
        Image AbilityButton(RectTransform row, string name, float x, string sprite, out Image icon)
        {
            var border = Fixed(name, row, new Vector2(1f, 0.5f), new Vector2(x - 26f, 0f), new Vector2(54f, 38f), Glow, round);
            var fill = Fixed("Fill", border, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 32f), Teal, round);
            icon = Fixed("Icon", fill, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 28f), Ice, Load(sprite)).GetComponent<Image>();
            icon.preserveAspect = true;
            return border.GetComponent<Image>();
        }

        // ---------- helpers ----------

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, bool stretch)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            if (stretch) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = min; rect.offsetMax = max; }
            return rect;
        }

        static RectTransform Panel(string name, Transform parent, float inset, Color color, float radius)
        {
            var rect = Rect(name, parent, Vector2.one * inset, -Vector2.one * inset, true);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = round;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 22f / radius;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static void Inset(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        static RectTransform Fixed(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, Sprite sprite)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite == round) { image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 22f / Mathf.Min(size.y * 0.5f, 22f); }
            return rect;
        }

        static Sprite Load(string name) => Resources.Load<Sprite>("PhoneHud/" + name);

        static Sprite MakeRound(int size, int radius, bool full)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float r = full ? size * 0.5f : radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, size - r), cy = Mathf.Clamp(y + 0.5f, r, size - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(r - d + 0.5f)));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var border = full ? Vector4.zero : Vector4.one * radius;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        static Color Hex(int rgb) => new((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, 1f);
    }
}
