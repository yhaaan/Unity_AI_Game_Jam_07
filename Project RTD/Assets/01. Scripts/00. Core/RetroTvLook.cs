using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace ProjectRTD.Core
{
    public sealed class RetroTvLook : MonoBehaviour
    {
        private VolumeProfile profile;
        private Vignette vignette;
        private FilmGrain grain;
        private ColorAdjustments color;
        private LensDistortion lens;
        private ChromaticAberration colorFringe;
        private WhiteBalance filmTone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject("Retro TV Look");
            DontDestroyOnLoad(host);
            host.AddComponent<RetroTvLook>();
        }

        private void Awake()
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>();
            grain = profile.Add<FilmGrain>();
            color = profile.Add<ColorAdjustments>();
            lens = profile.Add<LensDistortion>();
            colorFringe = profile.Add<ChromaticAberration>();
            filmTone = profile.Add<WhiteBalance>();
            RestoreDefaults();

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var camera = Camera.main;
            if (camera == null) return;

            // The title art is already finished key art: show it clean, without the CRT treatment.
            bool isTitle = scene.name == SceneFlow.TitleScene;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = !isTitle;
        }

        private void RestoreDefaults()
        {
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.65f);
            grain.intensity.Override(0.38f);
            // Warm, faded animation cels with enough ink contrast to read at game scale.
            color.saturation.Override(-14f);
            color.contrast.Override(12f);
            filmTone.temperature.Override(16f);

            // A stationary curved tube: keep the centre stable and let the edges split slightly.
            lens.intensity.Override(0.22f);
            colorFringe.intensity.Override(0.12f);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (profile == null) return;
            foreach (var component in profile.components) Destroy(component);
            Destroy(profile);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool showPanel;
        private Vector2 panelScroll;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
                showPanel = !showPanel;
        }

        private void OnGUI()
        {
            if (!showPanel) return;
            const float scale = 2f;
            var previousMatrix = GUI.matrix;
            try
            {
                GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                DrawPanel(scale);
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        private void DrawPanel(float scale)
        {
            float width = Mathf.Min(280f, (Screen.width - 16f) / scale);
            float height = Mathf.Min(390f, (Screen.height - 16f) / scale);
            GUILayout.BeginArea(new Rect((Screen.width - 8f) / scale - width, 8f / scale, width, height), GUI.skin.box);
            GUILayout.Label("Retro TV  [F6]");
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            Slider("CRT curvature", lens.intensity);
            Slider("Edge color fringe", colorFringe.intensity);
            Slider("Film warmth", filmTone.temperature);
            Slider("Ink contrast", color.contrast);
            Slider("Vignette", vignette.intensity);
            Slider("Edge softness", vignette.smoothness);
            Slider("Film grain", grain.intensity);
            Slider("Saturation", color.saturation);
            GUILayout.EndScrollView();
            if (GUILayout.Button("기본값으로")) RestoreDefaults();
            GUILayout.EndArea();
        }

        private static void Slider(string label, ClampedFloatParameter parameter)
        {
            GUILayout.Label($"{label}: {parameter.value:0.00}");
            parameter.value = GUILayout.HorizontalSlider(parameter.value, parameter.min, parameter.max);
        }
#endif
    }
}
