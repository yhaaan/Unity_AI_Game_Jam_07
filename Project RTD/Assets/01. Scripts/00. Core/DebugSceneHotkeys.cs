using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRTD.Core
{
    [DisallowMultipleComponent]
    public sealed class DebugSceneHotkeys : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly Key[] StageKeys =
            { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7 };
        private static readonly Key[] NumpadStageKeys =
            { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7 };
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (FindFirstObjectByType<DebugSceneHotkeys>() != null)
            {
                return;
            }

            GameObject host = new(nameof(DebugSceneHotkeys));
            DontDestroyOnLoad(host);
            host.AddComponent<DebugSceneHotkeys>();
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                SceneFlow.LoadTitle();
                return;
            }

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                SceneFlow.LoadEnding();
                return;
            }

            for (int i = 0; i < StageKeys.Length; i++)
            {
                if (!keyboard[StageKeys[i]].wasPressedThisFrame && !keyboard[NumpadStageKeys[i]].wasPressedThisFrame) continue;
                SceneFlow.LoadStage(i + 1);
                return;
            }
#endif
        }
    }
}
