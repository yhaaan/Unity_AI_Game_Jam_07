using System;
using System.Linq;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerMotor2D))]
    public sealed class PlayerPartLoadout : MonoBehaviour
    {
        [SerializeField] private PartCatalog catalog;
        [Header("Prototype multipliers by level (1–5)")]
        [SerializeField] private AnimationCurve speedScale = AnimationCurve.Linear(1f, 0.8f, 5f, 1.2f);
        [SerializeField] private AnimationCurve jumpScale = AnimationCurve.Linear(1f, 0.8f, 5f, 1.2f);

        private PlayerMotor2D motor;
        private float baseSpeed, baseJump;
        public PlayerPartBuild Current { get; private set; }
        public event Action<PlayerPartBuild> Changed;

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
                baseSpeed = motor.MoveSpeed;
                baseJump = motor.JumpVelocity;
            }
            var next = new PlayerPartBuild(head, body, legs);
            motor.MoveSpeed = baseSpeed * speedScale.Evaluate(next.Speed);
            motor.JumpVelocity = baseJump * jumpScale.Evaluate(next.Jump);
            Current = next;
            ProjectRTD.Core.SoundManager.Play(ProjectRTD.Core.Sfx.Summon);
            Changed?.Invoke(next);
            return true;
        }

        private AnimalPartDefinition Pick(PartSlot slot)
        {
            var candidates = catalog.ForSlot(slot).ToArray();
            return candidates.Length == 0 ? null : candidates[UnityEngine.Random.Range(0, candidates.Length)];
        }
    }
}
