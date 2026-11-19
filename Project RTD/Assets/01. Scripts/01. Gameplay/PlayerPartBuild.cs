using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Per-player snapshot. The shared definitions never hold character state.
    public sealed class PlayerPartBuild
    {
        public AnimalPartDefinition Head { get; }
        public AnimalPartDefinition Body { get; }
        public AnimalPartDefinition Legs { get; }
        public int Speed => 3; // Fixed HUD level; movement uses the motor's configured speed.
        public int Power { get; }
        public int Jump { get; }
        public int Height { get; }
        public int Weight { get; }
        private readonly HashSet<PartEffect> effects = new();
        // "불사조" / "용" when this is a legendary roll: every skill, top jump and power, and it fits anywhere.
        public string Legend { get; }

        public PlayerPartBuild(AnimalPartDefinition head, AnimalPartDefinition body, AnimalPartDefinition legs, string legend = null)
        {
            Legend = legend;
            Head = RequireSlot(head, PartSlot.Head);
            Body = RequireSlot(body, PartSlot.Body);
            Legs = RequireSlot(legs, PartSlot.Legs);
            Height = head.HeightDelta + body.HeightDelta + legs.HeightDelta;
            Weight = head.WeightDelta + body.WeightDelta + legs.WeightDelta;
            Jump = Weight >= 3 ? 1 : Weight <= -3 ? 3 : 2;
            Power = Weight >= 3 ? 3 : Weight <= -2 ? 1 : 2;
            effects.UnionWith(head.EffectIds);
            effects.UnionWith(body.EffectIds);
            effects.UnionWith(legs.EffectIds);
            if (Legend == null) return;
            Jump = 3; Power = 3;
            Weight = Mathf.Clamp(Weight, -2, 2);   // light enough for weak bridges
            Height = Mathf.Clamp(Height, 0, 2);    // short enough for low ceilings
            effects.Add(PartEffect.Glide); effects.Add(PartEffect.Float); effects.Add(PartEffect.CanPass);
        }

        public bool HasEffect(PartEffect effect) => effects.Contains(effect);

        private static AnimalPartDefinition RequireSlot(AnimalPartDefinition part, PartSlot slot) =>
            part && part.Slot == slot ? part : throw new ArgumentException($"Expected a {slot} part.");
    }
}
