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
        [SerializeField] private Transform player;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private UnityEngine.UI.Image deathFade;

        private PlayerPartLoadout loadout;
        private PlayerMotor2D motor;
        private Rigidbody2D body;
        private Collider2D playerCollider;
        private Vector2 safePosition;
        private bool fallDamaged;

        public static StageRuntime Instance { get; private set; }
        public Transform Player => player;
        public Transform SpawnPoint => spawnPoint;
        public int Health { get; private set; } = 1;
        public int MaxHealth { get; private set; } = 1;
        public bool IsRespawning { get; private set; }
        public event Action HealthChanged;
        public event Action ResetMechanics;

        public void Configure(Transform playerTransform, Transform spawnTransform)
        {
            player = playerTransform;
            spawnPoint = spawnTransform;
        }

        public void TakeDamage(bool lethal = false, float deathDelay = 0f)
        {
            if (!player || !spawnPoint || IsRespawning) return;
            Health = lethal ? 0 : Health - 1;
            HealthChanged?.Invoke();
            if (Health == 0) StartCoroutine(Respawn(deathDelay));
        }

        public void RegisterFall(bool outsideStage)
        {
            if (IsRespawning || (motor && motor.IsClimbing)) return;
            if (!fallDamaged)
            {
                fallDamaged = true;
                TakeDamage();
            }

            // A fall onto a lower floor carries on. Recover only beyond the last floor.
            if (outsideStage && !IsRespawning) PlacePlayer(safePosition);
        }

        private IEnumerator Respawn(float delay)
        {
            IsRespawning = true;
            SoundManager.Play(Sfx.Defeat);
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            yield return Fade(0f, 1f);
            ResetMechanics?.Invoke();
            safePosition = spawnPoint.position;
            PlacePlayer(safePosition);
            Health = MaxHealth;
            HealthChanged?.Invoke();
            SoundManager.Play(Sfx.Respawn);
            yield return Fade(1f, 0f);
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
            fallDamaged = false;
        }

        private void ApplyBuild(PlayerPartBuild build)
        {
            MaxHealth = build?.Health ?? 1;
            Health = MaxHealth;
            HealthChanged?.Invoke();
        }

        private void LateUpdate()
        {
            if (!motor || !body || !playerCollider || IsRespawning || !motor.IsGrounded) return;
            Collider2D ground = motor.GroundCollider;
            if (!ground) return;
            fallDamaged = false;
            if (ground.attachedRigidbody || ground.GetComponentInParent<CollapsingBridge2D>()) return;

            Bounds surface = ground.bounds;
            float margin = playerCollider.bounds.extents.x;
            if (surface.size.x < margin * 2f) return;
            safePosition = new Vector2(Mathf.Clamp(body.position.x, surface.min.x + margin, surface.max.x - margin),
                surface.max.y + body.position.y - playerCollider.bounds.min.y + .02f);
        }

        private void Awake()
        {
            Instance = this;
            if (spawnPoint) safePosition = spawnPoint.position;
            if (!player) return;
            player.TryGetComponent(out body);
            player.TryGetComponent(out motor);
            player.TryGetComponent(out playerCollider);
            if (player.TryGetComponent(out loadout))
            {
                loadout.Changed += ApplyBuild;
                ApplyBuild(loadout.Current);
            }
        }

        private void OnDestroy()
        {
            if (loadout) loadout.Changed -= ApplyBuild;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                SceneFlow.RestartActiveScene();
            }
        }
    }
}
