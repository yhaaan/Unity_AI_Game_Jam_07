using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class RespawnZone : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                StageRuntime.Instance?.RespawnPlayer();
            }
        }
    }
}
