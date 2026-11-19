using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // First time Stage 01 starts in a run: the stage goes dark, and the HUD pieces are lit one at a time with a short
    // explanation each (SPACE / Enter / click for next, Tab to skip). The game is paused meanwhile, so the delayed
    // entry reroll happens right after the tutorial ends.
    [DisallowMultipleComponent]
    public sealed class StageTutorial : MonoBehaviour
    {
        const string TutorialStage = ProjectRTD.Core.SceneFlow.TutorialScene;
        const float Dim = 0.25f;

        static readonly Color Ink = new(0.043f, 0.102f, 0.227f, 1f), Paper = new(1f, 0.97f, 0.9f, 1f),
            Spot = new(1f, 0.784f, 0.078f, 1f);

        static bool shownThisRun;

        // True from scene load until the tutorial closes; the entry reroll waits for it.
        internal static bool Active { get; private set; }
        static Sprite ring, panel;

        enum Focus { Phone, Row0, Row1, Row2, Row3, Header, Controls }

        struct Step
        {
            public Focus focus;
            public string text;
        }

        static readonly Step[] Steps =
        {
            new() { focus = Focus.Phone,    text = "이건 <color=#FFC814><b>키메라 폰</b></color>!\n지금 조합의 능력치를 보여줘요." },
            new() { focus = Focus.Row0,     text = "<color=#FFC814><b>무게</b></color>\n무거운 조합이면 스위치가 켜져요.\n무거우면 약한 다리가 무너질 수 있어요!" },
            new() { focus = Focus.Row1,     text = "<color=#FFC814><b>힘</b></color>\n3칸을 채우면 무거운 상자를 밀 수 있어요." },
            new() { focus = Focus.Row2,     text = "<color=#FFC814><b>점프</b></color>\n칸이 많을수록 더 높이 뛰어요." },
            new() { focus = Focus.Row3,     text = "<color=#FFC814><b>특수 능력</b></color> — 수영 · 날기\n가진 능력은 불타오르며 켜져요!\n최대 능력치도 불타요." },
            new() { focus = Focus.Header,   text = "지금 <color=#FFC814><b>스테이지</b></color>와\n이번 방을 통과하는 <color=#FFC814><b>목표</b></color>예요." },
            new() { focus = Focus.Controls, text = "<color=#FFC814><b>R</b></color> 로 새 조합을 뽑아요!\n<color=#FFC814><b>A/D</b></color> 이동,  <color=#FFC814><b>SPACE</b></color> 점프 · 활공" },
        };

        RectTransform canvas, overlay, frame, callout;
        Image overlayImage;
        TMP_Text calloutText, hint;
        PlayerPhoneHud phone;
        StageTopHud top;
        CanvasGroup phoneGroup;
        readonly CanvasGroup[] rowGroups = new CanvasGroup[4];
        CanvasGroup nameGroup, barsGroup, goalGroup, controlsGroup;
        float previousTimeScale = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRun()
        {
            shownThisRun = false;
            Active = false;
            SceneManager.activeSceneChanged -= ForgetOutsideStages;
            SceneManager.activeSceneChanged += ForgetOutsideStages;
        }

        // Going back through the menus starts a new run, which shows the tutorial again.
        static void ForgetOutsideStages(Scene from, Scene to)
        {
            if (!to.name.StartsWith("Stage", System.StringComparison.Ordinal)) shownThisRun = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (scene, _) => Attach(scene);
            Attach(SceneManager.GetActiveScene());
        }

        static void Attach(Scene scene)
        {
            if (shownThisRun || scene.name != TutorialStage) return;
            shownThisRun = true;
            Active = true;
            var go = new GameObject("Stage Tutorial");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<StageTutorial>();
        }

        IEnumerator Start()
        {
            // Let the door arrival (which fades in from black on game time) finish and the HUD settle first.
            for (float limit = 15f; limit > 0f && (!StageRuntime.Instance || StageRuntime.Instance.InDoorway); limit -= Time.unscaledDeltaTime)
                yield return null;
            while (StageObjectiveBanner.Showing) yield return null; // let the mission banner finish first
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime) yield return null;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            phone = FindFirstObjectByType<PlayerPhoneHud>();
            top = FindFirstObjectByType<StageTopHud>();
            canvas = phone ? (RectTransform)phone.transform.parent : null;
            if (!phone || !top || !canvas) { Finish(); yield break; }

            BuildChrome();
            yield return FadeOverlay(0f, 0.72f, 0.35f);

            for (int i = 0; i < Steps.Length; i++)
            {
                Show(Steps[i], i);
                yield return null; // don't let the key that opened this step also close it
                bool skip = false;
                while (true)
                {
                    PulseFrame();
                    if (Pressed(out skip)) break;
                    yield return null;
                }
                if (skip) break;
            }

            frame.gameObject.SetActive(false);
            callout.gameObject.SetActive(false);
            SetDim(null);
            yield return FadeOverlay(overlayImage.color.a, 0f, 0.3f);
            Finish();
        }

        void Finish()
        {
            Active = false;
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            if (StageRuntime.Instance && StageRuntime.Instance.Player &&
                StageRuntime.Instance.Player.TryGetComponent<PlayerInputReader>(out var input))
                input.ConsumeJumpPressed(); // the SPACE that closed the tutorial is not a jump
            if (overlay) Destroy(overlay.gameObject);
            if (callout) Destroy(callout.gameObject);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            Active = false;
            // Scene unloaded mid-tutorial: never leave the game paused.
            if (Time.timeScale == 0f && !StagePausedByMenu()) Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        }

        static bool StagePausedByMenu() => GameObject.Find("Pause menu") is { activeInHierarchy: true };

        static bool Pressed(out bool skip)
        {
            var k = Keyboard.current;
            var m = Mouse.current;
            skip = k != null && k.tabKey.wasPressedThisFrame; // not Esc: that opens the pause menu
            return skip
                || k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)
                || m != null && m.leftButton.wasPressedThisFrame;
        }

        // ---------- steps ----------

        void Show(Step step, int index)
        {
            SetDim(step.focus);
            var target = Target(step.focus, out var pad);

            // The frame rides on the target, so it follows the phone's tilt and bob.
            frame.SetParent(target, false);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(-pad.x, -pad.w);
            frame.offsetMax = new Vector2(pad.z, pad.y);
            frame.SetAsLastSibling();
            frame.gameObject.SetActive(true);

            calloutText.text = step.text;
            hint.text = index < Steps.Length - 1 ? $"{index + 1}/{Steps.Length}   SPACE ▶   TAB 건너뛰기" : $"{index + 1}/{Steps.Length}   SPACE  시작!";
            PlaceCallout(target, step.focus);
            StartCoroutine(Pop(callout));
        }

        RectTransform Target(Focus focus, out Vector4 pad) // pad: left, top, right, bottom
        {
            switch (focus)
            {
                case Focus.Phone: pad = new Vector4(10, 10, 10, 10); return phone.Body;
                case Focus.Row0: case Focus.Row1: case Focus.Row2: case Focus.Row3:
                    pad = new Vector4(6, 6, 6, 6); return phone.Row(focus - Focus.Row0);
                case Focus.Header:
                    if (top.StageBars) { pad = new Vector4(40, 92, 40, 56); return top.StageBars; }
                    pad = new Vector4(30, 14, 30, 60); return top.StageName; // tutorial room: no progress bar
                default: pad = new Vector4(16, 12, 16, 12); return top.Controls;
            }
        }

        void SetDim(Focus? focus)
        {
            bool all = focus == null;
            bool onPhone = all || focus is Focus.Phone or Focus.Row0 or Focus.Row1 or Focus.Row2 or Focus.Row3;
            phoneGroup.alpha = onPhone ? 1f : Dim;
            for (int i = 0; i < 4; i++)
                rowGroups[i].alpha = all || focus == Focus.Phone || focus == Focus.Row0 + i ? 1f : Dim;
            nameGroup.alpha = barsGroup.alpha = goalGroup.alpha = all || focus == Focus.Header ? 1f : Dim;
            controlsGroup.alpha = all || focus == Focus.Controls ? 1f : Dim;
        }

        void PlaceCallout(RectTransform target, Focus focus)
        {
            // Target centre in canvas space (canvas origin is its centre).
            Vector2 c = canvas.InverseTransformPoint(target.TransformPoint(target.rect.center));
            Vector2 half = canvas.rect.size * 0.5f;
            Vector2 size = callout.sizeDelta;
            Vector2 pos = focus switch
            {
                Focus.Header => c + new Vector2(0f, -150f - size.y * 0.5f),
                Focus.Controls => c + new Vector2(size.x * 0.5f - target.rect.width * 0.5f, 90f + size.y * 0.5f),
                _ => c + new Vector2(200f + size.x * 0.5f, 0f), // right of the phone (top-left corner)
            };
            // Keep clear of the curved edges.
            pos.x = Mathf.Clamp(pos.x, -half.x + 120f + size.x * 0.5f, half.x - 120f - size.x * 0.5f);
            pos.y = Mathf.Clamp(pos.y, -half.y + 120f + size.y * 0.5f, half.y - 140f - size.y * 0.5f);
            callout.anchoredPosition = pos;
        }

        // ---------- chrome ----------

        void BuildChrome()
        {
            if (!ring) ring = MakeRing(64, 24, 6);
            if (!panel) panel = PlayerPhoneHud.MakeRound(64, 3, false); // near-square corners

            // Darkens the stage behind the HUD (first child, so every HUD piece stays above it).
            overlay = NewRect("Tutorial Dim", canvas);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.SetAsFirstSibling();
            overlayImage = overlay.gameObject.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0f);
            overlayImage.raycastTarget = false;

            phoneGroup = Group(phone.transform);
            for (int i = 0; i < 4; i++) rowGroups[i] = Group(phone.Row(i));
            nameGroup = Group(top.StageName);
            barsGroup = Group(top.StageBars);
            goalGroup = Group(top.GoalText);
            controlsGroup = Group(top.Controls);

            frame = NewRect("Tutorial Spotlight", canvas);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = ring;
            frameImage.type = Image.Type.Sliced;
            frameImage.fillCenter = false;
            frameImage.pixelsPerUnitMultiplier = 1.4f;
            frameImage.color = Spot;
            frameImage.raycastTarget = false;
            frame.gameObject.SetActive(false);

            // Speech-bubble callout: ink outline, warm paper, dark text.
            callout = NewRect("Tutorial Callout", canvas);
            callout.SetAsLastSibling();
            callout.anchorMin = callout.anchorMax = callout.pivot = new Vector2(0.5f, 0.5f);
            callout.sizeDelta = new Vector2(520f, 190f);
            Fill(callout, new Color(0.07f, 0.08f, 0.11f, 1f), 0f);
            Fill(callout, new Color(0.36f, 0.39f, 0.47f, 1f), 3f);
            var paper = Fill(callout, new Color(0.15f, 0.16f, 0.21f, 1f), 6f);
            var body = NewRect("Text", paper);
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(26f, 44f);
            body.offsetMax = new Vector2(-26f, -18f);
            calloutText = body.gameObject.AddComponent<TextMeshProUGUI>();
            calloutText.color = Color.white;
            calloutText.fontSize = 30f;
            calloutText.enableAutoSizing = true;
            calloutText.fontSizeMin = 18f;
            calloutText.fontSizeMax = 30f;
            calloutText.alignment = TextAlignmentOptions.Center;
            calloutText.raycastTarget = false;
            var hintRect = NewRect("Hint", paper);
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.offsetMin = new Vector2(20f, 10f);
            hintRect.offsetMax = new Vector2(-20f, 40f);
            hint = hintRect.gameObject.AddComponent<TextMeshProUGUI>();
            hint.color = new Color(1f, 1f, 1f, 0.55f);
            hint.fontSize = 18f;
            hint.alignment = TextAlignmentOptions.Bottom;
            hint.raycastTarget = false;
        }

        static CanvasGroup Group(Transform t)
        {
            if (!t) return new GameObject("unused").AddComponent<CanvasGroup>();
            return t.TryGetComponent<CanvasGroup>(out var g) ? g : t.gameObject.AddComponent<CanvasGroup>();
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            return rect;
        }

        static RectTransform Fill(RectTransform parent, Color color, float inset)
        {
            var rect = NewRect("Fill", parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = -Vector2.one * inset;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = panel;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.9f;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        // ---------- animation ----------

        void PulseFrame()
        {
            float t = Time.unscaledTime;
            var image = frame.GetComponent<Image>();
            image.color = Mathf.Repeat(t * 3f, 1f) < 0.5f ? Spot : Color.white;
            float s = 1f + 0.03f * Mathf.Sin(t * 6f);
            frame.localScale = new Vector3(s, s, 1f);
        }

        IEnumerator FadeOverlay(float from, float to, float duration)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
            {
                overlayImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t));
                yield return null;
            }
            overlayImage.color = new Color(0f, 0f, 0f, to);
        }

        static IEnumerator Pop(Transform target)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
            {
                float s = t < 0.6f ? Mathf.Lerp(0.6f, 1.08f, t / 0.6f) : Mathf.Lerp(1.08f, 1f, (t - 0.6f) / 0.4f);
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        // Hollow rounded rectangle for the spotlight frame (9-sliced, centre left empty).
        static Sprite MakeRing(int size, int radius, int thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float outer = Mathf.Clamp01(radius - d + 0.5f), inner = Mathf.Clamp01(d - (radius - thickness) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * outer * inner));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, Vector4.one * radius);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }
}
