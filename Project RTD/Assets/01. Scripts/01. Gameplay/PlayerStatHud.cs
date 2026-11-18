using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // Shows the current build's movement stats.
    [DisallowMultipleComponent]
    public sealed class PlayerStatHud : MonoBehaviour
    {
        [SerializeField] private PlayerPartLoadout loadout;
        [SerializeField] private TMP_Text weightText, speedText, jumpText;
        [SerializeField] private Graphic[] glows;
        [SerializeField, Range(0f, 1f)] private float glowMin = 0.35f, glowMax = 0.85f;
        [SerializeField] private float pulseSpeed = 2.2f;
        private void Awake()
        {
            // Older stage canvases have manually positioned rows. Close the gap before the first frame.
            var obsolete = transform.Find("health") as RectTransform;
            if (!obsolete) return;
            if (transform.Find("jump") is RectTransform jump) jump.anchoredPosition = obsolete.anchoredPosition;
            ((RectTransform)transform).sizeDelta -= Vector2.up * obsolete.rect.height;
            obsolete.gameObject.SetActive(false);
            Destroy(obsolete.gameObject);
        }

        private void OnEnable()
        {
            if (!loadout) loadout = FindFirstObjectByType<PlayerPartLoadout>();
            if (!loadout) return;
            loadout.Changed += Show;
            Show(loadout.Current);

        }

        private void OnDisable()
        {
            if (loadout) loadout.Changed -= Show;

        }

        private void Update()
        {
            float alpha = Mathf.Lerp(glowMin, glowMax, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed));
            foreach (var glow in glows)
            {
                if (!glow) continue;
                var color = glow.color;
                color.a = alpha;
                glow.color = color;
            }
        }

        private void Show(PlayerPartBuild build)
        {
            Set(weightText, build?.Weight);
            Set(speedText, build?.Speed);

            Set(jumpText, build?.Jump);
        }


        private static void Set(TMP_Text text, int? level)
        {
            if (text) text.text = level.HasValue ? level.Value.ToString() : "-";
        }
    }
}
