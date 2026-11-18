#if UNITY_EDITOR
using ProjectRTD.Core;
using UnityEditor;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Adds the yellow PlayerOutline (on Visual), the reroll PlayerStatPopup and the PlayerCombineFx (on the root) to the Player prefab.
    internal static class PlayerOutlineSetup
    {
        const string PlayerPrefab = "Assets/02. Prefabs/Player.prefab";
        const string MaterialPath = "Assets/05. Arts/PlayerOutline.mat";

        [InitializeOnLoadMethod]
        static void AutoInstall()
        {
            EditorApplication.delayCall += InstallIfMissing;
            // Scripts often recompile while the game is playing; finish the install as soon as play stops.
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += InstallIfMissing;
            };
        }

        static void InstallIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (prefab && (!prefab.GetComponentInChildren<PlayerOutline>(true) || !prefab.GetComponent<PlayerStatPopup>() || !prefab.GetComponent<PlayerCombineFx>() || !HasCombineFont(prefab) || !HasPhasedTiming(prefab))) Install();
        }

        const string Icons = "Assets/03. Sprites/UI/StatIcons/stat_";
        const string Dust = "Assets/99. Externals/Prologue/Resources/Prologue/cut4-dust-cloud/parts/";
        const string Font = "Assets/TextMesh Pro/Resources/Fonts & Materials/Engcomica SDF.asset";

        [MenuItem("Tools/RTD/Add player outline, stat popup and combine FX")]
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
                popupFields.FindProperty("startDelay").floatValue = 0.88f; // right after the combine cloud bursts
                popupFields.ApplyModifiedPropertiesWithoutUndo();

                var fx = root.GetComponent<PlayerCombineFx>();
                if (!fx) fx = root.AddComponent<PlayerCombineFx>();
                var fxFields = new SerializedObject(fx);
                fxFields.FindProperty("lettering").objectReferenceValue = DustSprite("ka-thump-lettering");
                fxFields.FindProperty("lightning").objectReferenceValue = DustSprite("lightning");
                SetSprites(fxFields.FindProperty("stars"), "star-high", "star-low");
                SetSprites(fxFields.FindProperty("limbs"), "bear-paw", "duck-foot", "axolotl-gill", "rabbit-ear-near");
                SetSprites(fxFields.FindProperty("puffs"), "puff-trail-01", "puff-trail-02", "puff-trail-03", "puff-trail-04");
                SetSprites(fxFields.FindProperty("speedLines"), "speed-upper-long", "speed-lower-long", "speed-bottom-long");
                fxFields.FindProperty("visual").objectReferenceValue = visual.transform;
                fxFields.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(Font);
                // Timing of the phased version (older prefabs kept the first draft's shorter values).
                fxFields.FindProperty("slamAt").floatValue = 0.18f;
                fxFields.FindProperty("burstAt").floatValue = 0.8f;
                fxFields.FindProperty("duration").floatValue = 1.35f;
                fxFields.FindProperty("hitStop").floatValue = 0.07f;
                fxFields.FindProperty("cloudRadius").vector2Value = new Vector2(0.82f, 0.86f);
                fxFields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("Player prefab now has the yellow outline, reroll stat popup and combine effect.");
        }

        static bool HasCombineFont(GameObject prefab)
        {
            var fx = prefab.GetComponent<PlayerCombineFx>();
            return fx && new SerializedObject(fx).FindProperty("font").objectReferenceValue;
        }

        static bool HasPhasedTiming(GameObject prefab)
        {
            var fx = prefab.GetComponent<PlayerCombineFx>();
            var popup = prefab.GetComponent<PlayerStatPopup>();
            return fx && popup && Mathf.Approximately(new SerializedObject(fx).FindProperty("burstAt").floatValue, 0.8f)
                && Mathf.Approximately(new SerializedObject(popup).FindProperty("startDelay").floatValue, 0.88f);
        }

        static Sprite DustSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Dust + name + ".png");

        static void SetSprites(SerializedProperty list, params string[] names)
        {
            list.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = DustSprite(names[i]);
        }
    }
}
#endif
