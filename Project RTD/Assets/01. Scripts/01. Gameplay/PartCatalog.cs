using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    public sealed class PartCatalog : ScriptableObject
    {
        [SerializeField] private AnimalPartDefinition[] parts = Array.Empty<AnimalPartDefinition>();
        public IEnumerable<AnimalPartDefinition> ForSlot(PartSlot slot)
        {
            foreach (var part in parts)
                if (part != null && part.Slot == slot) yield return part;
        }
#if UNITY_EDITOR
        internal void SetParts(AnimalPartDefinition[] imported) => parts = imported;
#endif
    }
}
