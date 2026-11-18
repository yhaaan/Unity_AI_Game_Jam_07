using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class StageAssemblyIntro : MonoBehaviour
    {
        // Older stage assets still contain the retired panel. Never show it or accept input.
        private void Awake()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
