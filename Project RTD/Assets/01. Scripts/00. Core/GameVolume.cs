using UnityEngine;

namespace ProjectRTD.Core
{
    // Player-facing music / effects levels (0..1), saved between sessions.
    public static class GameVolume
    {
        private const string MusicKey = "Volume.Music", EffectsKey = "Volume.Effects";

        public static event System.Action Changed;

        public static float Music
        {
            get => PlayerPrefs.GetFloat(MusicKey, 1f);
            set => Set(MusicKey, value);
        }

        public static float Effects
        {
            get => PlayerPrefs.GetFloat(EffectsKey, 1f);
            set => Set(EffectsKey, value);
        }

        private static void Set(string key, float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(PlayerPrefs.GetFloat(key, 1f), value)) return;
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
