using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class StageStatGate2D : MonoBehaviour
    {
        public enum Condition { HighJump, LowCeiling }
        [SerializeField] private Condition condition;
        private PlayerPartLoadout loadout;
        private BoxCollider2D barrier;

        private void Start()
        {
            barrier = GetComponent<BoxCollider2D>();
            if (!StageRuntime.Instance || !StageRuntime.Instance.Player ||
                !StageRuntime.Instance.Player.TryGetComponent(out loadout)) return;
            loadout.Changed += Apply;
            Apply(loadout.Current);
        }

        private void Apply(PlayerPartBuild build)
        {
            bool canPass = build != null && (condition == Condition.HighJump ? build.Jump >= 3 : build.Height is >= 0 and <= 2);
            barrier.enabled = !canPass;
        }

        private void OnDestroy()
        {
            if (loadout) loadout.Changed -= Apply;
        }
    }
}
