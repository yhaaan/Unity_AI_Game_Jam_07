using System;
using System.Collections;
using ProjectRTD.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class StageRuntime : MonoBehaviour
    {
        public enum Challenge { HighJump, Glide, Lightweight, Scientist, LowCeiling, Power, Swim }

        [SerializeField] private Transform player;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private UnityEngine.UI.Image deathFade;
        [SerializeField] private Challenge challenge;

        private PlayerPartLoadout loadout;
        private PlayerMotor2D motor;
        private Rigidbody2D body;
        // Failures always return to the stage entrance, never to a recent landing.

        public static StageRuntime Instance { get; private set; }
        public Transform Player => player;
        public Transform SpawnPoint => spawnPoint;
        public bool IsRespawning { get; private set; }

        internal bool InDoorway { get; private set; }
        bool motorWasEnabled, bodyWasSimulated;

        internal void BeginDoorway()
        {
            InDoorway = true;
            if (motor) { motorWasEnabled = motor.enabled; motor.enabled = false; }
            if (body)
            {
                bodyWasSimulated = body.simulated;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0;
                body.simulated = false;
            }
            if (deathFade) deathFade.transform.SetAsLastSibling();
        }

        internal void SetDoorFade(float alpha)
        {
            if (deathFade) deathFade.color = new Color(0, 0, 0, Mathf.Clamp01(alpha));
        }

        internal void EndDoorway()
        {
            if (body) { body.position = player.position; body.simulated = bodyWasSimulated; }
            if (player.TryGetComponent<PlayerInputReader>(out var input)) input.ConsumeJumpPressed();
            if (motor) { motor.ResetAfterTeleport(); motor.enabled = motorWasEnabled; }
            InDoorway = false;
        }

        public event Action ResetMechanics;

        public string Requirement => RequirementFor(challenge);

        public static string RequirementFor(Challenge c) => c switch
        {
            Challenge.HighJump => "JUMP LEVEL 3",
            Challenge.Glide => "GLIDING BODY",
            Challenge.Lightweight => "WEIGHT BELOW 3",
            Challenge.Scientist => "HEAD NOT IN THE BUBBLE",
            Challenge.LowCeiling => "HEIGHT 0-2",
            Challenge.Power => "POWER LEVEL 3",
            Challenge.Swim => "DUCK OR AXOLOTL BODY",
            _ => ""
        };

        // Multi-gimmick stages (StageSections) judge a build against the obstacle just ahead of the player.
        public Challenge CurrentChallenge =>
            StageSections.Instance && player ? StageSections.Instance.ChallengeAt(player.position.x) : challenge;

        public bool CanClearWith(PlayerPartBuild build) => Passes(CurrentChallenge, build);

        public bool Passes(Challenge c, PlayerPartBuild build) => build != null && (c switch
        {
            Challenge.HighJump => build.Jump >= 3,
            Challenge.Glide => build.HasEffect(PartEffect.Glide),
            Challenge.Lightweight => build.Weight < 3,
            Challenge.Scientist => !Array.Exists(GetComponentsInChildren<PlatformEnemy>(), enemy => enemy.Attacks(build)),
            Challenge.LowCeiling => build.Height is >= 0 and <= 2,
            Challenge.Power => build.Power >= 3,
            Challenge.Swim => build.HasEffect(PartEffect.Float),
            _ => false
        });

        public void Configure(Transform playerTransform, Transform spawnTransform)
        {
            player = playerTransform;
            spawnPoint = spawnTransform;
        }

        public void ReturnToStart(bool falling = false)
        {
            if (!player || !spawnPoint || IsRespawning || InDoorway) return;
            // 불사조 rises from its ashes once per stage (not out of a pit).
            if (!falling && PlayerLegendPower.TryRevive()) return;
            StartCoroutine(Respawn(falling));
        }

        private IEnumerator Respawn(bool falling)
        {
            IsRespawning = true;
            bool restoreMotor = motor && motor.enabled;
            bool restorePhysics = body && body.simulated;
            if (motor) motor.enabled = false;
            if (body) body.simulated = false;
            if (deathFade) deathFade.transform.SetAsLastSibling();
            SoundManager.Play(Sfx.Defeat);
            if (falling)
            {
                // Keep the fall readable even when the basin bottom or a lower ledge has a collider.
                Vector3 start = player.position;
                const float duration = .42f;
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    player.position = start + Vector3.down * (1.5f * t + 6f * t * t);
                    yield return null;
                }
            }
            yield return Fade(0f, 1f);
            ResetMechanics?.Invoke();
            PlacePlayer(spawnPoint.position);
            SoundManager.Play(Sfx.Respawn);
            yield return Fade(1f, 0f);
            if (body) body.simulated = restorePhysics;
            if (player.TryGetComponent<PlayerInputReader>(out var input)) input.ConsumeJumpPressed();
            if (motor) motor.enabled = restoreMotor;
            IsRespawning = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (!deathFade) yield break;
            const float duration = 0.28f;
            float elapsed = 0f;
            Color color = deathFade.color;
            while (elapsed < duration)
            {
                color.a = Mathf.Lerp(from, to, elapsed / duration);
                deathFade.color = color;
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            color.a = to;
            deathFade.color = color;
        }

        private void PlacePlayer(Vector2 position)
        {
            if (motor) motor.ResetAfterTeleport();
            if (body)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.rotation = 0f;
            }
            player.position = position;
        }

        private void Awake()
        {
            Instance = this;
            // The course layout (floors, walls, spawn, camera bounds) is baked into the scene: Tools/RTD/Bake Stage Layout.
            if (!player) return;
            player.TryGetComponent(out body);
            player.TryGetComponent(out motor);
            player.TryGetComponent(out loadout);
        }

        // If the entry roll is interrupted (scene unload, play-mode hot reload kills coroutines), never leave the
        // player frozen.
        private void OnDisable()
        {
            if (!AwaitingEntryRoll) return;
            AwaitingEntryRoll = false;
            if (motor) motor.enabled = true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------- rerolls ----------

        public enum RollOutcome { Random, CannotClear, CanClear }

        [Header("Reroll")]
        [Tooltip("스테이지에 처음 들어온 뒤 자동 리롤까지 기다리는 시간(초). 그동안 플레이어는 움직일 수 없다.")]
        [SerializeField, Min(0f)] private float entryRerollDelay = 1.2f;
        [Tooltip("이 스테이지에서 n번째 리롤의 결과 (자동 리롤이 1번째). 끝까지 쓰면 처음부터 다시 반복.")]
        [SerializeField] private RollOutcome[] rollPlan = DefaultRollPlan();

        // Roll count per stage for this run, so restarting a stage does not hand back the guaranteed misses.
        private static readonly System.Collections.Generic.Dictionary<string, int> rollCounts = new();
        private int RollCount
        {
            get => rollCounts.TryGetValue(gameObject.scene.name, out int n) ? n : 0;
            set => rollCounts[gameObject.scene.name] = value;
        }
        internal bool AwaitingEntryRoll { get; private set; }
        /// <summary>Rerolls the player spent in this stage this run (the free stage-entry roll is not counted).</summary>
        internal int ManualRolls => Mathf.Max(0, RollCount - 1);

        // 20 rolls: the first 3 never clear, every 5th (5, 10, 15, 20) always can, the rest are random.
        internal static RollOutcome[] DefaultRollPlan()
        {
            var plan = new RollOutcome[20];
            for (int i = 0; i < plan.Length; i++)
                plan[i] = i < 3 ? RollOutcome.CannotClear : (i + 1) % 5 == 0 ? RollOutcome.CanClear : RollOutcome.Random;
            return plan;
        }

        internal RollOutcome NextOutcome()
        {
            var plan = rollPlan != null && rollPlan.Length > 0 ? rollPlan : DefaultRollPlan();
            return plan[RollCount % plan.Length];
        }

        private bool RollNow()
        {
            if (!loadout) return false;
            var outcome = NextOutcome();
            System.Func<PlayerPartBuild, bool> accept = outcome switch
            {
                RollOutcome.CanClear => CanClearWith,
                RollOutcome.CannotClear => build => !CanClearWith(build),
                _ => null
            };
            // Tutorial room: after the entry roll, R always gives a build that beats the obstacle ahead.
            var guide = TutorialGuide.Instance;
            if (guide && !AwaitingEntryRoll && player && guide.AcceptFor(player.position.x) is { } next) accept = next;
            if (!loadout.TryResummon(accept)) return false;
            RollCount++;
            return true;
        }

        private IEnumerator EntryRoll()
        {
            AwaitingEntryRoll = true;
            if (motor) motor.enabled = false;
            if (body) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            yield return new WaitForSeconds(entryRerollDelay);
            while (InDoorway || IsRespawning || Time.timeScale <= 0f || StageTutorial.Active) yield return null;
            RollNow();
            if (player && player.TryGetComponent<PlayerInputReader>(out var input)) input.ConsumeJumpPressed();
            // Always hand control back. Don't restore a remembered state: the door arrival may have switched the
            // motor off before this ran, and restoring that would leave the player frozen.
            if (motor) motor.enabled = true;
            AwaitingEntryRoll = false;
        }

        // Stages entered during this run. Reloading the same stage (restart) keeps the current build;
        // going back through the menus starts a fresh run.
        private static readonly System.Collections.Generic.HashSet<string> enteredStages = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRun()
        {
            enteredStages.Clear();
            rollCounts.Clear();
            RollsLeft = RollBudget;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= ForgetRunOutsideStages;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += ForgetRunOutsideStages;
        }

        private static void ForgetRunOutsideStages(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
        {
            if (to.name.StartsWith("Stage", StringComparison.Ordinal)) return;
            enteredStages.Clear();
            rollCounts.Clear();
            RollsLeft = RollBudget;
        }

        private void Start()
        {
            if (loadout && player && spawnPoint && enteredStages.Add(gameObject.scene.name)) StartCoroutine(EntryRoll());
        }

        // ---- reroll budget: the whole run shares this many player rerolls (stage-entry rolls and the tutorial are free) ----
        public const int RollBudget = 80, RefillOnEmpty = 5;
        public static int RollsLeft { get; private set; } = RollBudget;
        /// <summary>Raised when the player pressed R with nothing left: sent back to the stage start with a small refill.</summary>
        public static event Action RanOutOfRolls;

        private void Reroll()
        {
            if (InDoorway || IsRespawning || AwaitingEntryRoll || Time.timeScale <= 0f) return;
            bool free = TutorialGuide.Instance;
            if (!free && RollsLeft <= 0)
            {
                RollsLeft = RefillOnEmpty;
                RanOutOfRolls?.Invoke();
                ReturnToStart();
                return;
            }
            if (RollNow() && !free) RollsLeft--;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || InDoorway || IsRespawning || Time.timeScale <= 0f) return;
            if (keyboard.rKey.wasPressedThisFrame) Reroll();
        }
    }
}
