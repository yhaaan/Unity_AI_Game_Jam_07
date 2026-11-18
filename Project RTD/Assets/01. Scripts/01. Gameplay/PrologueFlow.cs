using ChimeraGroove.Prologue;
using ProjectRTD.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // Runs the comic prologue before Stage01. Click / Space / Enter / → advances a cut; SKIP button or Esc jumps to the stage.
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
            SceneFlow.LoadStage01();
        }
    }

    // Comic-panel button feel: idle wobble, pops and tilts on hover, squashes on press.
    [DisallowMultipleComponent]
    public sealed class ComicButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Graphic fill;
        [SerializeField] private Color idleFill = new Color32(0xfb, 0xf3, 0xe2, 0xff);
        [SerializeField] private Color hoverFill = new Color32(0xff, 0xd1, 0x66, 0xff);
        [SerializeField] private float restTilt = -4f;

        private bool hover, pressed;
        private float scale = 1f, tilt;

        private void OnEnable() { tilt = restTilt; }

        private void Update()
        {
            float t = Time.unscaledTime;
            float targetScale = pressed ? 0.9f : hover ? 1.1f : 1f + 0.025f * Mathf.Sin(t * 3.2f);
            float targetTilt = hover ? restTilt + 6f * Mathf.Sin(t * 14f) * 0.35f : restTilt + 1.2f * Mathf.Sin(t * 2.1f);
            float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            scale = Mathf.Lerp(scale, targetScale, k);
            tilt = Mathf.Lerp(tilt, targetTilt, k);
            transform.localScale = new Vector3(scale, scale, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            if (fill) fill.color = Color.Lerp(fill.color, hover ? hoverFill : idleFill, k);
        }

        public void OnPointerEnter(PointerEventData _) => hover = true;
        public void OnPointerExit(PointerEventData _) { hover = false; pressed = false; }
        public void OnPointerDown(PointerEventData _) => pressed = true;
        public void OnPointerUp(PointerEventData _) => pressed = false;
    }
}
