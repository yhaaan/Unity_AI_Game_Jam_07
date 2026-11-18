using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class StageWaterHazard2D : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") && other.TryGetComponent(out PlayerPartLoadout loadout) &&
                loadout.Current?.HasEffect(PartEffect.Float) != true)
                StageRuntime.Instance?.TakeDamage(lethal: true);
        }
    }
}
