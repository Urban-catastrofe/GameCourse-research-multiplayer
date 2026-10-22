using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Menu (and CLI) build targets for the demo.
/// CLI examples:
///   Unity -batchmode -nographics -quit -projectPath . -executeMethod BuildDemo.BuildLinuxServer
///   Unity -batchmode -nographics -quit -projectPath . -executeMethod BuildDemo.BuildWindowsClient
/// </summary>
public static class BuildDemo
{
    private static readonly string[] Scenes = { "Assets/Scenes/SampleScene.unity" };

    [MenuItem("Demo/Build Linux Server")]
    public static void BuildLinuxServer()
    {
        const string dir = "Builds/LinuxServer";
        Directory.CreateDirectory(dir);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = dir + "/GameServer.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None,
        });
        Done(report, dir + "/GameServer.x86_64");
    }

    [MenuItem("Demo/Build Windows Client (needs Windows module)")]
    public static void BuildWindowsClient()
    {
        // Sensible defaults for a shareable test build.
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

        const string dir = "Builds/WindowsClient";
        Directory.CreateDirectory(dir);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = dir + "/TankDemo.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        Done(report, dir + "/TankDemo.exe");
    }

    private static void Done(BuildReport report, string path)
    {
        var s = report.summary;
        Debug.Log($"[BuildDemo] {s.result}: {path} ({s.totalSize / (1024f * 1024f):F1} MB, {s.totalTime})");
        if (Application.isBatchMode)
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }
}
