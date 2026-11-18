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

        public static StageRuntime Instance { get; private set; }
        public Transform Player => player;
        public Transform SpawnPoint => spawnPoint;

        public void Configure(Transform playerTransform, Transform spawnTransform)
        {
            player = playerTransform;
            spawnPoint = spawnTransform;
        }

        public void RespawnPlayer()
        {
            if (player == null || spawnPoint == null)
            {
                return;
            }

            if (player.TryGetComponent(out Rigidbody2D body))
            {
                body.position = spawnPoint.position;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.rotation = 0f;
            }
            else
            {
                player.position = spawnPoint.position;
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
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
