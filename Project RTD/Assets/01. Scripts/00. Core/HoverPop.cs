using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectRTD.Core
{
    // Grows a menu button slightly while hovered; uses unscaled time so it works on a paused game.
    [DisallowMultipleComponent]
    public sealed class HoverPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HoverScale = 1.08f, Speed = 14f;
        private float target = 1f;

        public void OnPointerEnter(PointerEventData _) => target = HoverScale;
        public void OnPointerExit(PointerEventData _) => target = 1f;

        private void OnDisable()
        {
            target = 1f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
