using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Returns an activated built-in course to its generated catalog entry.</summary>
public static class BakedTrackDeactivationCommand
{
    [MenuItem("Tools/ReReVolt/Tracks/Deactivate Open Baked Track Scene")]
    private static void DeactivateFromMenu()
    {
        try
        {
            string path = Deactivate();
            Debug.Log("[TrackAuthoring] Restored the generated course for " + path +
                      ". The editable scene and its assets were kept.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[TrackAuthoring] Could not deactivate baked track: " + exception);
            EditorUtility.DisplayDialog("Baked track remains active", exception.Message, "OK");
        }
    }

    public static string Deactivate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before deactivating a baked track.");
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) ||
            scene.isDirty || SceneManager.sceneCount != 1 || !File.Exists(scene.path))
            throw new InvalidOperationException("Open the saved baked track scene alone and save all edits first.");
        GameObject[] roots = scene.GetRootGameObjects();
        if (roots.Length != 1 || !roots[0].TryGetComponent(out TrackAuthoringRoot authoring))
            throw new InvalidOperationException("The scene needs one TrackAuthoringRoot as its only root.");
        string id = authoring.StableId;
        if (!SceneTrackManifest.IsBuiltInTrackId(id) ||
            !string.Equals(scene.path, SceneTrackManifest.ScenePathFor(id), StringComparison.Ordinal))
            throw new InvalidOperationException("The open scene path and root ID must match a built-in baked track.");
        if (!File.Exists(SceneTrackManifest.AssetPath))
            throw new FileNotFoundException("The scene track manifest is missing.", SceneTrackManifest.AssetPath);
        string previousJson = File.ReadAllText(SceneTrackManifest.AssetPath);
        SceneTrackManifest manifest = SceneTrackManifest.Parse(previousJson);
        SceneTrackRecord currentRecord = manifest.Tracks.FirstOrDefault(track =>
            string.Equals(track.Id, id, StringComparison.Ordinal));
        if (currentRecord == null || !currentRecord.OverridesBuiltIn ||
            !string.Equals(currentRecord.ScenePath, scene.path, StringComparison.Ordinal) ||
            !TrackCatalog.TryGet(id, out TrackDefinition current) || !current.IsSceneAuthored)
            throw new InvalidOperationException("This baked track is not an active built-in scene override.");
        string nextJson = manifest.Without(id).ToJson();

        EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
        int sceneIndex = -1;
        for (int index = 0; index < previousScenes.Length; index++)
        {
            if (!string.Equals(previousScenes[index].path, scene.path, StringComparison.Ordinal)) continue;
            if (sceneIndex >= 0)
                throw new InvalidOperationException("The baked scene appears more than once in Build Settings.");
            sceneIndex = index;
        }
        if (sceneIndex < 0 || !previousScenes[sceneIndex].enabled)
            throw new InvalidOperationException("The active baked scene must be enabled in Build Settings.");
        var nextScenes = (EditorBuildSettingsScene[])previousScenes.Clone();
        // Keep its list position: activation may have enabled an existing entry.
        // Disabling is reversible without guessing whether that entry predated activation.
        nextScenes[sceneIndex] = new EditorBuildSettingsScene(scene.path, false);

        bool buildTouched = false;
        bool manifestTouched = false;
        try
        {
            buildTouched = true;
            EditorBuildSettings.scenes = nextScenes;
            manifestTouched = true;
            File.WriteAllText(SceneTrackManifest.AssetPath, nextJson);
            AssetDatabase.ImportAsset(SceneTrackManifest.AssetPath, ImportAssetOptions.ForceUpdate);
            TrackCatalog.ReloadSceneManifest(nextJson);
            if (!TrackCatalog.TryGet(id, out TrackDefinition restored) || restored.IsSceneAuthored ||
                !restored.IsAvailable ||
                !TrackCatalog.Entries.Any(generated => ReferenceEquals(generated, restored)))
                throw new InvalidOperationException("The catalog did not restore the generated built-in course.");
        }
        catch (Exception deactivationError)
        {
            var rollbackErrors = new List<string>();
            if (manifestTouched) TryRestore(() =>
            {
                File.WriteAllText(SceneTrackManifest.AssetPath, previousJson);
                AssetDatabase.ImportAsset(SceneTrackManifest.AssetPath, ImportAssetOptions.ForceUpdate);
                TrackCatalog.ReloadSceneManifest(previousJson);
            }, "manifest", rollbackErrors);
            if (buildTouched) TryRestore(() => EditorBuildSettings.scenes = previousScenes,
                "build scene list", rollbackErrors);
            if (rollbackErrors.Count != 0)
                throw new InvalidOperationException("Deactivation failed: " + deactivationError.Message +
                    ". Restoration also failed for " + string.Join(", ", rollbackErrors) + ".", deactivationError);
            throw;
        }
        return scene.path;
    }

    private static void TryRestore(Action action, string label, List<string> errors)
    {
        try { action(); }
        catch (Exception exception) { errors.Add(label + " (" + exception.Message + ")"); }
    }
}
