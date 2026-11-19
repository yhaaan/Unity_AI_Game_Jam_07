using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Gameplay
{
    // Legendary builds (불사조 / 용) have no art of their own yet: the current parts are tinted in the legend's colours
    // with a flickering glow behind the body. F7 forces a legend in the editor / development builds for testing.
    [DefaultExecutionOrder(1000), DisallowMultipleComponent, RequireComponent(typeof(PlayerPartLoadout))]
    public sealed class PlayerLegendAura : MonoBehaviour
    {
        static Sprite glowSprite;

        PlayerPartLoadout loadout;
        Transform visual;
        SpriteRenderer glow;
        bool tinted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (_, _) => Attach();
            Attach();
        }

        static void Attach()
        {
            foreach (var l in FindObjectsByType<PlayerPartLoadout>(FindObjectsSortMode.None))
                if (!l.GetComponent<PlayerLegendAura>()) l.gameObject.AddComponent<PlayerLegendAura>();
        }

        void Awake()
        {
            loadout = GetComponent<PlayerPartLoadout>();
            var part = GetComponentInChildren<PlayerPartVisual>(true);
            if (part) visual = part.transform;
        }

        void LateUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame && Time.timeScale > 0f) loadout.ForceLegend();
#endif
            string legend = loadout.Current?.Legend;
            if (legend == null)
            {
                if (tinted) { Paint(Color.white); tinted = false; if (glow) glow.enabled = false; }
                return;
            }
            float t = Time.time;
            // 불사조: fire that flickers between orange and gold. 용: jade green shimmering to violet.
            Color a, b;
            if (legend == "불사조") { a = new Color(1f, 0.5f, 0.22f); b = new Color(1f, 0.86f, 0.4f); }
            else { a = new Color(0.45f, 1f, 0.62f); b = new Color(0.75f, 0.6f, 1f); }
            var tint = Color.Lerp(a, b, 0.5f + 0.5f * Mathf.Sin(t * 6f));
            Paint(tint);
            tinted = true;

            if (!glow) BuildGlow();
            glow.enabled = true;
            var c = Color.Lerp(a, b, 0.5f + 0.5f * Mathf.Sin(t * 4f + 1f));
            c.a = 0.55f + 0.2f * Mathf.Sin(t * 9f);
            glow.color = c;
            glow.transform.localScale = Vector3.one * (3.4f + 0.3f * Mathf.Sin(t * 5f));
        }

        void Paint(Color color)
        {
            if (!visual) return;
            foreach (var sr in visual.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr == glow) continue;
                color.a = sr.color.a; // keep whatever fade the animation is doing
                sr.color = color;
            }
        }

        void BuildGlow()
        {
            if (!glowSprite)
            {
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
                }
                tex.SetPixels32(px);
                tex.Apply();
                glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                glowSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            glow = new GameObject("Legend Glow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            glow.sprite = glowSprite;
            glow.sortingOrder = -1; // behind the body
        }
    }
}
