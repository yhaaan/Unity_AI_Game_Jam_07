using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    public enum PartAnimal { Rabbit = 0, Bear = 1, Lion = 2, Axolotl = 3, Duck = 4, Chicken = 5 }
    public enum PartSlot { Head, Body, Legs }
    public enum PartEffect { SideDrift, Glide, ShortStature, BananaFrenzy, WallClimb, PredatorSafe, HunterVulnerable, Float, CanPass }

    [Serializable]
    public sealed class PartSheetRow
    {
        public string partId, displayName;
        [TextArea] public string description;
        public PartAnimal animal;
        public PartSlot slot;
        public int weightDelta, healthDelta, heightDelta;

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


        public int HeightDelta => sheet.heightDelta;
        public int WeightDelta => sheet.weightDelta;
        public int HealthDelta => sheet.healthDelta;
        public IReadOnlyList<PartEffect> EffectIds => sheet.effectIds;
        public Sprite Sprite => sprite;
        [SerializeField, Tooltip("다리 PNG의 위쪽부터 발바닥까지의 픽셀 거리")]
        private int groundYpx;
        public int GroundYpx => groundYpx;
#if UNITY_EDITOR
        internal void ApplyArt(ChimeraLayerLibrary.Part art)
        {
            if (art == null) return;
            sprite = art.preview;
            groundYpx = art.ground_y_px;
        }
        internal void ApplySheet(PartSheetRow row) => sheet = row;
#endif
    }
}
