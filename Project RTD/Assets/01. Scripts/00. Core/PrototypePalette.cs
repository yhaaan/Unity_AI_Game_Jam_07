using UnityEngine;

namespace ProjectRTD.Core
{
    /// <summary>
    /// Prototype art bible.
    /// Style: clean paper-cut platformer made from bold, flat Unity primitives.
    /// Palette: ink backdrop, cream surfaces, orange platforms, cyan player, blue water, coral danger, yellow goal.
    /// Mood: bright stage markers floating over a calm midnight-blue playfield.
    /// </summary>
    public static class PrototypePalette
    {
        public static Color Backdrop => FromHex(0x162033);
        public static Color BackdropSoft => FromHex(0x22304A);
        public static Color Surface => FromHex(0xE8DCC4);
        public static Color Platform => FromHex(0xC8782C);
        public static Color Player => FromHex(0x3DD6C6);
        public static Color Water => FromHex(0x3F8FE8);
        public static Color Danger => FromHex(0xE05263);
        public static Color Goal => FromHex(0xFFD166);
        public static Color Ink => FromHex(0x101726);

        private static Color FromHex(int rgb)
        {
            return new Color32(
                (byte)((rgb >> 16) & 0xFF),
                (byte)((rgb >> 8) & 0xFF),
                (byte)(rgb & 0xFF),
                0xFF);
        }
    }
}
