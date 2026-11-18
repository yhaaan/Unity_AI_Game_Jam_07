using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds Windows and macOS players into Builds/<yyMMddHHmm>/{Windows,Mac}.
// Menu: Tools/RTD/Build Windows + Mac. Batch: -executeMethod BuildAll.BuildWindowsAndMac
public static class BuildAll
{
    [MenuItem("Tools/RTD/Build Windows + Mac")]
    public static void BuildWindowsAndMac()
    {
        string root = Path.Combine("Builds", DateTime.Now.ToString("yyMMddHHmm"));
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var original = EditorUserBuildSettings.activeBuildTarget;
        var originalGroup = BuildPipeline.GetBuildTargetGroup(original);

        bool ok = Build(scenes, BuildTarget.StandaloneWindows64, Path.Combine(root, "Windows", PlayerSettings.productName + ".exe"))
                & Build(scenes, BuildTarget.StandaloneOSX, Path.Combine(root, "Mac", PlayerSettings.productName + ".app"));

        EditorUserBuildSettings.SwitchActiveBuildTarget(originalGroup, original);
        Debug.Log($"[BuildAll] {(ok ? "All builds succeeded" : "Some builds FAILED")} -> {Path.GetFullPath(root)}");
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool Build(string[] scenes, BuildTarget target, string path)
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            target = target,
            targetGroup = BuildTargetGroup.Standalone,
            locationPathName = path,
            options = BuildOptions.None,
        });
        var s = report.summary;
        Debug.Log($"[BuildAll] {target}: {s.result}, {s.totalErrors} errors, {s.totalSize / (1024 * 1024)} MB -> {path}");
        return s.result == BuildResult.Succeeded;
    }
}
