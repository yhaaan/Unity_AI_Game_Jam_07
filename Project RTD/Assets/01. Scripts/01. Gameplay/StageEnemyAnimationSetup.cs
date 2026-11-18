#if UNITY_EDITOR
using ChimeraGroove;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRTD.Gameplay
{
    // Gives the Drone and Scientist prefabs an animated "Art" child (Chimera DroneAnimator / ScientistAnimator).
    // The flat composite sprite stays on the root for editor preview and is hidden in play mode.
    internal static class StageEnemyAnimationSetup
    {
        const string Prefabs = "Assets/02. Prefabs/";
        const string ArtName = "Art";
        const string ScientistId = "ray";
        // Match the flat sprites' world size: they were imported at 450 (scientist) and 480 (drone) pixels per unit.
        const float FlatScientistPpu = 450f, FlatDronePpu = 480f;

        [InitializeOnLoadMethod]
        static void AutoInstall() => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var kind in new[] { PlatformEnemy.Kind.Drone, PlatformEnemy.Kind.Scientist })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + kind + ".prefab");
                if (prefab && !prefab.transform.Find(ArtName)) { Install(); return; }
            }
        };

        [MenuItem("Tools/RTD/Animate Stage01 enemies")]
        static void Install()
        {
            foreach (var kind in new[] { PlatformEnemy.Kind.Drone, PlatformEnemy.Kind.Scientist })
            {
                string path = Prefabs + kind + ".prefab";
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var old = root.transform.Find(ArtName);
                    if (old) Object.DestroyImmediate(old.gameObject);
                    var art = new GameObject(ArtName).transform;
                    art.SetParent(root.transform, false);
                    var group = art.gameObject.AddComponent<SortingGroup>();
                    group.sortingOrder = root.GetComponent<SpriteRenderer>().sortingOrder;

                    if (kind == PlatformEnemy.Kind.Scientist)
                    {
                        var sci = art.gameObject.AddComponent<ScientistAnimator>();
                        sci.id = ScientistId;
                        sci.state = ScientistState.Walk;
                        float height = EnemyMoves.Config(ScientistId).height;
                        art.localScale = Vector3.one * (height / FlatScientistPpu) / (EnemyMoves.SciHeight / sci.pixelsPerUnit);
                    }
                    else
                    {
                        var drone = art.gameObject.AddComponent<DroneAnimator>();
                        drone.hover = 20f;
                        art.localScale = Vector3.one * (drone.pixelsPerUnit / FlatDronePpu);
                    }

                    var settings = new SerializedObject(root.GetComponent<PlatformEnemy>());
                    settings.FindProperty("animatedArt").objectReferenceValue = art;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("Stage01 enemies now use the Chimera scientist and drone animators.");
        }
    }
}
#endif
