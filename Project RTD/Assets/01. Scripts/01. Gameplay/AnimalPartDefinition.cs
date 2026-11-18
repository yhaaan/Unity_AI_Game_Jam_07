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
        public string partId, animal;
        
        public PartSlot slot;
        public string displayName;
        [TextArea] public string description;
        public PartAnimal Animal => animal == "upa" ? PartAnimal.Axolotl : (PartAnimal)Enum.Parse(typeof(PartAnimal), animal, true);
        
        public int weightDelta;
        public string heightDelta = "0", skill = "";
        public int Height => heightDelta == "-" ? 0 : int.Parse(heightDelta, System.Globalization.CultureInfo.InvariantCulture);

        internal static bool TrySkill(string token, out PartEffect effect)
        {
            switch (token) {
                case "canFly": effect = PartEffect.Glide; return true;
                case "canPass": effect = PartEffect.CanPass; return true;
                case "canSwim": effect = PartEffect.Float; return true;
                default: effect = default; return false;
            }
        }

        public IEnumerable<PartEffect> Effects()
        {
            if (string.IsNullOrWhiteSpace(skill)) yield break;
            foreach (string token in skill.Split(','))
                if (TrySkill(token.Trim(), out var value)) yield return value;
        }
    }

    [CreateAssetMenu(menuName = "RTD/Animal Part")]
    public sealed class AnimalPartDefinition : ScriptableObject
    {
        [SerializeField] private PartSheetRow sheet = new PartSheetRow();
        [SerializeField] private Sprite sprite;
        public string PartId => sheet.partId;
        public PartAnimal Animal => sheet.Animal;
        public PartSlot Slot => sheet.slot;
        public string DisplayName => sheet.displayName;
        public string Description => sheet.description;


        public int HeightDelta => sheet.Height;
        public int WeightDelta => sheet.weightDelta;

        public IEnumerable<PartEffect> EffectIds => sheet.Effects();
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
