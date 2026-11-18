using System;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    public sealed class ChimeraLayerLibrary : ScriptableObject
    {
        [Serializable] public class Layer
        {
            public string id, role, parent, path;
            public int[] pivot;
            public Sprite sprite;
        }
        [Serializable] public class Part
        {
            public string id;
            public int ground_y_px;
            public Sprite preview;
            public Layer[] layers;
        }
        [Serializable] public class Parts { public Part[] heads, bodies, legs; }
        [Serializable] public class Manifest { public Parts parts; }
        public Parts parts;

        public Part Find(AnimalPartDefinition part)
        {
            var list = part.Slot == PartSlot.Head ? parts.heads : part.Slot == PartSlot.Body ? parts.bodies : parts.legs;
            return Array.Find(list, p => string.Equals(p.id, part.Animal.ToString(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
