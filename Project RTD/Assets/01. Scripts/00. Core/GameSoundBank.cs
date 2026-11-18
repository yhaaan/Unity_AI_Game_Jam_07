using System;
using UnityEngine;

namespace ProjectRTD.Core
{
    // Registered in Player Settings > Preloaded Assets so the initial scene doesn't matter.
    public sealed class GameSoundBank : ScriptableObject
    {
        [Serializable]
        private struct Entry { public Sfx cue; public AudioClip clip; }
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public AudioClip GetClip(Sfx cue)
        {
            foreach (var entry in entries)
                if (entry.cue == cue) return entry.clip;
            return null;
        }
    }
}
