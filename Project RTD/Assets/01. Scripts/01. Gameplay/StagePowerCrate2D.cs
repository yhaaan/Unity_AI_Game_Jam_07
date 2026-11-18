using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class StagePowerCrate2D : MonoBehaviour
    {
        private Rigidbody2D body;
        private StageRuntime stage;
        private PlayerPartLoadout loadout;
        private Vector2 startPosition;

        private void Start()
        {
            body = GetComponent<Rigidbody2D>();
            startPosition = body.position;
            stage = StageRuntime.Instance;
            if (!stage || !stage.Player || !stage.Player.TryGetComponent(out loadout)) return;
            stage.ResetMechanics += ResetCrate;
            loadout.Changed += Apply;
            Apply(loadout.Current);
        }

        private void Apply(PlayerPartBuild build)
        {
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            if (build == null || build.Power < 3) body.constraints |= RigidbodyConstraints2D.FreezePositionX;
        }

        private void ResetCrate()
        {
            transform.position = startPosition;
            body.position = startPosition;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private void OnDestroy()
        {
            if (stage) stage.ResetMechanics -= ResetCrate;
            if (loadout) loadout.Changed -= Apply;
        }
    }
}
