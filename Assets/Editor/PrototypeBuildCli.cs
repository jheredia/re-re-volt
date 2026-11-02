using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

/// <summary>Reproducible command-line player build without changing build profiles.</summary>
public static class PrototypeBuildCli
{
    public static void BuildWebGL()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new InvalidOperationException("Launch Unity with -buildTarget webgl.");

        string output = RequiredOutputPath();
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes in Editor Build Settings.");
        if (Directory.Exists(output) || File.Exists(output))
            throw new InvalidOperationException("Build output already exists: " + output);

        Directory.CreateDirectory(Path.GetDirectoryName(output));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("WebGL build failed: " + report.summary.result);

        UnityEngine.Debug.Log("[PrototypeBuildCli] WebGL build ready: " + output);
    }

    private static string RequiredOutputPath()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
            if (args[index] == "-prototypeBuildOutput")
            {
                string path = Path.GetFullPath(args[index + 1]);
                string assets = Path.GetFullPath(UnityEngine.Application.dataPath);
                if (path == assets || path.StartsWith(assets + Path.DirectorySeparatorChar,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException("Build output must be outside Assets.");
                return path;
            }

        throw new ArgumentException("Pass -prototypeBuildOutput followed by an output directory.");
    }
}
