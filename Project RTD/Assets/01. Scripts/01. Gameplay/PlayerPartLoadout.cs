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

        [Tooltip("리롤할 때 전설 조합(불사조·용)이 나올 확률. 0.001 = 0.1%")]
        [SerializeField, Range(0f, 1f)] private float legendChance = 0.001f;
        internal static readonly string[] Legends = { "불사조", "용" };
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

        public bool TryResummon() => TryResummon(null);

        // accept narrows the roll (e.g. "can clear this stage" / "cannot"). If no combination fits, any combination is used.
        public bool TryResummon(Func<PlayerPartBuild, bool> accept)
        {
            if (!catalog) return false;
            PlayerPartBuild next;
            if (accept == null)
            {
                var head = Pick(PartSlot.Head);
                var body = Pick(PartSlot.Body);
                var legs = Pick(PartSlot.Legs);
                if (!head || !body || !legs) return false;
                next = new PlayerPartBuild(head, body, legs);
            }
            else
            {
                var all = (from h in catalog.ForSlot(PartSlot.Head)
                           from b in catalog.ForSlot(PartSlot.Body)
                           from l in catalog.ForSlot(PartSlot.Legs)
                           select new PlayerPartBuild(h, b, l)).ToList();
                if (all.Count == 0) return false;
                var fits = all.Where(accept).ToList();
                // Prefer a visibly different body over re-rolling the exact same build.
                var fresh = fits.Where(b => !SameBuild(b, Current)).ToList();
                var pool = fresh.Count > 0 ? fresh : fits.Count > 0 ? fits : all;
                next = pool[UnityEngine.Random.Range(0, pool.Count)];
            }

            if (UnityEngine.Random.value < legendChance) next = Legendary(next);
            var previous = Current;
            Equip(next);
            ProjectRTD.Core.SoundManager.Play(ProjectRTD.Core.Sfx.Summon);
            Revealed?.Invoke(previous, next);
            return true;
        }

        // A legend wears its own three parts (Resources/LegendParts). If they are missing, the current parts stand in.
        private static PlayerPartBuild Legendary(PlayerPartBuild from)
        {
            string legend = Legends[UnityEngine.Random.Range(0, Legends.Length)];
            string id = legend == "불사조" ? "phoenix" : "dragon";
            var head = Resources.Load<AnimalPartDefinition>($"LegendParts/Part_{id}_head");
            var body = Resources.Load<AnimalPartDefinition>($"LegendParts/Part_{id}_body");
            var legs = Resources.Load<AnimalPartDefinition>($"LegendParts/Part_{id}_legs");
            return head && body && legs ? new PlayerPartBuild(head, body, legs, legend)
                                        : new PlayerPartBuild(from.Head, from.Body, from.Legs, legend);
        }

        // Dev/test: turn the current build into a legend right now.
        internal void ForceLegend()
        {
            if (Current == null) return;
            var previous = Current;
            Equip(Legendary(Current));
            ProjectRTD.Core.SoundManager.Play(ProjectRTD.Core.Sfx.Summon);
            Revealed?.Invoke(previous, Current);
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

        private static bool SameBuild(PlayerPartBuild a, PlayerPartBuild b) =>
            a != null && b != null && a.Head == b.Head && a.Body == b.Body && a.Legs == b.Legs;

        private AnimalPartDefinition Pick(PartSlot slot)
        {
            var candidates = catalog.ForSlot(slot).ToArray();
            return candidates.Length == 0 ? null : candidates[UnityEngine.Random.Range(0, candidates.Length)];
        }
    }
}
