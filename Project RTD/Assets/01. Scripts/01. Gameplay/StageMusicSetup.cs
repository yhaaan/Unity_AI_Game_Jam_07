#if UNITY_EDITOR
using System.IO;
using ProjectRTD.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Gameplay
{
    // Puts the stage BGM in Stage01 (a "Stage Music" object) and imports the track as streamed Vorbis.
    internal static class StageMusicSetup
    {
        const string Stage = "Assets/00. Scenes/Stage01.unity";
        const string Track = "Assets/05. Arts/RTD_BGM_Stage_BureaucraticComposure.wav";
        const string ObjectName = "Stage Music";

        [InitializeOnLoadMethod]
        static void AutoInstall()
        {
            EditorApplication.delayCall += InstallIfMissing;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += InstallIfMissing;
            };
        }

        static void InstallIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Stage) && !File.ReadAllText(Stage).Contains("m_Name: " + ObjectName)) Install();
        }

        [MenuItem("Tools/RTD/Install Stage01 music")]
        static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var importer = (AudioImporter)AssetImporter.GetAtPath(Track);
            if (!importer) { Debug.LogWarning("Stage music track not imported yet: " + Track); return; }
            var settings = importer.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.Streaming || settings.compressionFormat != AudioCompressionFormat.Vorbis)
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Track);

            var scene = SceneManager.GetSceneByPath(Stage);
            bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) { Debug.LogWarning("Save Stage01 and run Tools/RTD/Install Stage01 music."); return; }
            if (opened) scene = EditorSceneManager.OpenScene(Stage, OpenSceneMode.Additive);
            try
            {
                foreach (var old in scene.GetRootGameObjects())
                    if (old.name == ObjectName) Object.DestroyImmediate(old);
                var go = new GameObject(ObjectName);
                SceneManager.MoveGameObjectToScene(go, scene);
                var music = go.AddComponent<StageMusic>();
                var fields = new SerializedObject(music);
                fields.FindProperty("clip").objectReferenceValue = clip;
                fields.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException(Stage);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            Debug.Log("Stage01 now plays " + Path.GetFileNameWithoutExtension(Track) + ".");
        }
    }
}
#endif
