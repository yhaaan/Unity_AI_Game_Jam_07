using ChimeraGroove.Prologue;
using ProjectRTD.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // Click / Space / Enter / → advances a cut. Finishing or skipping opens the title.
    [DisallowMultipleComponent]
    public sealed class PrologueFlow : MonoBehaviour
    {
        [SerializeField] private ProloguePlayer player;
        [SerializeField] private Button skipButton;

        private bool leaving;

        private void Start()
        {
            player.handleInput = false; // input is routed here so the SKIP click doesn't also advance a cut
            player.Finished += Leave;
            skipButton.onClick.AddListener(Leave);
        }

        private void OnDestroy()
        {
            if (player) player.Finished -= Leave;
        }

        private void Update()
        {
            if (leaving) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Leave(); return; }
            if (PressedNext(keyboard)) player.Next();
        }

        private static bool PressedNext(Keyboard keyboard)
        {
            bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !overUi) return true;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame && !overUi) return true;
            return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame);
        }

        private void Leave()
        {
            if (leaving) return;
            leaving = true;
            SoundManager.Play(Sfx.Confirm);
            SceneFlow.LoadTitle();
        }
    }
}
