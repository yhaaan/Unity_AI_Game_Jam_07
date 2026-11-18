using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectRTD.Core
{
    /// <summary>
    /// Masks the whole game picture into an old TV tube: a slightly bulging rounded rectangle (superellipse),
    /// dark bezel outside, a soft inner shadow and a faint glass sheen. Every scene except the title.
    /// Drawn as a top-most overlay, regenerated when the window size changes.
    /// </summary>
    public sealed class TvScreenFrame : MonoBehaviour
    {
        [Tooltip("bezel width as a fraction of the screen height")] public float margin = .035f;
        [Tooltip("roundness: higher = squarer corners and flatter edges (4 = very round, 8 = boxy)")] public float squareness = 5.5f;
        public Color bezel = new Color32(0x16, 0x14, 0x1c, 0xff);
        [Range(0f, 1f)] public float innerShadow = .45f;
        [Range(0f, 1f)] public float sheen = .07f;

        static TvScreenFrame instance;
        Canvas canvas;
        RawImage image;
        Texture2D tex;
        int builtW, builtH;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (instance) return;
            var go = new GameObject("TV Screen Frame");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<TvScreenFrame>();
        }

        void Awake()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;                         // over the game and its HUD
            var imgGo = new GameObject("Mask");
            imgGo.transform.SetParent(transform, false);
            image = imgGo.AddComponent<RawImage>();
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (tex) Destroy(tex);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

        void Apply(Scene scene)
        {
            bool show = scene.name != SceneFlow.TitleScene;      // the title keeps its full-bleed key art
            canvas.enabled = show;
            if (show) Rebuild();
        }

        void Update()
        {
            if (canvas.enabled && (Screen.width / 2 != builtW || Screen.height / 2 != builtH)) Rebuild();
        }

        /// <summary>Builds the mask at half resolution (bilinear upscale keeps the edge smooth).</summary>
        public void Rebuild()
        {
            int w = Mathf.Max(64, Screen.width / 2), h = Mathf.Max(36, Screen.height / 2);
            builtW = Screen.width / 2; builtH = Screen.height / 2;
            if (tex == null || tex.width != w || tex.height != h)
            {
                if (tex) Destroy(tex);
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "TV mask" };
            }
            float m = margin * h;
            float a = w / 2f - m, b = h / 2f - m;                 // superellipse half extents
            float n = squareness, shadowW = .05f * h;
            var px = new Color32[w * h];
            var bz = (Color32)bezel;
            for (int y = 0; y < h; y++)
            {
                float dy = Mathf.Abs(y + .5f - h / 2f) / b;
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x + .5f - w / 2f) / a;
                    // f < 1 inside the tube; distance to the edge in pixels ≈ (1 - f^(1/n)) * min(a, b)
                    float f = Mathf.Pow(Mathf.Pow(dx, n) + Mathf.Pow(dy, n), 1f / n);
                    float dist = (1f - f) * Mathf.Min(a, b);       // >0 inside
                    float outside = Mathf.Clamp01(.5f - dist);     // 1 px anti-aliased edge
                    // inner shadow: the glass curves away from the viewer near the edge
                    float sh = innerShadow * Mathf.Pow(Mathf.Clamp01(1f - dist / shadowW), 2f);
                    // faint sheen across the top-left of the glass
                    float u = (x + .5f) / w, v = (y + .5f) / h;
                    float gl = sheen * Mathf.Clamp01(1f - Mathf.Abs((u - .2f) - (v - .78f) * .9f) / .16f) * Mathf.Clamp01((v - .45f) * 3f) * (1f - outside);
                    float alpha = Mathf.Max(outside, sh * (1f - outside));
                    Color32 c;
                    if (gl > alpha) c = new Color32(255, 255, 255, (byte)(255 * gl));
                    else c = new Color32(bz.r, bz.g, bz.b, (byte)(255 * alpha));
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            image.texture = tex;
        }
    }
}
