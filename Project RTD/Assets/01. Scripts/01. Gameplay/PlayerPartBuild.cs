using System;
using System.Collections.Generic;

namespace ProjectRTD.Gameplay
{
    // Per-player snapshot. The shared definitions never hold character state.
    public sealed class PlayerPartBuild
    {
        public AnimalPartDefinition Head { get; }
        public AnimalPartDefinition Body { get; }
        public AnimalPartDefinition Legs { get; }
        public int Speed { get; }
        public int Jump { get; }
        public int Weight { get; }
        public int Health { get; }
        private readonly HashSet<PartEffect> effects = new();

        public PlayerPartBuild(AnimalPartDefinition head, AnimalPartDefinition body, AnimalPartDefinition legs)
        {
            Head = RequireSlot(head, PartSlot.Head);
            Body = RequireSlot(body, PartSlot.Body);
            Legs = RequireSlot(legs, PartSlot.Legs);
            Weight = Level(head.WeightDelta, body.WeightDelta, legs.WeightDelta);
            Speed = Math.Max(1, Level(head.SpeedDelta, body.SpeedDelta, legs.SpeedDelta) - (Weight >= 4 ? 1 : 0));
            Jump = Level(head.JumpDelta, body.JumpDelta, legs.JumpDelta);
            Health = Level(head.HealthDelta, body.HealthDelta, legs.HealthDelta);
            effects.UnionWith(head.EffectIds);
            effects.UnionWith(body.EffectIds);
            effects.UnionWith(legs.EffectIds);
        }

        public bool HasEffect(PartEffect effect) => effects.Contains(effect);

        private static int Level(int a, int b, int c) => (int)Math.Clamp(3L + a + b + c, 1L, 5L);

        private static AnimalPartDefinition RequireSlot(AnimalPartDefinition part, PartSlot slot) =>
            part && part.Slot == slot ? part : throw new ArgumentException($"Expected a {slot} part.");
    }
}
