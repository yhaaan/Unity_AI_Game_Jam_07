using UnityEngine;

namespace ProjectRTD.Gameplay
{
    internal sealed class CardTurn
    {
        private const float Duration = .08f;
        private readonly Transform visual;
        private readonly float fullWidth;
        private float phase, target;

        public CardTurn(Transform visual)
        {
            this.visual = visual;
            fullWidth = Mathf.Abs(visual.localScale.x);
            phase = target = Mathf.Sign(visual.localScale.x);
        }

        public void Tick(float direction, float dt)
        {
            if (Mathf.Abs(direction) >= .01f) target = direction > 0f ? -1f : 1f;
            if (phase == target) return;
            phase = Mathf.MoveTowards(phase, target, 2f * dt / Duration);
            float width = Mathf.Sin(phase * Mathf.PI * .5f);
            var scale = visual.localScale;
            // Keep the visual transform invertible even on the exact edge-on frame.
            scale.x = fullWidth * Mathf.Sign(width) * Mathf.Max(.001f, Mathf.Abs(width));
            visual.localScale = scale;
        }
    }
}
