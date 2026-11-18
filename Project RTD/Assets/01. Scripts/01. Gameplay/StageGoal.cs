using ProjectRTD.Core;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class StageGoal : MonoBehaviour
    {
        private bool completed;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (completed || !other.CompareTag("Player")) return;
            var stage = StageRuntime.Instance;
            if (stage && stage.IsRespawning) return;

            completed = true;
            SoundManager.Play(Sfx.Goal);
            SceneFlow.CompleteActiveStage();
        }
    }
}
