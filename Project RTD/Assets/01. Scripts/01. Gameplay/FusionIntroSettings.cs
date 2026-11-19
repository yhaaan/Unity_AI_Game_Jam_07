using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Resources/FusionTank/FusionIntroSettings: lets the new-game film intro find the part catalog at runtime.
    public sealed class FusionIntroSettings : ScriptableObject
    {
        public PartCatalog catalog;
        [Tooltip("off = start the game straight away")] public bool play = true;
    }
}
