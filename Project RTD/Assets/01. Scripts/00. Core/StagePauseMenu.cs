using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ProjectRTD.Core.MenuArtView;

namespace ProjectRTD.Core
{
    public sealed class StagePauseMenu : MonoBehaviour
    {
        private RectTransform modal, audioControls;
        private float previousScale;
        private bool paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        private static void Install(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.StartsWith("Stage", System.StringComparison.Ordinal)) return;
            var art = SharedMenuArt.Loaded;
            if (!art) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>())
                    if (canvas.name == "Stage HUD")
                    {
                        canvas.gameObject.AddComponent<StagePauseMenu>().Build(art);
                        return;
                    }
        }

        private void Build(SharedMenuArt art)
        {
            Action(transform, art.pause, "Pause", .94f,.91f,.05f,.08f, Open);
            modal = Box(transform, "Pause menu", 0,0,1,1);
            modal.gameObject.AddComponent<Image>().color = new Color(0,0,0,.8f);
            Art(modal, art.uiIllustration, .17f,.22f,.32f,.56f);
            Art(modal, art.panel, .49f,.22f,.34f,.56f,false);
            Action(modal, art.resume, "Resume", .59f,.59f,.14f,.14f, Close);
            Action(modal, art.settings, "Sound settings", .6f,.44f,.12f,.12f, ToggleSettings);
            audioControls = Box(modal,"Master volume",.53f,.29f,.26f,.11f);
            Art(audioControls,art.speaker,0,0,.2f,1);
            var bar = Box(audioControls,"Volume slider",.22f,.1f,.76f,.8f);
            var trackImage = Art(bar,art.track,0,0,1,1,false);
            trackImage.raycastTarget = true;
            var handleArea = Box(bar,"Travel",.05f,0,.9f,1);
            var handle = Box(handleArea,"Handle",0,0,0,1);
            handle.sizeDelta = new Vector2(30,0);
            var thumb = Art(handle,art.knob,0,0,1,1);
            thumb.raycastTarget = true;
            var slider = bar.gameObject.AddComponent<Slider>();
            slider.handleRect = handle;
            slider.targetGraphic = thumb;
            slider.SetValueWithoutNotify(AudioListener.volume);
            slider.onValueChanged.AddListener(value => AudioListener.volume = value);
            modal.gameObject.SetActive(false);
        }

        private void ToggleSettings() => audioControls.gameObject.SetActive(!audioControls.gameObject.activeSelf);

        private void Open()
        {
            if (paused || Time.timeScale <= 0) return;
            previousScale = Time.timeScale;
            paused = true;
            Time.timeScale = 0;
            modal.SetAsLastSibling();
            modal.gameObject.SetActive(true);
            audioControls.gameObject.SetActive(false);
        }

        private void Close()
        {
            if (!paused) return;
            paused = false;
            Time.timeScale = previousScale;
            if (modal) modal.gameObject.SetActive(false);
        }

        private void OnDisable() => Close();
    }
}
