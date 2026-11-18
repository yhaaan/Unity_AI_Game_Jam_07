#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Gameplay
{
    internal static class StageEnemySetup
    {
        const string Stage = "Assets/00. Scenes/Stage01.unity";
        const string Prefabs = "Assets/02. Prefabs/";

        [InitializeOnLoadMethod]
        static void FirstInstall() => EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Prefabs+"Scientist.prefab")) Install();
        };

        [MenuItem("Tools/RTD/Install Stage01 enemies")]
        static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetSceneByPath(Stage);
            bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) { Debug.LogWarning("Save Stage01 and run Tools/RTD/Install Stage01 enemies."); return; }
            if (opened) scene = EditorSceneManager.OpenScene(Stage, OpenSceneMode.Additive);
            try
            {
                var stage = scene.GetRootGameObjects().Single(obj => obj.GetComponent<StageRuntime>());
                bool changed = false;
                foreach (var kind in new[] {PlatformEnemy.Kind.Drone, PlatformEnemy.Kind.Scientist})
                {
                    var ledge = stage.GetComponentsInChildren<BoxCollider2D>().SingleOrDefault(c => c.name == (kind == PlatformEnemy.Kind.Drone ? "Head Ledge B" : "Lower Floor A"));
                    if (!ledge) continue;
                    string path = Prefabs+kind+".prefab";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!prefab) prefab = Build(kind,path);
                    if (stage.GetComponentsInChildren<PlatformEnemy>(true).Any(enemy => PrefabUtility.GetCorrespondingObjectFromSource(enemy.gameObject) == prefab)) continue;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,stage.transform);
                    instance.transform.position = new Vector3(ledge.bounds.center.x,ledge.bounds.max.y,0);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    changed = true;
                }
                if (changed && !EditorSceneManager.SaveScene(scene)) throw new IOException(Stage);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene,true); }
        }

        static GameObject Build(PlatformEnemy.Kind kind,string path)
        {
            var recipe = "Assets/03. Sprites/"+kind+"Enemy.yaml";
            AssetDatabase.SetImporterOverride<ChimeraLayerImporter>(recipe);
            AssetDatabase.ImportAsset(recipe, ImportAssetOptions.ForceSynchronousImport);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(recipe);
            if (!sprite) throw new InvalidDataException(recipe);
            var go = new GameObject(kind.ToString());
            try
            {
                var enemy = go.AddComponent<PlatformEnemy>();
                var settings = new SerializedObject(enemy); settings.FindProperty("kind").enumValueIndex = (int)kind; settings.ApplyModifiedPropertiesWithoutUndo();
                var body = go.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.interpolation = RigidbodyInterpolation2D.Interpolate;
                var box = go.GetComponent<BoxCollider2D>(); box.isTrigger = true;
                box.size = kind == PlatformEnemy.Kind.Drone ? new Vector2(1.45f,.9f) : new Vector2(.95f,1.65f);
                box.offset = Vector2.up * box.size.y * .5f;
                var visual = new GameObject("Visual"); visual.transform.SetParent(go.transform, false);
                var art = visual.AddComponent<SpriteRenderer>(); art.sprite = sprite; art.sortingOrder = 8;
                return PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
#endif
