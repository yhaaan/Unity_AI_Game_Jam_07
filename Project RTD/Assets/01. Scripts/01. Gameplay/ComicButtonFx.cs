using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
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
