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
            if (confirmAction != ConfirmAction.StartStage01) yield break;

            var logoObject = GameObject.Find("/Title Canvas/Title Logo");
            if (logoObject == null || !logoObject.TryGetComponent<RectTransform>(out var logo)) yield break;

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

        public void Configure(ConfirmAction action)
        {
            confirmAction = action;
        }

        public void StartStage01()
        {
            SoundManager.Play(Sfx.Confirm);
            SceneFlow.StartNewGame();
        }

        public void ReturnToTitle()
        {
            SoundManager.Play(Sfx.Confirm);
            SceneFlow.LoadTitle();
        }

        public void RestartCurrentScene()
        {
            SoundManager.Play(Sfx.Confirm);
            SceneFlow.RestartActiveScene();
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
