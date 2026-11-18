using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Core
{
    public static class GameTypography
    {
        private static Font guiFont;
        public static Font GuiFont => guiFont ? guiFont : guiFont = Resources.Load<Font>("Engcomica");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= Apply;
            SceneManager.sceneLoaded += Apply;
        }

        // Older scenes serialize their own TMP font and atlas materials, bypassing the TMP default.
        private static void Apply(Scene scene, LoadSceneMode mode)
        {
            var font = TMP_Settings.defaultFontAsset;
            if (!font) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.font == font) continue;
                    label.font = font;
                    label.fontSharedMaterial = font.material;
                }
        }
    }
}
