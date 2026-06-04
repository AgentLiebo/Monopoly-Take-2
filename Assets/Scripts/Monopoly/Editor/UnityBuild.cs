#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonopolyTake2.Editor;

public static class UnityBuild
{
    private const string DefaultWindowsOutputPath = "Builds/Windows/MonopolyTake2.exe";
    private const string GeneratedScenePath = "Assets/Scenes/GeneratedMonopoly.unity";

    public static void BuildWindowsPlayer()
    {
        var outputPath = Environment.GetEnvironmentVariable("UNITY_BUILD_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = DefaultWindowsOutputPath;
        }

        BuildStandaloneWindows64(outputPath);
    }

    public static void BuildStandaloneWindows64(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Build output path cannot be empty.", nameof(outputPath));
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var scenePath = EnsureGeneratedScene();
        PlayerSettings.companyName = "Educational Project";
        PlayerSettings.productName = "Monopoly Take 2";
        PlayerSettings.SplashScreen.show = false;

        var buildOptions = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = fullOutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(buildOptions);
        var summary = report.summary;
        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Unity Windows build failed with result {summary.result}. See the Unity editor log for details.");
        }

        Debug.Log($"Built Monopoly Take 2 Windows player to {fullOutputPath} ({summary.totalSize} bytes). Note: Unity creates a data folder beside the .exe; keep them together when distributing.");
    }

    private static string EnsureGeneratedScene()
    {
        var sceneDirectory = Path.GetDirectoryName(GeneratedScenePath);
        if (!string.IsNullOrEmpty(sceneDirectory))
        {
            Directory.CreateDirectory(sceneDirectory);
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "GeneratedMonopoly";

        var controller = new GameObject("Monopoly Game Controller");
        controller.AddComponent<UnityGameController>();

        var ambientLight = new GameObject("Build Scene Light");
        var light = ambientLight.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.8f;
        ambientLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        EditorSceneManager.SaveScene(scene, GeneratedScenePath);
        return GeneratedScenePath;
    }
}
#endif
