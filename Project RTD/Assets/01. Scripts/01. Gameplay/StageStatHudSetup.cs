#if UNITY_EDITOR
using System.IO;
using System.Linq;
using ProjectRTD.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    // Builds the bottom-left stat column (weight, speed, health, jump) under the "Stage HUD" canvas of Stage01.
    internal static class StageStatHudSetup
    {
        const string Stage = "Assets/00. Scenes/Stage01.unity";
        const string Icons = "Assets/03. Sprites/UI/StatIcons/stat_";
        const string PanelName = "Player Stats";
        const float Row = 76f, IconSize = 52f;
        const int Version = 5;
        static readonly string[] Stats = { "weight", "speed", "health", "jump" };

        [InitializeOnLoadMethod]
        static void FirstInstall() => EditorApplication.delayCall += () =>
        {
            if (File.Exists(Stage) && !File.ReadAllText(Stage).Contains("m_Name: " + PanelName + " v" + Version)) Install();
        };

        [MenuItem("Tools/RTD/Install Stage01 stat HUD")]
        static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetSceneByPath(Stage);
            bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) { Debug.LogWarning("Save Stage01 and run Tools/RTD/Install Stage01 stat HUD."); return; }
            if (opened) scene = EditorSceneManager.OpenScene(Stage, OpenSceneMode.Additive);
            try
            {
                var canvas = scene.GetRootGameObjects().Select(o => o.GetComponent<Canvas>()).Single(c => c && c.name == "Stage HUD");
                foreach (Transform child in canvas.transform.Cast<Transform>().ToArray())
                    if (child.name.StartsWith(PanelName)) Object.DestroyImmediate(child.gameObject);

                var panel = Rect(PanelName + " v" + Version, canvas.transform, new Vector2(24f, 24f), new Vector2(120f, Row * Stats.Length));
                panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
                var plate = Rect("Backplate", panel, Vector2.zero, new Vector2(16f, 16f));
                plate.anchorMin = Vector2.zero; plate.anchorMax = Vector2.one; plate.pivot = new Vector2(0.5f, 0.5f);
                var plateImage = plate.gameObject.AddComponent<Image>();
                plateImage.color = new Color(PrototypePalette.Ink.r, PrototypePalette.Ink.g, PrototypePalette.Ink.b, 0.55f);
                plateImage.raycastTarget = false;
                var hud = panel.gameObject.AddComponent<PlayerStatHud>();
                var fields = new SerializedObject(hud);
                var glows = fields.FindProperty("glows");
                glows.arraySize = Stats.Length;
                Color yellow = PrototypePalette.Goal;

                for (int i = 0; i < Stats.Length; i++)
                {
                    var row = Rect(Stats[i], panel, new Vector2(0f, -Row * i), new Vector2(120f, Row));
                    row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
                    var glow = Icon("Glow", row, Icons + Stats[i] + "_glow.png", new Color(yellow.r, yellow.g, yellow.b, 0.6f));
                    Icon("Icon", row, Icons + Stats[i] + ".png", yellow);
                    glows.GetArrayElementAtIndex(i).objectReferenceValue = glow;

                    var valueRect = Rect("Value", row, new Vector2(IconSize + 4f, 0f), new Vector2(60f, Row));
                    valueRect.anchorMin = valueRect.anchorMax = valueRect.pivot = new Vector2(0f, 0.5f);
                    var value = valueRect.gameObject.AddComponent<TextMeshProUGUI>();
                    value.text = "-";
                    value.fontSize = 34f;
                    value.fontStyle = FontStyles.Bold;
                    value.color = yellow;
                    value.alignment = TextAlignmentOptions.MidlineLeft;
                    value.raycastTarget = false;
                    fields.FindProperty(Stats[i] + "Text").objectReferenceValue = value;
                }

                fields.FindProperty("loadout").objectReferenceValue = scene.GetRootGameObjects()
                    .Select(o => o.GetComponentInChildren<PlayerPartLoadout>(true)).FirstOrDefault(l => l);
                fields.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException(Stage);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Image Icon(string name, Transform row, string path, Color color)
        {
            var rect = Rect(name, row, new Vector2(0f, 0f), new Vector2(IconSize, IconSize));
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(path);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Sprite LoadSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite ? sprite : throw new InvalidDataException(path);
        }
    }
}
#endif
