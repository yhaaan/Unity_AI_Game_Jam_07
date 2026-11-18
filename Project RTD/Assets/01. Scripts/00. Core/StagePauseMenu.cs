using UnityEngine;
using UnityEngine.UI;
using static ProjectRTD.Core.MenuArtView;

namespace ProjectRTD.Core
{
    // Esc opens PAUSED (resume / restart / settings / quit); SETTINGS holds music and effects volume.
    public sealed class StagePauseMenu : MonoBehaviour
    {
        // UV regions of pause.png (panel on the left, title and buttons stacked on the right).
        private static readonly Rect PanelUv = new(.0485f,.0213f,.4393f,.9575f);
        private static readonly Rect PausedUv = new(.5380f,.7343f,.4093f,.2285f);
        private static readonly Rect ResumeUv = new(.6056f,.5643f,.2609f,.1498f);
        private static readonly Rect RestartUv = new(.6110f,.3964f,.2555f,.1435f);
        private static readonly Rect SettingsUv = new(.5458f,.2051f,.3896f,.1902f);
        private static readonly Rect QuitUv = new(.6080f,.0276f,.2597f,.1403f);
        // settings.png title, sound icon.png (on / muted), sound bar.png (empty / full bolt), handle.png knob.
        private static readonly Rect SettingsTitleUv = new(.0300f,.1004f,.9455f,.7730f);
        private static readonly Rect SpeakerOnUv = new(.1146f,.1146f,.2910f,.7431f);
        private static readonly Rect SpeakerMutedUv = new(.6151f,.1519f,.2785f,.6796f);
        private static readonly Rect BarEmptyUv = new(.1160f,.4931f,.8016f,.3591f);
        private static readonly Rect BarFullUv = new(.1160f,.0608f,.8016f,.3591f);
        private static readonly Rect KnobUv = new(.2480f,.1906f,.4976f,.5965f);

        private RectTransform modal, pausePage, settingsPage;
        private float previousScale;
        private bool paused;

        [SerializeField] private SharedMenuArt menuArt;

        private void Awake()
        {
            if (!menuArt)
            {
                Debug.LogError("Stage HUD에 SharedMenuArt가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            Build(menuArt);
        }

        private void Build(SharedMenuArt art)
        {
            modal = Box(transform, "Pause menu", 0,0,1,1);
            // Render all screen UI (including the full-screen fade) after world sprites and the water pass.
            GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            if (transform.Find("Controls") is RectTransform rect && rect.TryGetComponent<TMPro.TMP_Text>(out var hint))
            {
                rect.anchorMin = new Vector2(.22f, .86f);
                rect.anchorMax = new Vector2(.84f, .99f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                hint.text = "SHIFT : REASSEMBLE     R : RESTART\n<size=60%>A/D : MOVE     SPACE : JUMP / GLIDE</size>";
                hint.enableAutoSizing = true;
                hint.fontSizeMin = 18;
                hint.fontSizeMax = 52;
                hint.alignment = TMPro.TextAlignmentOptions.Center;
                hint.raycastTarget = false;
            }
            var layer = modal.gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = 1000;
            modal.gameObject.AddComponent<GraphicRaycaster>();
            var dim = modal.gameObject.AddComponent<Image>();
            dim.color = new Color(0,0,0,.7f);
            var outside = modal.gameObject.AddComponent<Button>(); // clicking outside the panel goes back
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(Back);

            // Positions below are fractions of the panel art, so the layout keeps its shape at any resolution.
            pausePage = Page(art, "Pause");
            Cropped(pausePage, art.pause, "Paused title", PausedUv, .04f,.66f,.92f,.24f);
            CroppedAction(pausePage, art.pause, "Resume", ResumeUv, .30f,.43f,.40f,.105f, Close);
            CroppedAction(pausePage, art.pause, "Restart", RestartUv, .30f,.31f,.40f,.105f, Restart);
            CroppedAction(pausePage, art.pause, "Settings", SettingsUv, .20f,.165f,.60f,.135f, ShowSettings);
            CroppedAction(pausePage, art.pause, "Quit", QuitUv, .30f,.045f,.40f,.10f, Quit);

            settingsPage = Page(art, "Settings");
            Cropped(settingsPage, art.settings, "Settings title", SettingsTitleUv, -.02f,.66f,1.04f,.24f);
            VolumeRow(settingsPage, art, "Music volume", .42f, () => GameVolume.Music, v => GameVolume.Music = v);
            VolumeRow(settingsPage, art, "Effects volume", .16f, () => GameVolume.Effects, v => GameVolume.Effects = v);

            modal.gameObject.SetActive(false);
        }

        private RectTransform Page(SharedMenuArt art, string name)
        {
            var panel = Cropped(modal, art.pause, name, PanelUv, 0,0,1,1);
            // Match the screen height, keeping the sheet's aspect ratio and the controls centered.
            var fit = panel.GetComponent<AspectRatioFitter>();
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(.5f, 0);
            rt.anchorMax = new Vector2(.5f, 1);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            fit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            panel.raycastTarget = true;
            // Clicks bubble to the nearest click handler: this inert one keeps panel clicks off the backdrop's Back.
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            return panel.rectTransform;
        }

        // Speaker icon (click to mute / unmute) + lightning-bolt slider; the full bolt is revealed up to the value.
        private static void VolumeRow(RectTransform page, SharedMenuArt art, string name, float y,
            System.Func<float> get, System.Action<float> set)
        {
            var row = Box(page, name, 0,y,1,.17f);
            var on = Cropped(row, art.speaker, "Speaker", SpeakerOnUv, .02f,0,.24f,1);
            var muted = Cropped(row, art.speaker, "Muted", SpeakerMutedUv, .02f,0,.24f,1);

            var bar = Cropped(row, art.track, "Slider", BarEmptyUv, .28f,.22f,.78f,.56f).rectTransform;
            var fillArea = Box(bar, "Fill Area", 0,0,1,1);
            var fill = Box(fillArea, "Fill", 0,0,1,1).gameObject.AddComponent<RawImage>();
            fill.texture = art.track;
            fill.raycastTarget = false;
            var handleArea = Box(bar, "Handle Slide Area", 0,-.25f,1,1.5f);
            var handle = Box(handleArea, "Handle", 0,0,0,1);
            var knob = Picture(handle, art.knob, false);
            knob.uvRect = KnobUv;
            knob.raycastTarget = true;
            var knobFit = handle.gameObject.AddComponent<AspectRatioFitter>();
            knobFit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            knobFit.aspectRatio = art.knob.width * KnobUv.width / (art.knob.height * KnobUv.height);

            bar.GetComponent<RawImage>().raycastTarget = true;
            var slider = bar.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.transition = Selectable.Transition.None;

            float lastHeard = get() > 0 ? get() : 1f;
            void Show(float value)
            {
                fill.uvRect = new Rect(BarFullUv.x, BarFullUv.y, BarFullUv.width * value, BarFullUv.height);
                on.gameObject.SetActive(value > 0);
                muted.gameObject.SetActive(value <= 0);
            }
            slider.onValueChanged.AddListener(value =>
            {
                if (value > 0) lastHeard = value;
                set(value);
                Show(value);
            });
            slider.SetValueWithoutNotify(get());
            Show(get());

            foreach (var icon in new[] { on, muted })
            {
                icon.raycastTarget = true;
                icon.gameObject.AddComponent<HoverPop>();
                icon.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    SoundManager.Play(Sfx.Confirm);
                    slider.value = slider.value > 0 ? 0 : lastHeard;
                });
            }
        }


        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (paused) Back();
            else OpenMenu(false);
        }

        private void OpenMenu(bool showSettings)
        {
            if (paused || Time.timeScale <= 0) return;
            previousScale = Time.timeScale;
            paused = true;
            Time.timeScale = 0;
            modal.SetAsLastSibling();
            modal.gameObject.SetActive(true);
            ShowPage(showSettings);
        }

        private void ShowPage(bool settings)
        {
            pausePage.gameObject.SetActive(!settings);
            settingsPage.gameObject.SetActive(settings);
        }

        private void ShowSettings()
        {
            SoundManager.Play(Sfx.Confirm);
            ShowPage(true);
        }

        // Esc / click outside: settings -> pause page -> back to the game.
        private void Back()
        {
            if (settingsPage && settingsPage.gameObject.activeSelf) ShowPage(false);
            else Close();
        }

        private void Close()
        {
            if (!paused) return;
            paused = false;
            Time.timeScale = previousScale;
            if (modal) modal.gameObject.SetActive(false);
        }

        private void Restart()
        {
            Close();
            SoundManager.Play(Sfx.Confirm);
            SceneFlow.RestartActiveScene();
        }

        private void Quit()
        {
            Close();
            SoundManager.Play(Sfx.Confirm);
            SceneFlow.LoadTitle();
        }

        private void OnDisable() => Close();
    }
}
