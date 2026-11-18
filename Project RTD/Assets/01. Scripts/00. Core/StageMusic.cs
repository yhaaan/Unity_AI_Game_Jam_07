using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Core
{
    // Drop on any object in a scene that should have background music.
    // One persistent player keeps the track going across restarts of the same stage (R / respawn reloads),
    // crossfades when a scene asks for a different track, and fades out in scenes that ask for none.
    [DisallowMultipleComponent]
    public sealed class StageMusic : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.45f;

        private void Awake() => MusicPlayer.Request(clip, volume);
    }

    internal sealed class MusicPlayer : MonoBehaviour
    {
        private const float FadeSeconds = 0.8f;
        private static MusicPlayer instance;
        private AudioSource source;
        private float targetVolume;
        private Coroutine fade;

        public static void Request(AudioClip clip, float volume)
        {
            if (!clip) return;
            Ensure();
            if (instance.source.clip == clip && instance.source.isPlaying)
            {
                instance.FadeTo(volume);
                return;
            }
            instance.Switch(clip, volume);
        }

        private static void Ensure()
        {
            if (instance) return;
            var go = new GameObject(nameof(MusicPlayer));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MusicPlayer>();
            instance.source = go.AddComponent<AudioSource>();
            instance.source.loop = true;
            instance.source.playOnAwake = false;
            instance.source.spatialBlend = 0f;
            instance.source.volume = 0f;
            SceneManager.sceneLoaded += instance.OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // StageMusic.Awake has already run for this scene by now; no StageMusic means no music here.
            if (mode == LoadSceneMode.Single && !FindAnyObjectByType<StageMusic>()) FadeTo(0f, stopAtEnd: true);
        }

        private void Switch(AudioClip clip, float volume)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(SwitchRoutine(clip, volume));
        }

        private IEnumerator SwitchRoutine(AudioClip clip, float volume)
        {
            if (source.isPlaying) yield return Fade(0f);
            source.clip = clip;
            source.volume = 0f;
            source.Play();
            yield return Fade(volume);
            fade = null;
        }

        private void FadeTo(float volume, bool stopAtEnd = false)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeRoutine(volume, stopAtEnd));
        }

        private IEnumerator FadeRoutine(float volume, bool stopAtEnd)
        {
            yield return Fade(volume);
            if (stopAtEnd) source.Stop();
            fade = null;
        }

        private IEnumerator Fade(float to)
        {
            targetVolume = to;
            float from = source.volume;
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(from, targetVolume, t / FadeSeconds);
                yield return null;
            }
            source.volume = targetVolume;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }
}
