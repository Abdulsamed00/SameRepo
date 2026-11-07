using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Menü: Same > Build Windows. Komut satırı: -executeMethod BuildGame.BuildWindows
public static class BuildGame
{
    const string OutputDir = "Builds/Same";

    [MenuItem("Same/Build Windows")]
    public static void BuildWindows()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string exe = Path.Combine(OutputDir, "Same.exe");
        Directory.CreateDirectory(OutputDir);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"Build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime}, hata {summary.totalErrors}");

        if (Application.isBatchMode)
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        else if (summary.result == BuildResult.Succeeded)
            EditorUtility.RevealInFinder(Path.GetFullPath(exe));
    }
}
