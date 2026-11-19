using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // Shown once when a run starts (the first stage scene after the menus): a dark band slides across the middle of the
    // screen with the mission — "연구소에서 무사히 탈출하세요!" — holds for a moment, then fades away.
    // On the curved HUD canvas, so it bends and takes the CRT look.
    [DisallowMultipleComponent]
    public sealed class StageObjectiveBanner : MonoBehaviour
    {
        const float In = 0.35f, Hold = 2.4f, Out = 0.5f;

        static bool shownThisRun;
        internal static bool Showing { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRun()
        {
            shownThisRun = false;
            Showing = false;
            SceneManager.activeSceneChanged -= ForgetOutsideStages;
            SceneManager.activeSceneChanged += ForgetOutsideStages;
        }

        static void ForgetOutsideStages(Scene from, Scene to)
        {
            if (!to.name.StartsWith("Stage", System.StringComparison.Ordinal)) shownThisRun = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (_, _) => Attach();
            Attach();
        }

        static void Attach()
        {
            if (shownThisRun || !PlayerPhoneHud.IsStage()) return;
            var host = PlayerPhoneHud.CurvedCanvas();
            if (!host) return;
            shownThisRun = true;
            Showing = true;
            var go = new GameObject("Objective Banner", typeof(RectTransform));
            go.layer = 0;
            go.transform.SetParent(host, false);
            go.AddComponent<StageObjectiveBanner>();
        }

        void OnDestroy() => Showing = false;

        IEnumerator Start()
        {
            var root = (RectTransform)transform;
            root.anchorMin = new Vector2(0f, 0.5f);
            root.anchorMax = new Vector2(1f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, 60f);
            root.sizeDelta = new Vector2(0f, 190f);
            root.SetAsLastSibling();
            var group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // No box: just a soft dark glow behind the words so they read on any background.
            var halo = Box("Glow", root, new Color(0.02f, 0.04f, 0.12f, 0.85f));
            halo.sizeDelta = new Vector2(1700f, 420f);
            halo.GetComponent<Image>().sprite = SoftSprite();

            var label = Label(root, "<color=#FFC814>MISSION</color>", new Vector2(0f, 46f), 30f);
            var mission = Label(root, "연구소에서 <color=#FFC814>무사히 탈출</color>하세요!", new Vector2(0f, -14f), 62f);
            mission.fontStyle = FontStyles.Bold;

            // Give the door fade a beat before sliding in.
            for (float d = 0f; d < 0.6f; d += Time.unscaledDeltaTime) yield return null;

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / In)
            {
                float e = 1f - Mathf.Pow(1f - t, 3f);
                group.alpha = e;
                root.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, e), 1f);            // band opens like a shutter
                mission.rectTransform.anchoredPosition = new Vector2((1f - e) * 500f, -14f); // text slides in
                label.rectTransform.anchoredPosition = new Vector2((1f - e) * -500f, 46f);
                yield return null;
            }
            group.alpha = 1f;
            root.localScale = Vector3.one;
            mission.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 46f);

            for (float t = 0f; t < Hold; t += Time.unscaledDeltaTime) yield return null;

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Out)
            {
                group.alpha = 1f - t;
                root.localScale = new Vector3(1f, 1f - 0.6f * t * t, 1f);
                yield return null;
            }
            Destroy(gameObject);
        }

        static Sprite softSprite;

        internal static Sprite SoftSprite()
        {
            if (softSprite) return softSprite;
            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d))));
            }
            tex.SetPixels32(px);
            tex.Apply();
            softSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            softSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return softSprite;
        }

        static RectTransform Box(string name, Transform parent, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static void Stretch(RectTransform rect, float x, float y)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(x, y);
            rect.offsetMax = new Vector2(-x, -y);
        }

        static TMP_Text Label(Transform parent, string content, Vector2 position, float size)
        {
            var rect = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1500f, size * 1.4f);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
