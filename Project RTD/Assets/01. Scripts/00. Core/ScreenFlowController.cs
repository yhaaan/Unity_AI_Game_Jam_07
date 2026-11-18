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

        public void Configure(ConfirmAction action)
        {
            confirmAction = action;
        }

        public void StartStage01()
        {
            SoundManager.Play(Sfx.Confirm);
            SceneFlow.LoadStage01();
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
