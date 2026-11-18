using UnityEngine;

namespace ProjectRTD.Core
{
    // Shared PNGs are textures; keep their import settings and aspect ratios intact.
    public sealed class SharedMenuArt : ScriptableObject
    {
        public Texture2D title, brand, subtitle, leftPrint, rightPrint, available, locked;
        public Texture2D panel, resumePanel, backPanel, pause, resume, settings, speaker, track, knob;
        public Texture2D labIllustration, uiIllustration;
        public Texture2D startButton, exitButton;
        public Texture2D reroll, crate, cupboardLeft, cupboardRight, plateSheet, doorSheet;
        public Vector2Int plateGrid, doorGrid; // Columns, rows; frames are read left to right from the top row.
        public Sprite labFloor; // floor.png의 바닥 띠(floor_83). 윗테두리는 9-slice로 두께 유지, 아래 면만 늘어남.
        public RectInt crateBody; // Opaque pixels of the crate PNG (bottom-left origin); the crate collider matches it.

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
