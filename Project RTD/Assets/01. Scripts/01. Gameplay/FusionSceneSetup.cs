#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Tools/RTD/Build Fusion Scene: a standalone scene that loops the fusion tank (parts whirl → STOP!! → FUSION!!).
    internal static class FusionSceneSetup
    {
        const string ScenePath = "Assets/00. Scenes/FusionScene.unity";
        const string CatalogPath = "Assets/06. Data/PartCatalog.asset";

        [MenuItem("Tools/RTD/Build Fusion Scene")]
        internal static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                cam.tag = "MainCamera";
                cam.orthographic = true;
                cam.orthographicSize = 5.4f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(0x9a, 0xa6, 0xe0, 0xff);
                cam.transform.position = new Vector3(0f, 0f, -10f);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cam.gameObject, scene);

                var go = new GameObject("Fusion Tank");
                var seq = go.AddComponent<FusionTankSequence>();
                var so = new SerializedObject(seq);
                so.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PartCatalog>(CatalogPath);
                so.FindProperty("loop").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("Fusion scene saved: " + ScenePath);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
#endif
