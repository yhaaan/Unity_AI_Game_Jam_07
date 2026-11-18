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

        public string Requirement => challenge switch
        {
            Challenge.HighJump => "JUMP LEVEL 3",
            Challenge.Glide => "GLIDING BODY",
            Challenge.Lightweight => "WEIGHT BELOW 3",
            Challenge.Scientist => "BEAR OR LION HEAD",
            Challenge.LowCeiling => "HEIGHT 0-2",
            Challenge.Power => "POWER LEVEL 3",
            Challenge.Swim => "DUCK OR AXOLOTL BODY",
            _ => ""
        };

        public bool CanClearWith(PlayerPartBuild build) => build != null && (challenge switch
        {
            Challenge.HighJump => build.Jump >= 3,
            Challenge.Glide => build.HasEffect(PartEffect.Glide),
            Challenge.Lightweight => build.Weight < 3,
            Challenge.Scientist => build.HasEffect(PartEffect.CanPass),
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
            StageCourseLayout.Apply(this);
            if (!player) return;
            player.TryGetComponent(out body);
            player.TryGetComponent(out motor);
            player.TryGetComponent(out loadout);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Reroll()
        {
            if (!InDoorway && !IsRespawning && Time.timeScale > 0f && loadout) loadout.TryResummon();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || InDoorway || IsRespawning || Time.timeScale <= 0f) return;
            if (keyboard.rKey.wasPressedThisFrame) Reroll();
        }
    }
}
