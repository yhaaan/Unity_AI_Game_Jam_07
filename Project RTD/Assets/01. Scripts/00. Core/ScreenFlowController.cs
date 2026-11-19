using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRTD.Core
{
    [DisallowMultipleComponent]
    public sealed class ScreenFlowController : MonoBehaviour
    {
        public enum ConfirmAction
        {
            None,
            StartStage01,
            ReturnToTitle
        }

        [SerializeField] private ConfirmAction confirmAction;

        private static bool titleIntroShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTitleIntro() => titleIntroShown = false;

        private System.Collections.IEnumerator Start()
        {
            if (confirmAction == ConfirmAction.ReturnToTitle && gameObject.scene.name == SceneFlow.EndingScene)
            {
                var summary = GameObject.Find("/Ending Canvas");
                if (summary) summary.SetActive(false);
                // The old Enter/Space handler and the title button must not consume credit-card input.
                confirmAction = ConfirmAction.None;
                var roll = new GameObject("Ending Credits").AddComponent<ChimeraGroove.Ending.EndingCreditsPlayer>();
                roll.transform.SetParent(transform, false);
                roll.Finished += FinishCredits;
                void FinishCredits()
                {
                    roll.Finished -= FinishCredits;
                    roll.enabled = false; // keep the final card visible without accepting further input
                    StartCoroutine(FadeToPrologue());
                }
                yield break;
            }
            if (confirmAction != ConfirmAction.StartStage01) yield break;

            var logoObject = GameObject.Find("/Title Canvas/Title Logo");
            if (logoObject == null || !logoObject.TryGetComponent<RectTransform>(out var logo)) yield break;
            // The logo and the Start / Exit buttons are placed in TitleScene itself; only the intro runs here.
            if (titleIntroShown) yield break;
            titleIntroShown = true;
            var group = logoObject.AddComponent<CanvasGroup>();
            const float duration = 0.45f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                group.alpha = progress;
                logo.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, progress);
                yield return null;
            }
            group.alpha = 1f;
            logo.localScale = Vector3.one;
        }

        private System.Collections.IEnumerator FadeToPrologue()
        {
            var overlay = new GameObject("Credits fade", typeof(Canvas)).GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = short.MaxValue;
            var curtain = new GameObject("Curtain", typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            curtain.rectTransform.SetParent(overlay.transform, false);
            curtain.rectTransform.anchorMin = Vector2.zero;
            curtain.rectTransform.anchorMax = Vector2.one;
            curtain.rectTransform.offsetMin = curtain.rectTransform.offsetMax = Vector2.zero;
            const float duration = .4f;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                curtain.color = new Color(0, 0, 0, Mathf.SmoothStep(0, 1, t / duration));
                yield return null;
            }
            curtain.color = Color.black;
            ResetTitleIntro();
            SceneFlow.LoadPrologue();
        }

        public void Configure(ConfirmAction action)
        {
            confirmAction = action;
        }

        public void StartStage01()
        {
            SoundManager.Play(Sfx.Confirm);
            GameStartIntro.Run(SceneFlow.StartNewGame);
        }

        public void ReturnToTitle()
        {
            SoundManager.Play(Sfx.Confirm);
            SceneFlow.LoadTitle();
        }

        public void OpenLevelSelect()
        {
            SoundManager.Play(Sfx.Confirm);
            SceneFlow.LoadLevelSelect();
        }

        public void QuitGame()
        {
            SoundManager.Play(Sfx.Confirm);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || (!keyboard.enterKey.wasPressedThisFrame && !keyboard.spaceKey.wasPressedThisFrame))
            {
                return;
            }

            switch (confirmAction)
            {
                case ConfirmAction.StartStage01:
                    StartStage01();
                    break;
                case ConfirmAction.ReturnToTitle:
                    ReturnToTitle();
                    break;
            }
        }
    }
}
