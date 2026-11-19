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
        const float Tilt = 4f; // top-left corner: lean in toward the screen centre
        static readonly Vector2 Home = new(150f, -120f); // from the top-left corner, clear of the curved edge

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
        readonly Image[] rowLines = new Image[4];
        readonly RectTransform[] rowInners = new RectTransform[4];
        Fire powerFire, jumpFire, swimFire, wingFire;
        static Sprite[] wideFlames, narrowFlames;
        static readonly Color FlameMid = Hex(0xFF8A1F);
        Coroutine knobMove;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (_, _) => Attach();
            Attach();
        }

        // Only stages have a "Stage HUD". The phone lives on the shared curved HUD canvas so it is drawn with the scene
        // and therefore goes through the Curved World pass and the CRT post-processing.
        static void Attach()
        {
            if (!IsStage() || FindFirstObjectByType<PlayerPhoneHud>(FindObjectsInactive.Include)) return;
            var host = CurvedCanvas();
            if (!host) return;
            var phone = new GameObject("Player Phone", typeof(RectTransform));
            phone.layer = 0;
            phone.transform.SetParent(host, false);
            phone.AddComponent<PlayerPhoneHud>();
        }

        internal static bool IsStage()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.name == CanvasName) return true;
            return false;
        }

        // A world-space canvas parented to the main camera, 1080 units tall, sorted above the stage sprites.
        // HUD on it is rendered with the scene, so it bends and gets the CRT look like the stage does.
        internal static RectTransform CurvedCanvas()
        {
            var cam = Camera.main;
            if (!cam) return null;
            var existing = cam.transform.Find("Curved HUD Canvas");
            if (existing) return (RectTransform)existing;

            var host = new GameObject("Curved HUD Canvas", typeof(RectTransform));
            host.layer = 0;
            host.transform.SetParent(cam.transform, false);
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            // Default layer, above everything on it (foreground props sit at ~45). The WaterOverlay layer is drawn
            // in its own earlier pass, so putting the HUD there would let foreground props cover it.
            canvas.sortingLayerName = "Default";
            canvas.sortingOrder = 32000;
            host.AddComponent<FollowCamera>().Init(cam);
            return (RectTransform)host.transform;
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

        // For the tutorial spotlight.
        internal RectTransform Body => body;
        internal RectTransform Row(int i) => (RectTransform)rowLines[i].transform;

        void Awake()
        {
            root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = Home;
            root.sizeDelta = new Vector2(W, H);
            root.localScale = Vector3.one * 1.1f;
            Build();
        }

        void OnEnable()
        {
            if (!loadout) loadout = FindFirstObjectByType<PlayerPartLoadout>();
            if (loadout) { loadout.Changed += Show; Show(loadout.Current); }
            StageRuntime.RanOutOfRolls += RollAlarm;
            StartCoroutine(SlideIn());
        }

        void RollAlarm() => rollAlarm = 1f;

        void OnDisable()
        {
            if (loadout) loadout.Changed -= Show;
            StageRuntime.RanOutOfRolls -= RollAlarm;
        }

        void Update()
        {
            float t = Time.unscaledTime;
            body.localRotation = Quaternion.Euler(0f, 0f, Tilt + Mathf.Sin(t * 1.3f) * 1f + danceTilt);
            body.anchoredPosition = new Vector2(danceX, Mathf.Sin(t * 0.9f) * 3f + danceY);
            shadow.localRotation = body.localRotation;
            shadow.anchoredPosition = new Vector2(10f, -10f - body.anchoredPosition.y * 0.5f);
            screenFlash = Mathf.MoveTowards(screenFlash, 0f, Time.unscaledDeltaTime * 4f);
            screenGlow.color = Color.Lerp(WithAlpha(Glow, 0.7f + 0.15f * Mathf.Sin(t * 2.4f)), Color.white, screenFlash);

            float faceH = ((RectTransform)scanImage.transform).rect.height;
            scanImage.uvRect = new Rect(0f, -t * 0.6f, 1f, faceH / 4f);
            var band = (RectTransform)rollBand.transform;
            float span = faceH + 60f;
            band.anchoredPosition = new Vector2(0f, faceH + 30f - Mathf.Repeat(t * 70f, span));
            screenFlicker.alpha = 0.97f + 0.03f * Mathf.PerlinNoise(t * 9f, 0.3f) + (Random.value < 0.006f ? -0.06f : 0f);

            // Rerolls left for the run, on a tag under the phone. Pops on every use; turns red and blinks when low.
            int rolls = StageRuntime.RollsLeft;
            if (rolls != shownRolls)
            {
                if (shownRolls != int.MinValue) rollPop = 1f;
                shownRolls = rolls;
                rollText.text = $"<size=62%>R</size> {rolls}";
            }
            rollPop = Mathf.MoveTowards(rollPop, 0f, Time.unscaledDeltaTime * 4f);
            rollAlarm = Mathf.MoveTowards(rollAlarm, 0f, Time.unscaledDeltaTime * 0.8f);
            bool low = rolls <= 10;
            bool blink = low && Mathf.Repeat(t * 3f, 1f) < 0.5f || Mathf.Repeat(rollAlarm * 8f, 1f) > 0.5f;
            rollPlate.color = blink ? Hex(0xE8401C) : Screen;
            rollText.color = low && !blink ? Hex(0xFF7A66) : Yellow;
            rollCounter.localScale = Vector3.one * (1f + 0.3f * rollPop * rollPop + 0.25f * rollAlarm);
            rollCounter.gameObject.SetActive(!TutorialGuide.Instance); // the tutorial room's rerolls are free

            // Legendary build: a T key under the phone fills up as the hidden power recharges.
            var charge = PlayerLegendPower.Charge;
            if (legendKey.gameObject.activeSelf != charge.HasValue) legendKey.gameObject.SetActive(charge.HasValue);
            if (charge.HasValue)
            {
                legendFill.fillAmount = charge.Value;
                bool ready = charge.Value >= 1f;
                legendFill.color = ready ? Color.Lerp(Yellow, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 8f)) : WithAlpha(Yellow, 0.55f);
                legendKey.localScale = Vector3.one * (ready ? 1f + 0.06f * Mathf.Sin(t * 8f) : 1f);
            }

            powerFire.Tick(t, Time.unscaledDeltaTime);
            jumpFire.Tick(t, Time.unscaledDeltaTime);
            swimFire.Tick(t, Time.unscaledDeltaTime);
            wingFire.Tick(t, Time.unscaledDeltaTime);
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
            // A skill switching on from a reroll is "unlocked": sparks fly from the body to the icon, which then pops open.
            bool unlockSwim = !first && s == 1 && swim == 0, unlockWing = !first && f == 1 && wing == 0;
            if (s != swim) SetAbility(swimButton, swimIcon, swimFire, s == 1, first || unlockSwim);
            if (f != wing) SetAbility(wingButton, wingIcon, wingFire, f == 1, first || unlockWing);
            if (unlockSwim) StartCoroutine(Unlock(swimButton, swimIcon, swimFire, 0f));
            if (unlockWing) StartCoroutine(Unlock(wingButton, wingIcon, wingFire, unlockSwim ? 0.18f : 0f));
            power = p; jump = j; heavy = h; swim = s; wing = f;
            powerFire.Burning = p >= 3;
            jumpFire.Burning = j >= 3;
            swimFire.Burning = s == 1 && !unlockSwim; // unlocking ones ignite when the sparks land
            wingFire.Burning = f == 1 && !unlockWing;
            if (changed) StartCoroutine(Wobble());
            if (!first && build?.Legend != null) { if (party != null) StopCoroutine(party); party = StartCoroutine(LegendParty()); }
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

        void SetAbility(Image button, Image icon, Fire fire, bool on, bool instant)
        {
            fire.Idle = button.color = on ? Glow : WithAlpha(RowLine, 0.35f);
            button.transform.GetChild(0).GetComponent<Image>().color = on ? Teal : Screen;
            // On: bright white glyph at full strength. Off: the same shape, faded, so it still reads as not-yet.
            icon.color = on ? Color.white : Ice;
            icon.transform.parent.GetComponent<CanvasGroup>().alpha = on ? 1f : 0.32f;
            if (!instant && on) StartCoroutine(Pop(button.transform, 0f, 1f, 1.25f));
        }

        // ---------- legendary roll: the phone rings, then dances ----------

        float danceTilt, danceX, danceY;
        RectTransform legendKey;
        Image legendFill;
        RectTransform rollCounter;
        Image rollPlate;
        TMPro.TMP_Text rollText;
        int shownRolls = int.MinValue;
        float rollPop, rollAlarm;
        Coroutine party;

        IEnumerator LegendParty()
        {
            for (float d = 0f; d < UnlockDelay; d += Time.unscaledDeltaTime) yield return null; // after the combine cloud bursts
            // Ring: three bursts of fast buzzing, like a phone vibrating on a table.
            for (int ring = 0; ring < 3; ring++)
            {
                ProjectRTD.Core.SoundManager.Play(ProjectRTD.Core.Sfx.Confirm);
                for (float t = 0f; t < 0.38f; t += Time.unscaledDeltaTime)
                {
                    danceTilt = Mathf.Sin(t * 95f) * 9f;
                    danceX = Mathf.Sin(t * 120f) * 5f;
                    screenFlash = 1f;
                    yield return null;
                }
                danceTilt = danceX = 0f;
                for (float t = 0f; t < 0.16f; t += Time.unscaledDeltaTime) yield return null;
            }
            // Dance: hop and sway on a beat, screen strobing gold.
            ProjectRTD.Core.SoundManager.Play(ProjectRTD.Core.Sfx.Goal);
            const float beat = 0.3f;
            for (float t = 0f; t < beat * 12f; t += Time.unscaledDeltaTime)
            {
                float phase = t / beat, hop = Mathf.Abs(Mathf.Sin(phase * Mathf.PI));
                danceY = hop * 46f;
                danceTilt = Mathf.Sin(phase * Mathf.PI) * 16f;
                danceX = Mathf.Sin(phase * Mathf.PI * 0.5f) * 26f;
                float squash = 1f - 0.12f * (1f - hop);
                body.localScale = new Vector3(2f - squash, squash, 1f);
                screenFlash = (int)(phase * 2f) % 2 == 0 ? 0.9f : 0.2f;
                screenGlow.color = Color.Lerp(Yellow, Color.white, screenFlash);
                yield return null;
            }
            danceTilt = danceX = danceY = 0f;
            body.localScale = Vector3.one;
            party = null;
        }

        // ---------- skill unlock ----------

        const float UnlockDelay = 0.85f; // the combine cloud bursts at ~0.8 s (PlayerCombineFx.burstAt)
        const int Sparks = 22;
        float screenFlash; // brief white flare on the screen edge when a spark is absorbed

        IEnumerator Unlock(Image button, Image icon, Fire fire, float extraDelay)
        {
            var glyph = icon.transform.parent.GetComponent<CanvasGroup>();
            glyph.alpha = 0.32f; // still locked-looking until the sparks arrive
            for (float d = 0f; d < UnlockDelay + extraDelay; d += Time.unscaledDeltaTime) yield return null;

            var canvas = root.parent as RectTransform;
            var player = StageRuntime.Instance ? StageRuntime.Instance.Player : null;
            if (!canvas || !player) { Opened(button, glyph, fire); yield break; }
            Vector2 from = canvas.InverseTransformPoint(player.position + Vector3.up * 0.75f);

            if (!softGlow) softGlow = MakeSoftGlow();
            Color[] tints = { Color.white, new(0.8f, 0.97f, 1f, 1f), new(1f, 0.95f, 0.75f, 1f) };
            var sparks = new (RectTransform rect, Image halo, Vector2 start, Vector2 bend, float delay, float life, float size, bool hit)[Sparks];
            for (int i = 0; i < Sparks; i++)
            {
                // Mixed sizes: mostly small motes, a few big bright ones.
                float size = i % 5 == 0 ? Random.Range(18f, 26f) : Random.Range(6f, 14f);
                var tint = tints[i % tints.Length];
                var holder = Rect("Spark", canvas, Vector2.zero, Vector2.zero, false);
                holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
                holder.anchoredPosition = from;
                var halo = Fixed("Glow", holder, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * 3.2f, WithAlpha(tint, 0.55f), softGlow);
                Fixed("Core", holder, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size, Color.white, circle);
                var start = from + Random.insideUnitCircle * 45f;
                var bend = start + new Vector2(Random.Range(-280f, 280f), Random.Range(60f, 340f));
                sparks[i] = (holder, halo.GetComponent<Image>(), start, bend, i * 0.028f, Random.Range(0.55f, 0.8f), size, false);
            }

            bool opened = false;
            int absorbed = 0;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                Vector2 to = canvas.InverseTransformPoint(icon.transform.position);
                bool alive = false;
                for (int i = 0; i < sparks.Length; i++)
                {
                    var sp = sparks[i];
                    float k = (t - sp.delay) / sp.life;
                    if (k < 0f) { sp.rect.localScale = Vector3.zero; alive = true; continue; }
                    if (k >= 1f)
                    {
                        if (!sp.hit)
                        {
                            // Absorbed: the icon twitches and the screen edge flares a little with every spark.
                            sparks[i].hit = true;
                            sp.rect.gameObject.SetActive(false);
                            absorbed++;
                            StartCoroutine(Pop(button.transform, 0f, 1.08f, 1.16f));
                            screenFlash = 1f;
                        }
                        continue;
                    }
                    alive = true;
                    // Ease-in on the way home so they get sucked in; a quadratic curve swings them round first.
                    float e = k * k * k;
                    float swing = Mathf.Sin(k * Mathf.PI);
                    Vector2 a = Vector2.Lerp(sp.start, sp.bend, Mathf.Min(1f, k * 1.6f)), b = Vector2.Lerp(sp.bend, to, e);
                    sp.rect.anchoredPosition = Vector2.Lerp(a, b, Mathf.SmoothStep(0f, 1f, k));
                    // Pop out, then shrink into the icon; the halo flickers.
                    float scale = k < 0.12f ? k / 0.12f : Mathf.Lerp(1.15f, 0.15f, e);
                    sp.rect.localScale = Vector3.one * scale;
                    sp.halo.color = WithAlpha(sp.halo.color, (0.35f + 0.35f * swing) * (0.8f + 0.2f * Mathf.Sin(t * 30f + i)));
                }
                if (!opened && absorbed >= Sparks / 2) { opened = true; Opened(button, glyph, fire); }
                if (!alive) break;
                yield return null;
            }
            if (!opened) Opened(button, glyph, fire);
            foreach (var sp in sparks) if (sp.rect) Destroy(sp.rect.gameObject);
        }

        static Sprite softGlow;

        // Radial soft glow (bright centre fading to nothing) for spark halos.
        static Sprite MakeSoftGlow()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f)));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        // The icon bursts open: white flash ring, big pop, then it catches fire.
        void Opened(Image button, CanvasGroup glyph, Fire fire)
        {
            glyph.alpha = 1f;
            fire.Burning = true;
            StartCoroutine(Pop(button.transform, 0f, 0.6f, 1.45f));
            StartCoroutine(FlashRing(button.rectTransform));
            StartCoroutine(BigBurst(button.rectTransform));
            StartCoroutine(Wobble());
        }

        // The unlock bursts right out of the phone: a wide flash, a shockwave of light and sparks flying well past
        // the phone's edges. Drawn on the HUD canvas itself, so nothing clips it to the phone.
        IEnumerator BigBurst(RectTransform button)
        {
            var canvas = root.parent as RectTransform;
            if (!canvas) yield break;
            if (!softGlow) softGlow = MakeSoftGlow();
            Vector2 at = canvas.InverseTransformPoint(button.position);
            var flash = Fixed("Unlock Burst", canvas, new Vector2(0.5f, 0.5f), at, Vector2.one * 200f, Color.white, softGlow);
            var wave = Fixed("Unlock Wave", canvas, new Vector2(0.5f, 0.5f), at, Vector2.one * 200f, WithAlpha(Yellow, 0.8f), softGlow);
            const int count = 18;
            var bits = new (RectTransform rect, Vector2 dir, float reach, float size)[count];
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.6f) / count * Mathf.PI * 2f;
                float size = i % 3 == 0 ? Random.Range(20f, 30f) : Random.Range(9f, 16f);
                var holder = Rect("Bit", canvas, Vector2.zero, Vector2.zero, false);
                holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
                holder.anchoredPosition = at;
                Fixed("Glow", holder, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * 3f, WithAlpha(i % 2 == 0 ? Yellow : Glow, 0.6f), softGlow);
                Fixed("Core", holder, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size, Color.white, circle);
                bits[i] = (holder, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), Random.Range(260f, 520f), size);
            }
            var flashImage = flash.GetComponent<Image>();
            var waveImage = wave.GetComponent<Image>();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.7f)
            {
                float e = 1f - Mathf.Pow(1f - t, 3f);
                flash.sizeDelta = Vector2.one * Mathf.Lerp(200f, 1100f, e);
                flashImage.color = WithAlpha(Color.white, 0.95f * (1f - t) * (1f - t));
                wave.sizeDelta = Vector2.one * Mathf.Lerp(150f, 900f, e);
                waveImage.color = WithAlpha(Yellow, 0.7f * (1f - t));
                foreach (var b in bits)
                {
                    b.rect.anchoredPosition = at + b.dir * (b.reach * e) + Vector2.down * (120f * t * t); // arc down a little
                    b.rect.localScale = Vector3.one * (1f - t * t);
                }
                yield return null;
            }
            Destroy(flash.gameObject);
            Destroy(wave.gameObject);
            foreach (var b in bits) Destroy(b.rect.gameObject);
        }

        IEnumerator FlashRing(RectTransform button)
        {
            var ring = Fixed("Unlock Flash", button, new Vector2(0.5f, 0.5f), Vector2.zero, button.rect.size, Color.white, round);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
            {
                float s = 1f + 1.2f * t;
                ring.localScale = new Vector3(s, s, 1f);
                ring.GetComponent<Image>().color = WithAlpha(Color.white, 0.9f * (1f - t));
                yield return null;
            }
            Destroy(ring.gameObject);
        }

        // ---------- animation ----------

        IEnumerator SlideIn()
        {
            var to = Home;
            var from = to + Vector2.up * (H * 1.1f + 160f); // drops in from above
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
            // Unity's null check, not ??=: a scene change can unload these runtime assets, leaving a destroyed (fake-null) object.
            if (!round) round = MakeRound(64, 22, false);
            if (!circle) circle = MakeRound(64, 32, true);

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
                rowLines[i] = row.GetComponent<Image>();
                row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(0.5f, 1f);
                row.offsetMin = new Vector2(8f, -(10f + i * (rowH + gap) + rowH));
                row.offsetMax = new Vector2(-8f, -(10f + i * (rowH + gap)));
                var inner = Panel("Inner", row, 2f, Screen, 18f);
                rowInners[i] = inner;
                Icon(inner, new[] { "phone_weight", "phone_heart", "phone_jump", "phone_star" }[i]);
                switch (i)
                {
                    case 0: BuildToggle(inner); break;
                    case 1: BuildPips(inner, powerPips); break;
                    case 2: BuildPips(inner, jumpPips); break;
                    case 3:
                        wingButton = AbilityButton(inner, "Fly", -6f, "phone_wing", out wingIcon);
                        swimButton = AbilityButton(inner, "Swim", -68f, "phone_swim", out swimIcon);
                        break;
                }
            }

            // Reroll counter tag hanging under the phone.
            rollCounter = Fixed("Roll Counter", body, new Vector2(0.5f, 0f), new Vector2(0f, -32f), new Vector2(132f, 48f), Outline, round);
            rollPlate = Fixed("Plate", rollCounter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(124f, 40f), Screen, round).GetComponent<Image>();
            var rollLabel = Rect("Count", rollCounter, Vector2.zero, Vector2.zero, true);
            rollText = rollLabel.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            rollText.alignment = TMPro.TextAlignmentOptions.Center;
            rollText.fontSize = 30f;
            rollText.color = Yellow;
            rollText.raycastTarget = false;

            // T key badge hanging under the reroll counter (legend builds only).
            legendKey = Fixed("Legend Key", body, new Vector2(0.5f, 0f), new Vector2(0f, -86f), new Vector2(96f, 44f), Outline, round);
            Fixed("Back", legendKey, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88f, 36f), Screen, round);
            legendFill = Fixed("Fill", legendKey, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88f, 36f), Yellow, round).GetComponent<Image>();
            legendFill.type = Image.Type.Filled;
            legendFill.fillMethod = Image.FillMethod.Horizontal;
            // The letter T from two bars, so it needs no font.
            Fixed("T Top", legendKey, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(26f, 7f), Outline, null);
            Fixed("T Stem", legendKey, new Vector2(0.5f, 0.5f), new Vector2(0f, -3f), new Vector2(7f, 22f), Outline, null);
            legendKey.gameObject.SetActive(false);

            // Extra CRT on the little screen: scanlines that crawl downward, a rolling bright band, and flicker.
            if (!scanlines) scanlines = MakeScanlines();
            var lines = Rect("Scanlines", face, Vector2.zero, Vector2.zero, true);
            scanImage = lines.gameObject.AddComponent<RawImage>();
            scanImage.texture = scanlines;
            scanImage.color = WithAlpha(Color.black, 0.16f);
            scanImage.raycastTarget = false;
            var band = Rect("Roll Band", face, Vector2.zero, Vector2.zero, false);
            band.anchorMin = new Vector2(0f, 0f); band.anchorMax = new Vector2(1f, 0f); band.pivot = new Vector2(0.5f, 0.5f);
            band.sizeDelta = new Vector2(0f, 26f);
            rollBand = band.gameObject.AddComponent<Image>();
            rollBand.color = WithAlpha(Ice, 0.045f);
            rollBand.raycastTarget = false;
            screenFlicker = face.gameObject.AddComponent<CanvasGroup>();

            // Max stats and active skills burn: flames lick up off the element and its border glows orange.
            if (wideFlames == null || !wideFlames[0]) wideFlames = MakeFlameFrames(7, 9);
            if (narrowFlames == null || !narrowFlames[0]) narrowFlames = MakeFlameFrames(3, 17);
            powerFire = new Fire(rowInners[1], rowLines[1], wideFlames, 0.62f, 7, RowLine);
            jumpFire = new Fire(rowInners[2], rowLines[2], wideFlames, 0.62f, 7, RowLine);
            swimFire = new Fire((RectTransform)swimButton.transform.GetChild(0), swimButton, narrowFlames, 0.72f, 4, Glow);
            wingFire = new Fire((RectTransform)wingButton.transform.GetChild(0), wingButton, narrowFlames, 0.72f, 4, Glow);
        }

        static Texture2D MakeScanlines()
        {
            var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
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

        // Returns the border image (the button root); child 0 is the fill. The glyph is the icon over an ink outline
        // (offset dark copies) so the thin swim / wing shapes read against the teal and the fire behind them.
        Image AbilityButton(RectTransform row, string name, float x, string sprite, out Image icon)
        {
            const float w = 58f, h = 40f;
            var border = Fixed(name, row, new Vector2(1f, 0.5f), new Vector2(x - w * 0.5f, 0f), new Vector2(w, h), Glow, round);
            var fill = Fixed("Fill", border, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w - 6f, h - 6f), Teal, round);
            var glyph = Rect("Glyph", fill, Vector2.zero, Vector2.zero, true);
            glyph.gameObject.AddComponent<CanvasGroup>();
            var art = Load(sprite);
            var size = new Vector2(46f, 32f);
            foreach (var o in new[] { new Vector2(2f, 0f), new Vector2(-2f, 0f), new Vector2(0f, 2f), new Vector2(0f, -2f), new Vector2(1.5f, -2.5f) })
                Fixed("Ink", glyph, new Vector2(0.5f, 0.5f), o, size, Outline, art).GetComponent<Image>().preserveAspect = true;
            icon = Fixed("Icon", glyph, new Vector2(0.5f, 0.5f), Vector2.zero, size, Color.white, art).GetComponent<Image>();
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

        internal static Sprite MakeRound(int size, int radius, bool full)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
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
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        // One-piece cartoon fire burning inside an element: a crown of flame tongues rising from its floor, clipped to the
        // element's rounded shape and drawn behind its icons, flipping through hand-drawn-style frames (ink outline,
        // flat orange, yellow core). Small embers float up inside it and the element's border glows.
        sealed class Fire
        {
            struct Ember { public RectTransform ink, dot; public Image dotImage; public float born, life, x, rise, drift, size; }

            readonly RectTransform host, crown;
            readonly Image crownImage, border;
            readonly Sprite[] frames;
            readonly float heightFraction;
            readonly Ember[] embers;
            readonly float phase = Random.value * 10f;
            bool burning;
            float heat, litAt = -10f; // heat 0..1 eases in and out so the fire doesn't pop
            public Color Idle;

            public Fire(RectTransform host, Image border, Sprite[] frames, float heightFraction, int emberCount, Color idle)
            {
                this.host = host; this.border = border; this.frames = frames; this.heightFraction = heightFraction; Idle = idle;
                // Clip to the element's own rounded image so nothing leaves its border.
                if (!host.GetComponent<Mask>()) host.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                var layer = Rect("Fire", host, Vector2.zero, Vector2.zero, true);
                layer.SetAsFirstSibling(); // behind the icon, pips and toggle

                crown = Rect("Flame", layer, Vector2.zero, Vector2.zero, false);
                crown.anchorMin = new Vector2(0f, 0f);
                crown.anchorMax = new Vector2(1f, 0f);
                crown.pivot = new Vector2(0.5f, 0f);
                crownImage = crown.gameObject.AddComponent<Image>();
                crownImage.sprite = frames[0];
                crownImage.raycastTarget = false;
                crown.gameObject.SetActive(false);

                embers = new Ember[emberCount];
                for (int i = 0; i < emberCount; i++)
                {
                    var ink = Fixed("Ember", layer, new Vector2(0f, 0f), Vector2.zero, Vector2.zero, Outline, circle);
                    var dot = Fixed("Ember", layer, new Vector2(0f, 0f), Vector2.zero, Vector2.zero, Yellow, circle);
                    ink.gameObject.SetActive(false); dot.gameObject.SetActive(false);
                    embers[i] = new Ember { ink = ink, dot = dot, dotImage = dot.GetComponent<Image>(), born = -10f, life = 1f };
                }
            }

            public bool Burning
            {
                set
                {
                    if (value && !burning) litAt = Time.unscaledTime;
                    burning = value;
                }
            }

            public void Tick(float now, float dt)
            {
                heat = Mathf.MoveTowards(heat, burning ? 1f : 0f, dt * (burning ? 6f : 3f));
                // Stepped flicker between two flat tones, like a cel animation, rather than a smooth pulse.
                var hot = Mathf.Repeat(now * 8f + phase, 1f) < 0.5f ? FlameMid : Yellow;
                border.color = Color.Lerp(Idle, hot, heat);

                bool visible = heat > 0f;
                if (crown.gameObject.activeSelf != visible) crown.gameObject.SetActive(visible);
                if (!visible)
                {
                    foreach (var e in embers) ShowEmber(e, false);
                    return;
                }

                var size = host.rect.size;
                // Ignition flare, then a living flicker: frames at ~12 fps and a small height wobble.
                float since = now - litAt;
                float flare = since < 0.35f ? 1f + 0.35f * Mathf.Sin(since / 0.35f * Mathf.PI) : 1f;
                float wobble = 1f + 0.07f * Mathf.Sin(now * 13f + phase) + 0.05f * Mathf.Sin(now * 21f + phase * 2f);
                crownImage.sprite = frames[(int)((now + phase) * 12f) % frames.Length];
                crown.anchoredPosition = new Vector2(0f, -2f);
                crown.sizeDelta = new Vector2(8f, size.y * heightFraction * heat * flare * wobble);

                for (int i = 0; i < embers.Length; i++)
                {
                    ref var e = ref embers[i];
                    float age = now - e.born;
                    if (age > e.life)
                    {
                        if (!burning) { ShowEmber(e, false); continue; }
                        e.born = now - Random.Range(0f, 0.3f);
                        e.life = Random.Range(0.6f, 1.0f);
                        e.x = Random.Range(0.08f, 0.92f);
                        e.rise = size.y * Random.Range(0.55f, 0.9f);
                        e.drift = Random.Range(-8f, 8f);
                        e.size = Random.Range(3f, 5f);
                        age = now - e.born;
                    }
                    float k = Mathf.Clamp01(age / e.life);
                    var pos = new Vector2(size.x * e.x + e.drift * k + Mathf.Sin(now * 9f + i) * 1.5f,
                                          size.y * heightFraction * 0.45f + e.rise * k);
                    float d = e.size * (1f - k) * heat;
                    e.ink.anchoredPosition = e.dot.anchoredPosition = pos;
                    e.ink.sizeDelta = Vector2.one * (d + 2.5f);
                    e.dot.sizeDelta = Vector2.one * d;
                    e.dotImage.color = k < 0.5f ? Yellow : FlameMid;
                    ShowEmber(e, d > 0.6f);
                }
            }

            static void ShowEmber(Ember e, bool on)
            {
                if (e.ink.gameObject.activeSelf == on) return;
                e.ink.gameObject.SetActive(on);
                e.dot.gameObject.SetActive(on);
            }
        }

        // A strip of flame tongues per frame. Each frame moves the tongues a little so cycling them reads as fire.
        static Sprite[] MakeFlameFrames(int tongues, int seed)
        {
            const int w = 160, h = 64, frameCount = 4, ink = 3;
            var rng = new System.Random(seed);
            var centers = new float[tongues];
            var heights = new float[tongues];
            for (int i = 0; i < tongues; i++) { centers[i] = (i + 0.5f) / tongues; heights[i] = 0.7f + (float)rng.NextDouble() * 0.3f; }

            Color32 inkC = Outline, bodyC = FlameMid, coreC = Yellow, clear = new Color32(0, 0, 0, 0);
            var sprites = new Sprite[frameCount];
            for (int f = 0; f < frameCount; f++)
            {
                // Height profile: a low continuous base plus pointed tongues, tapering at both ends.
                var body = new float[w];
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, top = 0.26f + 0.05f * Mathf.Sin(u * 40f + f * 1.9f);
                    for (int i = 0; i < tongues; i++)
                    {
                        // Each tongue: broad base, needle tip, leaning a little differently every frame.
                        float lean = Mathf.Sin(f * 1.7f + i * 2.3f) * 0.25f;
                        float c = centers[i] + lean * 0.3f / tongues;
                        float hh = heights[i] * (0.78f + 0.22f * Mathf.Sin(f * 2.1f + i * 1.3f));
                        float d = (u - c) * tongues;
                        d = d < 0f ? -d * (1f + lean) : d * (1f - lean);
                        top = Mathf.Max(top, hh * Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f));
                    }
                    float edge = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 7f);
                    body[x] = top * Mathf.Lerp(0.3f, 1f, edge) * (h - ink - 1);
                }
                var px = new Color32[w * h];
                for (int x = 0; x < w; x++)
                {
                    // Outline = the body grown by the ink width in every direction.
                    float grown = 0f;
                    for (int dx = -ink; dx <= ink; dx++) grown = Mathf.Max(grown, body[Mathf.Clamp(x + dx, 0, w - 1)]);
                    grown += ink;
                    float core = body[x] * 0.45f - 2f;
                    for (int y = 0; y < h; y++)
                        px[y * w + x] = y < core ? coreC : y < body[x] ? bodyC : y < grown ? inkC : clear;
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
                tex.SetPixels32(px);
                tex.Apply();
                sprites[f] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
                sprites[f].hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            return sprites;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        static Color Hex(int rgb) => new((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, 1f);
    }
}
