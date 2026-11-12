using System;
using System.IO;
using System.Linq;
using Core;
using Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Copies saved scene race metadata into one existing manifest entry.</summary>
public static class SceneTrackManifestSyncCommand
{
    private const string MenuPath = "Tools/ReReVolt/Tracks/Sync Open Scene Track Registration";

    [MenuItem(MenuPath)]
    private static void SyncOpenScene()
    {
        try
        {
            string result = Synchronize();
            Debug.Log("[TrackAuthoring] " + result);
        }
        catch (Exception exception)
        {
            Debug.LogError("[TrackAuthoring] Could not synchronize scene track: " + exception);
            EditorUtility.DisplayDialog("Scene track was not synchronized", exception.Message, "OK");
        }
    }

    private static string Synchronize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before synchronizing a track.");
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            throw new InvalidOperationException("Open a saved custom track scene first.");
        if (string.Equals(scene.path, TrackAuthoringEditorTools.TemplateScenePath, StringComparison.Ordinal))
            throw new InvalidOperationException("The beta template is registered separately and cannot be synchronized here.");
        if (!scene.path.StartsWith(SceneTrackManifest.SceneFolder + "/", StringComparison.Ordinal))
            throw new InvalidOperationException("The open scene is not a registered custom track scene.");
        if (scene.isDirty)
            throw new InvalidOperationException("Save the open scene before synchronizing its registration.");

        GameObject[] roots = scene.GetRootGameObjects();
        if (roots.Length != 1 || !roots[0].TryGetComponent(out TrackAuthoringRoot authoring))
            throw new InvalidOperationException("The scene needs exactly one TrackAuthoringRoot as its only root.");
        if (!SceneTrackManifest.IsValidId(authoring.StableId) ||
            !string.Equals(scene.path, SceneTrackManifest.ScenePathFor(authoring.StableId), StringComparison.Ordinal))
            throw new InvalidOperationException("The root stable ID must match the registered scene path.");
        if (!SceneTrackManifest.IsValidRevision(authoring.ContentRevision))
            throw new InvalidOperationException("The root content revision must use 1 to 64 letters, digits, periods, underscores, or hyphens.");
        if (!TrackAuthoringEditorTools.TryValidateScene(scene, out string report))
            throw new InvalidOperationException("The saved track scene is invalid:\n" + report);

        if (!File.Exists(SceneTrackManifest.AssetPath))
            throw new FileNotFoundException("The scene track manifest is missing.", SceneTrackManifest.AssetPath);
        string previousJson = File.ReadAllText(SceneTrackManifest.AssetPath);
        SceneTrackManifest manifest = SceneTrackManifest.Parse(previousJson);
        SceneTrackRecord existing = manifest.Tracks.FirstOrDefault(track =>
            string.Equals(track.Id, authoring.StableId, StringComparison.Ordinal));
        if (existing == null || !string.Equals(existing.ScenePath, scene.path, StringComparison.Ordinal))
            throw new InvalidOperationException("This scene has no matching custom track manifest entry.");
        if (!EditorBuildSettings.scenes.Any(buildScene =>
                buildScene.enabled && string.Equals(buildScene.path, scene.path, StringComparison.Ordinal)))
            throw new InvalidOperationException("This track scene must be enabled in the build scene list.");

        int checkpointCount = authoring.GetComponentsInChildren<RaceCheckpoint>(true).Length;
        var updated = new SceneTrackRecord(existing.Id, existing.DisplayName, existing.Description,
            $"SCENE-AUTHORED / {checkpointCount} CHECKPOINTS", existing.ScenePath,
            authoring.ContentRevision, authoring.Laps, checkpointCount);
        string updatedJson = manifest.WithReplaced(updated).ToJson();
        if (string.Equals(previousJson, updatedJson, StringComparison.Ordinal))
            return "Track registration is already current: " + scene.path;

        try
        {
            File.WriteAllText(SceneTrackManifest.AssetPath, updatedJson);
            AssetDatabase.ImportAsset(SceneTrackManifest.AssetPath, ImportAssetOptions.ForceUpdate);
            TrackCatalog.ReloadSceneManifest(updatedJson);
        }
        catch (Exception updateError)
        {
            try
            {
                File.WriteAllText(SceneTrackManifest.AssetPath, previousJson);
                AssetDatabase.ImportAsset(SceneTrackManifest.AssetPath, ImportAssetOptions.ForceUpdate);
                TrackCatalog.ReloadSceneManifest(previousJson);
            }
            catch (Exception rollbackError)
            {
                throw new InvalidOperationException("Manifest update failed and restoration also failed: " +
                    rollbackError.Message, updateError);
            }
            throw;
        }
        return "Synchronized scene track registration: " + scene.path;
    }
}
