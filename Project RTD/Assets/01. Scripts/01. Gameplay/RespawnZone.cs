using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class RespawnZone : MonoBehaviour
    {

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") && other.attachedRigidbody &&
                other.attachedRigidbody.linearVelocityY < 0f && StageRuntime.Instance)
            {
                StageRuntime.Instance.ReturnToStart(falling: true);
            }
        }
    }
}
