using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-click build for Mission Mohakash.
/// Menu: Mission Mohakash > Build Windows EXE
///       Mission Mohakash > Build macOS App
///       Mission Mohakash > Build Both
///
/// Also supports command-line invocation from Unity:
///   Unity -batchmode -quit -projectPath . -executeMethod MohakashBuilder.BuildWindows
///   Unity -batchmode -quit -projectPath . -executeMethod MohakashBuilder.BuildMac
/// </summary>
public static class MohakashBuilder
{
    const string ProductName = "MissionMohakash";
    const string BuildRoot   = "Builds";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Intro.unity",
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/Mission1.unity",
    };

    // ── Menu items ─────────────────────────────────────────────────────────

    [MenuItem("Mission Mohakash/Build Windows EXE")]
    public static void BuildWindowsMenu() => BuildWindows();

    [MenuItem("Mission Mohakash/Build macOS App")]
    public static void BuildMacMenu() => BuildMac();

    [MenuItem("Mission Mohakash/Build Both (Windows + macOS)")]
    public static void BuildBothMenu() { BuildWindows(); BuildMac(); }

    // ── Build methods (also callable from -executeMethod) ─────────────────

    public static void BuildWindows()
    {
        string outDir = Path.Combine(BuildRoot, "Windows");
        string exe    = Path.Combine(outDir, ProductName + ".exe");
        Directory.CreateDirectory(outDir);

        var options = new BuildPlayerOptions
        {
            scenes           = Scenes,
            locationPathName = exe,
            target           = BuildTarget.StandaloneWindows64,
            options          = BuildOptions.None,
        };

        Run(options, "Windows EXE", exe);
    }

    public static void BuildMac()
    {
        string outDir = Path.Combine(BuildRoot, "macOS");
        string app    = Path.Combine(outDir, ProductName + ".app");
        Directory.CreateDirectory(outDir);

        var options = new BuildPlayerOptions
        {
            scenes           = Scenes,
            locationPathName = app,
            target           = BuildTarget.StandaloneOSX,
            options          = BuildOptions.None,
        };

        Run(options, "macOS App", app);
    }

    // ── Internal ───────────────────────────────────────────────────────────

    static void Run(BuildPlayerOptions opts, string label, string output)
    {
        Debug.Log($"[Build] Starting {label} → {Path.GetFullPath(output)}");
        var report = BuildPipeline.BuildPlayer(opts);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            double mb = summary.totalSize / 1_048_576.0;
            Debug.Log($"[Build] ✓ {label} succeeded in {summary.totalTime.TotalSeconds:F1}s  ({mb:F1} MB)  → {output}");

            // Open the output folder in Finder/Explorer after a successful build
            if (!Application.isBatchMode)
                EditorUtility.RevealInFinder(output);
        }
        else
        {
            string msg = $"[Build] ✗ {label} FAILED: {summary.result}  (errors: {summary.totalErrors})";
            Debug.LogError(msg);

            // In batch mode, exit with a non-zero code so CI knows it failed
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
