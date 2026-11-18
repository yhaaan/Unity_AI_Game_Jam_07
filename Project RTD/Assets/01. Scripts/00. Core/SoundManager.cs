using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTD.Core
{
    public enum Sfx { Confirm, Jump, Land, Splash, Scare, Defeat, PlateDown, PlateUp, Respawn, Goal, Summon }

    public sealed class SoundManager : MonoBehaviour
    {
        private static SoundManager instance;
        private readonly Dictionary<Sfx, AudioClip> clips = new();
        private readonly List<AudioClip> generated = new();
        private readonly Dictionary<Sfx, float> lastPlayed = new();
        private AudioSource source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!instance) new GameObject(nameof(SoundManager)).AddComponent<SoundManager>();
        }

        private void Awake()
        {
            if (instance && instance != this) { Destroy(this); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = .65f;
            foreach (Sfx cue in System.Enum.GetValues(typeof(Sfx)))
            {
                var clip = Resources.Load<AudioClip>("SFX/" + cue);
                if (!clip) clip = Resources.Load<AudioClip>("RTD_SFX_" + cue);
                if (!clip) { clip = RetroSfx.Create(cue); generated.Add(clip); }
                clips.Add(cue, clip);
            }
        }

        public static void Play(Sfx cue)
        {
            if (!instance || !instance.clips.TryGetValue(cue, out var clip)) return;
            float now = Time.unscaledTime;
            if (instance.lastPlayed.TryGetValue(cue, out float last) && now - last < .06f) return;
            instance.lastPlayed[cue] = now;
            instance.source.PlayOneShot(clip);
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            foreach (var clip in generated) Destroy(clip);
        }
    }
}
