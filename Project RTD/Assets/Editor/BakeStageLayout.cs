using System.Linq;
using ProjectRTD.Core;
using ProjectRTD.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Saves the stage layout into every stage scene so it can be seen and edited in the Scene view before Play:
//   * course: widened floors + lab floor art, pit floors, moved spawn/player/goal, invisible side walls and
//     the camera bounds on the Cinemachine camera ("Course Boundaries")
//   * dressing: entrance/exit doors, jump wall pillars, crate/plate/cupboard art
//   * cleanup: deletes the placeholders this art replaces and the retired "Assembly Canvas" panel
// Each step skips what a scene already has, so running it again is safe.
// Menu: Tools/RTD/Bake Stage Layout (All Stages)
public static class BakeStageLayout
{
    const string Folder = "Assets/00. Scenes";

    [MenuItem("Tools/RTD/Bake Stage Layout (All Stages)")]
    public static void BakeAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var restore = EditorSceneManager.GetSceneManagerSetup();
        // StageCourseLayout/StageDressing find the art through SharedMenuArt.Loaded, which needs the asset in memory.
        var art = AssetDatabase.LoadAssetAtPath<SharedMenuArt>("Assets/05. Arts/SharedMenuArt.asset");
        int baked = 0;
        foreach (var path in AssetDatabase.FindAssets("t:Scene Stage", new[] { Folder })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(p => System.Text.RegularExpressions.Regex.IsMatch(System.IO.Path.GetFileName(p), @"^Stage\d+\.unity$"))
                     .OrderBy(p => p))
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var stage = scene.GetRootGameObjects().Select(o => o.GetComponentInChildren<StageRuntime>(true)).FirstOrDefault(s => s);
            if (!stage) { Debug.LogWarning($"[BakeStageLayout] {path}: no StageRuntime"); continue; }

            if (!stage.transform.Find("Course Boundaries")) StageCourseLayout.Apply(stage);
            else StageCourseLayout.RetirePlaceholders(stage);
            StageDressing.Build(stage);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Assembly Canvas") Object.DestroyImmediate(root);

            // Changes made from code to prefab instances (player, camera...) must be recorded or they are lost.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                    if (c && PrefabUtility.IsPartOfPrefabInstance(c))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            baked++;
            Debug.Log($"[BakeStageLayout] {path}: baked");
        }
        if (restore.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(restore);
        Debug.Log($"[BakeStageLayout] done, {baked} scene(s) baked (art {(art && art.labFloor && art.doorSheet ? "ok" : "MISSING")})");
    }
}
