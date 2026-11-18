using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // Shows the current build and remaining stage HP.
    [DisallowMultipleComponent]
    public sealed class PlayerStatHud : MonoBehaviour
    {
        [SerializeField] private PlayerPartLoadout loadout;
        [SerializeField] private TMP_Text weightText, speedText, healthText, jumpText;
        [SerializeField] private Graphic[] glows;
        [SerializeField, Range(0f, 1f)] private float glowMin = 0.35f, glowMax = 0.85f;
        [SerializeField] private float pulseSpeed = 2.2f;
        private StageRuntime stage;

        private void OnEnable()
        {
            if (!loadout) loadout = FindFirstObjectByType<PlayerPartLoadout>();
            if (!loadout) return;
            loadout.Changed += Show;
            Show(loadout.Current);
            BindHealth();
        }

        private void Start() => BindHealth();

        private void BindHealth()
        {
            if (stage || !loadout || !StageRuntime.Instance || StageRuntime.Instance.Player != loadout.transform) return;
            stage = StageRuntime.Instance;
            stage.HealthChanged += ShowHealth;
            ShowHealth();
        }

        private void OnDisable()
        {
            if (loadout) loadout.Changed -= Show;
            if (stage) stage.HealthChanged -= ShowHealth;
            stage = null;
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
            if (!stage) Set(healthText, build?.Health);
            Set(jumpText, build?.Jump);
        }

        private void ShowHealth()
        {
            if (healthText) healthText.text = $"{stage.Health}/{stage.MaxHealth}";
        }

        private static void Set(TMP_Text text, int? level)
        {
            if (text) text.text = level.HasValue ? level.Value.ToString() : "-";
        }
    }
}
