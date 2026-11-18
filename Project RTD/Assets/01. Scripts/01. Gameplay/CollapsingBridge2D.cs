using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class CollapsingBridge2D : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D[] planks;
        private StageRuntime stage;
        private Vector3[] positions;
        private float fallTime;
        private bool collapsed;

        private void Start()
        {
            positions = new Vector3[planks.Length];
            for (int i = 0; i < planks.Length; i++) positions[i] = planks[i].transform.localPosition;
            stage = StageRuntime.Instance;
            if (stage) stage.ResetMechanics += RestoreBridge;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collapsed || !stage || stage.IsRespawning || !other.CompareTag("Player") ||
                !other.TryGetComponent(out PlayerPartLoadout loadout) || (loadout.Current?.Weight ?? 0) < 3) return;

            collapsed = true;
            foreach (BoxCollider2D plank in planks) plank.enabled = false;
            stage.ReturnToStart(falling: true);
        }

        private void Update()
        {
            if (!collapsed) return;
            fallTime += Time.deltaTime;
            float drop = 15f * fallTime * fallTime;
            for (int i = 0; i < planks.Length; i++)
            {
                Transform plank = planks[i].transform;
                plank.localPosition = positions[i] + Vector3.down * drop;
                plank.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * fallTime * 85f);
            }
        }

        private void RestoreBridge()
        {
            if (!collapsed) return;
            collapsed = false;
            fallTime = 0f;
            for (int i = 0; i < planks.Length; i++)
            {
                BoxCollider2D plank = planks[i];
                plank.transform.localPosition = positions[i];
                plank.transform.localRotation = Quaternion.identity;
                plank.enabled = true;
            }
        }

        private void OnDestroy()
        {
            if (stage) stage.ResetMechanics -= RestoreBridge;
        }
    }
}
