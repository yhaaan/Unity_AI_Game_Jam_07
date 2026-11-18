#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    internal static class AnimalPartArtSetup
    {
        private const string Prefab = "Assets/02. Prefabs/Player.prefab";

        [MenuItem("Tools/RTD/Assign matching farm part art")]
        private static void Setup()
        {
            const string recipe = "Assets/04. Animations/PlayerLayers.yaml";
            AssetDatabase.ImportAsset(recipe, ImportAssetOptions.ForceUpdate);
            var library = AssetDatabase.LoadAssetAtPath<ChimeraLayerLibrary>(recipe);
            if (!library) throw new InvalidDataException(recipe);
            foreach (var guid in AssetDatabase.FindAssets("t:AnimalPartDefinition", new[] { "Assets/06. Data" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var part = AssetDatabase.LoadAssetAtPath<AnimalPartDefinition>(path);
                var art = library.Find(part);
                if (art?.preview == null) throw new InvalidDataException(path);
                string id = art.id + "_" + part.Slot.ToString().ToLowerInvariant();
                if (part.PartId.StartsWith("chicken_") || part.PartId.StartsWith("monkey_") || part.PartId.StartsWith("kangaroo_"))
                {
                    string label = part.Animal == PartAnimal.Rabbit ? "토끼" : part.Animal == PartAnimal.Bear ? "곰" : "우파루파";
                    string slot = part.Slot == PartSlot.Head ? "머리" : part.Slot == PartSlot.Body ? "몸통" : "다리";
                    var error = AssetDatabase.RenameAsset(path,"Part_" + id);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    part.ApplySheet(new PartSheetRow {partId=id, displayName=label+" "+slot, animal=part.Animal, slot=part.Slot});
                    EditorUtility.SetDirty(part);
                }
                var data = new SerializedObject(part);
                data.FindProperty("sprite").objectReferenceValue = art.preview;
                data.FindProperty("groundYpx").intValue = art.ground_y_px;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(part);
            }
            using var scope = new PrefabUtility.EditPrefabContentsScope(Prefab);
            var visual = scope.prefabContentsRoot.transform.Find("Visual");
            foreach (PartSlot slot in Enum.GetValues(typeof(PartSlot)))
            {
                var part = AssetDatabase.LoadAssetAtPath<AnimalPartDefinition>($"Assets/06. Data/Part_rabbit_{slot.ToString().ToLowerInvariant()}.asset");
                visual.Find(slot.ToString()).GetComponent<SpriteRenderer>().sprite = part.Sprite;
            }
        }

        // Upgrade legacy checkout once, retaining asset GUIDs referenced by the catalog.
        [InitializeOnLoadMethod]
        private static void DetectLegacyParts() => EditorApplication.delayCall += () =>
        {
            if (File.Exists("Assets/06. Data/Part_chicken_head.asset")) Setup();
        };
    }
}
#endif
