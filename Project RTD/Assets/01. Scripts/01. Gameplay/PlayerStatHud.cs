using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // Left-side stat column: shows the current build's 1–5 levels and pulses the icon glow.
    [DisallowMultipleComponent]
    public sealed class PlayerStatHud : MonoBehaviour
    {
        [SerializeField] private PlayerPartLoadout loadout;
        [SerializeField] private TMP_Text weightText, speedText, healthText, jumpText;
        [SerializeField] private Graphic[] glows;
        [SerializeField, Range(0f, 1f)] private float glowMin = 0.35f, glowMax = 0.85f;
        [SerializeField] private float pulseSpeed = 2.2f;

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
            Set(healthText, build?.Health);
            Set(jumpText, build?.Jump);
        }

        private static void Set(TMP_Text text, int? level)
        {
            if (text) text.text = level.HasValue ? level.Value.ToString() : "-";
        }
    }
}
