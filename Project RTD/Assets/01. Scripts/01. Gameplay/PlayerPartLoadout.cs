using System;
using System.Linq;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerMotor2D))]
    public sealed class PlayerPartLoadout : MonoBehaviour
    {
        [SerializeField] private PartCatalog catalog;
        [Header("Jump multiplier by level (1–3)")]
        // Takeoff speeds tuned for apex heights 1 : 1.4 : 2.8 at the 0.02 s physics step.
        [SerializeField] private AnimationCurve jumpScale = new AnimationCurve(
            new Keyframe(1f, 0.8f), new Keyframe(2f, 0.94163f), new Keyframe(3f, 1.31882f));

        private PlayerMotor2D motor;
        private float baseJump;
        public PlayerPartBuild Current { get; private set; }
        public event Action<PlayerPartBuild> Changed;
        internal event Action<PlayerPartBuild, PlayerPartBuild> Revealed;

        private void Start()
        {
            if (Current == null && !TryResummon())
                Debug.LogWarning("파츠 카탈로그 또는 슬롯 후보가 없습니다. 기본 이동값을 유지합니다.", this);
        }

        // Call for a new stage/summon, not when returning to a checkpoint.
        public bool TryResummon()
        {
            if (!catalog) return false;
            var head = Pick(PartSlot.Head);
            var body = Pick(PartSlot.Body);
            var legs = Pick(PartSlot.Legs);
            if (!head || !body || !legs) return false;

            if (!motor)
            {
                motor = GetComponent<PlayerMotor2D>();
                baseJump = motor.JumpVelocity;
            }
            var next = new PlayerPartBuild(head, body, legs);
            motor.JumpVelocity = baseJump * jumpScale.Evaluate(next.Jump);
            var previous = Current;
            Current = next;
            ProjectRTD.Core.SoundManager.Play(ProjectRTD.Core.Sfx.Summon);
            Changed?.Invoke(next);
            Revealed?.Invoke(previous, next);
            return true;
        }

        private AnimalPartDefinition Pick(PartSlot slot)
        {
            var candidates = catalog.ForSlot(slot).ToArray();
            return candidates.Length == 0 ? null : candidates[UnityEngine.Random.Range(0, candidates.Length)];
        }
    }
}
