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
        internal PartCatalog Catalog => catalog;
        public event Action<PlayerPartBuild> Changed;
        internal event Action<PlayerPartBuild, PlayerPartBuild> Revealed;

        // A run outlives its scene objects, but never a trip back through the menus.
        private static PlayerPartBuild equipped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRun()
        {
            equipped = null;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private static void OnSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
        {
            if (!to.name.StartsWith("Stage", StringComparison.Ordinal)) equipped = null;
        }

        private void Start()
        {
            if (Current != null || !catalog) return;
            var next = equipped;
            if (next == null)
            {
                var head = catalog.ForSlot(PartSlot.Head).FirstOrDefault(p => p.Animal == PartAnimal.Axolotl);
                var body = catalog.ForSlot(PartSlot.Body).FirstOrDefault(p => p.Animal == PartAnimal.Bear);
                var legs = catalog.ForSlot(PartSlot.Legs).FirstOrDefault(p => p.Animal == PartAnimal.Chicken);
                if (!head || !body || !legs) { Debug.LogError("기본 조합 파츠가 카탈로그에 없습니다.", this); return; }
                next = new PlayerPartBuild(head, body, legs);
            }
            Equip(next);
        }

        public bool TryResummon()
        {
            if (!catalog) return false;
            var head = Pick(PartSlot.Head);
            var body = Pick(PartSlot.Body);
            var legs = Pick(PartSlot.Legs);
            if (!head || !body || !legs) return false;

            var previous = Current;
            var next = new PlayerPartBuild(head, body, legs);
            Equip(next);
            ProjectRTD.Core.SoundManager.Play(ProjectRTD.Core.Sfx.Summon);
            Revealed?.Invoke(previous, next);
            return true;
        }

        private void Equip(PlayerPartBuild next)
        {
            if (!motor)
            {
                motor = GetComponent<PlayerMotor2D>();
                baseJump = motor.JumpVelocity;
            }
            motor.JumpVelocity = baseJump * jumpScale.Evaluate(next.Jump);
            Current = next;
            if (Application.isPlaying) equipped = next;
            Changed?.Invoke(next);
        }

        private AnimalPartDefinition Pick(PartSlot slot)
        {
            var candidates = catalog.ForSlot(slot).ToArray();
            return candidates.Length == 0 ? null : candidates[UnityEngine.Random.Range(0, candidates.Length)];
        }
    }
}
