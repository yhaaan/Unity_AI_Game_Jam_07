using ProjectRTD.Core;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class StageGoal : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                SoundManager.Play(Sfx.Goal);
                SceneFlow.LoadEnding();
            }
        }
    }
}
