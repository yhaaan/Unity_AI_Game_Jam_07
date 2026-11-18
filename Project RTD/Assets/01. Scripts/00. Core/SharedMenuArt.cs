using UnityEngine;

namespace ProjectRTD.Core
{
    // Shared PNGs are textures; keep their import settings and aspect ratios intact.
    public sealed class SharedMenuArt : ScriptableObject
    {
        public Texture2D title, brand, subtitle, leftPrint, rightPrint, available, locked;
        public Texture2D panel, resumePanel, backPanel, pause, resume, settings, speaker, track, knob;
        public Texture2D labIllustration, uiIllustration;

        public static SharedMenuArt Loaded
        {
            get
            {
                var banks = Resources.FindObjectsOfTypeAll<SharedMenuArt>();
                return banks.Length == 1 ? banks[0] : null;
            }
        }
    }
}
