#if UNITY_EDITOR
using ProjectRTD.Core;
using UnityEditor;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Adds the yellow PlayerOutline (on Visual) and the reroll PlayerStatPopup (on the root) to the Player prefab.
    internal static class PlayerOutlineSetup
    {
        const string PlayerPrefab = "Assets/02. Prefabs/Player.prefab";
        const string MaterialPath = "Assets/05. Arts/PlayerOutline.mat";

        [InitializeOnLoadMethod]
        static void AutoInstall() => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (prefab && (!prefab.GetComponentInChildren<PlayerOutline>(true) || !prefab.GetComponent<PlayerStatPopup>())) Install();
        };

        const string Icons = "Assets/03. Sprites/UI/StatIcons/stat_";
        const string Font = "Assets/TextMesh Pro/Resources/Fonts & Materials/Engcomica SDF.asset";

        [MenuItem("Tools/RTD/Add player outline and stat popup")]
        static void Install()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                var shader = Shader.Find("RTD/Sprite Silhouette");
                if (!shader) { Debug.LogWarning("RTD/Sprite Silhouette shader is not imported yet."); return; }
                material = new Material(shader) { name = "PlayerOutline" };
                material.SetColor("_Color", PrototypePalette.Goal);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var visual = root.GetComponentInChildren<PlayerPartVisual>(true);
                var outline = visual.GetComponent<PlayerOutline>();
                if (!outline) outline = visual.gameObject.AddComponent<PlayerOutline>();
                var fields = new SerializedObject(outline);
                fields.FindProperty("silhouette").objectReferenceValue = material;
                fields.ApplyModifiedPropertiesWithoutUndo();

                var popup = root.GetComponent<PlayerStatPopup>();
                if (!popup) popup = root.AddComponent<PlayerStatPopup>();
                var popupFields = new SerializedObject(popup);
                foreach (var stat in new[] { "weight", "speed", "health", "jump" })
                    popupFields.FindProperty(stat + "Icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Icons + stat + ".png");
                popupFields.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(Font);
                popupFields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("Player prefab now has a yellow outline.");
        }
    }
}
#endif
