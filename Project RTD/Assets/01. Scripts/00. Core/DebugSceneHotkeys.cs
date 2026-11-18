using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRTD.Core
{
    [DisallowMultipleComponent]
    public sealed class DebugSceneHotkeys : MonoBehaviour
    {
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

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                SceneFlow.LoadStage(1);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                SceneFlow.LoadStage(2);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                SceneFlow.LoadStage(3);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            {
                SceneFlow.LoadStage(4);
            }
#endif
        }
    }
}
