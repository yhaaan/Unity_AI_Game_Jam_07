using ProjectRTD.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // Stage HUD layout (positions allow for the curve, which pushes the top edge off-screen): stage number centred at the top with one long rounded bar per stage underneath (the current
    // stage lit yellow), the goal under that, and the key guide in the bottom-left corner. Everything sits on the
    // curved HUD canvas so it bends and gets the CRT look together with the stage.
    [DisallowMultipleComponent]
    public sealed class StageTopHud : MonoBehaviour
    {
        const string StageHudName = "Stage HUD";
        const float BarW = 46f, BarH = 14f, BarGap = 8f;

        static readonly Color Ink = new(0.043f, 0.102f, 0.227f, 1f), Lit = new(1f, 0.784f, 0.078f, 1f),
            Cleared = new(0.65f, 0.55f, 0.82f, 1f), Ahead = new(0.04f, 0.10f, 0.25f, 0.85f);

        static Sprite pill;
        RectTransform current;

        // For the tutorial spotlight.
        internal RectTransform StageBars { get; private set; }
        internal RectTransform StageName { get; private set; }
        internal RectTransform GoalText { get; private set; }
        internal RectTransform Controls { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (_, _) => Attach();
            Attach();
        }

        static void Attach()
        {
            if (!PlayerPhoneHud.IsStage() || FindFirstObjectByType<StageTopHud>(FindObjectsInactive.Include)) return;
            var host = PlayerPhoneHud.CurvedCanvas();
            if (!host) return;
            var go = new GameObject("Stage Top HUD", typeof(RectTransform));
            go.layer = 0;
            go.transform.SetParent(host, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.AddComponent<StageTopHud>();
        }

        // Start, not Awake: the pause menu styles the old texts in its Awake, and this layout replaces that.
        void Start()
        {
            var stageHud = GameObject.Find(StageHudName);
            if (!stageHud) return;
            var hud = stageHud.transform;

            // Soft dark plates keep the HUD readable on bright stage moods and busy backgrounds.
            Plate("Header Plate", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(520f, 150f));
            Plate("Controls Plate", Vector2.zero, new Vector2(130f + 300f, 130f + 42f), new Vector2(660f, 112f));

            if (Take(hud, "Stage Name") is { } stageName)
            {
                Place(stageName, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(600f, 56f));
                stageName.alignment = TextAlignmentOptions.Top;
                stageName.fontSize = 42f;
                StageName = stageName.rectTransform;
            }
            if (StageNumber() > 0) BuildStageBars(new Vector2(0f, -172f)); // the tutorial room has no progress bar
            if (Take(hud, "Goal Text") is { } goal)
            {
                Place(goal, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(700f, 40f));
                goal.alignment = TextAlignmentOptions.Top;
                goal.fontSize = 22f;
                GoalText = goal.rectTransform;
            }
            if (Take(hud, "Controls") is { } controls)
            {
                // The curve sinks the corners, so keep clear of the bottom-left edge.
                var rect = controls.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(130f, 130f);
                rect.sizeDelta = new Vector2(640f, 90f);
                controls.text = "R : New Collaboration!\n<size=62%>A/D : MOVE     SPACE : JUMP / GLIDE     TAB : PARTS</size>";
                controls.enableAutoSizing = false;
                controls.fontSize = 34f;
                controls.alignment = TextAlignmentOptions.BottomLeft;
                Controls = rect;
            }
        }

        void Update()
        {
            if (!current) return;
            float s = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f);
            current.localScale = new Vector3(s, s, 1f);
        }

        static Sprite halo;

        // A soft glow behind the text instead of a box: no edges, just a dark-blue haze that fades out,
        // so light lettering stays readable on bright stage moods.
        void Plate(string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            if (!halo) halo = MakeHalo();
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = gameObject.layer;
            rect.SetParent(transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size * 1.7f; // the glow fades to nothing well before its edge
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = halo;
            image.color = new Color(0.03f, 0.07f, 0.2f, 0.78f);
            image.raycastTarget = false;
        }

        static Sprite MakeHalo()
        {
            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        // Moves a stage-HUD text onto this curved layer.
        TMP_Text Take(Transform hud, string name)
        {
            var child = hud.Find(name);
            if (!child || !child.TryGetComponent<TMP_Text>(out var text)) return null;
            child.SetParent(transform, false);
            child.gameObject.layer = gameObject.layer;
            text.raycastTarget = false;
            return text;
        }

        static void Place(TMP_Text text, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        void BuildStageBars(Vector2 position)
        {
            if (!pill) pill = PlayerPhoneHud.MakeRound(64, 32, false);
            int count = SceneFlow.StageCount;
            int stage = StageNumber();
            float width = count * BarW + (count - 1) * BarGap;

            var row = new GameObject("Stage Bars", typeof(RectTransform)).GetComponent<RectTransform>();
            row.gameObject.layer = gameObject.layer;
            row.SetParent(transform, false);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = position;
            row.sizeDelta = new Vector2(width, BarH + 8f);
            StageBars = row;

            for (int i = 1; i <= count; i++)
            {
                float x = -width * 0.5f + BarW * 0.5f + (i - 1) * (BarW + BarGap);
                bool isCurrent = i == stage;
                float h = isCurrent ? BarH + 4f : BarH;
                var outline = Bar("Stage " + i, row, new Vector2(x, 0f), new Vector2(BarW + 4f, h + 4f), Ink);
                var fill = Bar("Fill", outline, Vector2.zero, new Vector2(BarW, h), isCurrent ? Lit : i < stage ? Cleared : Ahead);
                if (isCurrent)
                {
                    // Cel highlight on the lit bar.
                    Bar("Shine", fill, new Vector2(-BarW * 0.18f, h * 0.18f), new Vector2(BarW * 0.4f, h * 0.28f), new Color(1f, 1f, 1f, 0.75f));
                    current = outline;
                }
            }
        }

        static RectTransform Bar(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = pill;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 32f / (size.y * 0.5f); // fully rounded ends
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        int StageNumber()
        {
            var name = gameObject.scene.name;
            return name.StartsWith("Stage") && int.TryParse(name.Substring(5), out int n) ? n : 0;
        }
    }
}
