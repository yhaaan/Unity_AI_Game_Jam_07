using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    public enum PartAnimal { Chicken, Monkey, Lion, Kangaroo, Duck }
    public enum PartSlot { Head, Body, Legs }
    public enum PartEffect { SideDrift, Glide, ShortStature, BananaFrenzy, WallClimb, PredatorSafe, HunterVulnerable, Float }

    [Serializable]
    public sealed class PartSheetRow
    {
        public string partId, displayName;
        [TextArea] public string description;
        public PartAnimal animal;
        public PartSlot slot;
        public int speedDelta, jumpDelta, weightDelta, healthDelta;
        public PartEffect[] effectIds = Array.Empty<PartEffect>();
    }

    [CreateAssetMenu(menuName = "RTD/Animal Part")]
    public sealed class AnimalPartDefinition : ScriptableObject
    {
        [SerializeField] private PartSheetRow sheet = new PartSheetRow();
        [SerializeField] private Sprite sprite;
        public string PartId => sheet.partId;
        public PartAnimal Animal => sheet.animal;
        public PartSlot Slot => sheet.slot;
        public string DisplayName => sheet.displayName;
        public string Description => sheet.description;
        public int SpeedDelta => sheet.speedDelta;
        public int JumpDelta => sheet.jumpDelta;
        public int WeightDelta => sheet.weightDelta;
        public int HealthDelta => sheet.healthDelta;
        public IReadOnlyList<PartEffect> EffectIds => sheet.effectIds;
        public Sprite Sprite => sprite;
#if UNITY_EDITOR
        internal void ApplySheet(PartSheetRow row) => sheet = row;
#endif
    }
}
