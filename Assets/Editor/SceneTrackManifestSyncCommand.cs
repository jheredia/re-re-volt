using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core;
using Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        if (existing.OverridesBuiltIn &&
            (!TrackCatalog.TryGet(existing.Id, out TrackDefinition builtIn) ||
             authoring.Laps != builtIn.LapCount || checkpointCount != builtIn.OrderedCheckpointCount))
            throw new InvalidOperationException("A built-in scene override must keep its original lap and checkpoint counts.");
        string preview = existing.OverridesBuiltIn ? existing.PreviewMetadata :
            $"SCENE-AUTHORED / {checkpointCount} CHECKPOINTS";
        var updated = new SceneTrackRecord(existing.Id, existing.DisplayName, existing.Description,
            preview, existing.ScenePath,
            authoring.ContentRevision, authoring.Laps, checkpointCount, existing.OverridesBuiltIn);
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

/// <summary>Opts one saved generated-track bake into the playable catalog under its stable ID.</summary>
public static class BakedTrackActivationCommand
{
    [MenuItem("Tools/ReReVolt/Tracks/Activate Open Baked Track Scene")]
    private static void OpenWindow() => EditorWindow.GetWindow<BakedTrackActivationWindow>("Activate Baked Track");

    public static string Activate(string requestedRevision)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before activating a baked track.");
        string revision = requestedRevision?.Trim();
        if (!SceneTrackManifest.IsValidRevision(revision))
            throw new ArgumentException("Enter a new content revision using 1 to 64 letters, digits, periods, underscores, or hyphens.", nameof(requestedRevision));

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) || scene.isDirty ||
            SceneManager.sceneCount != 1 || !File.Exists(scene.path))
            throw new InvalidOperationException("Open the saved baked track scene alone and save all edits before activation.");
        GameObject[] roots = scene.GetRootGameObjects();
        if (roots.Length != 1 || !roots[0].TryGetComponent(out TrackAuthoringRoot authoring))
            throw new InvalidOperationException("The scene needs exactly one TrackAuthoringRoot as its only root.");
        string id = authoring.StableId;
        if (!SceneTrackManifest.IsBuiltInTrackId(id) ||
            !string.Equals(scene.path, SceneTrackManifest.ScenePathFor(id), StringComparison.Ordinal))
            throw new InvalidOperationException("The open scene path and root ID must match a baked built-in track.");
        TrackDefinition generated = TrackCatalog.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Id, id, StringComparison.Ordinal));
        if (generated == null || !TrackCatalog.TryGet(id, out TrackDefinition current) || current.IsSceneAuthored)
            throw new InvalidOperationException("This built-in track has already been activated or is unavailable.");
        if (string.Equals(revision, authoring.ContentRevision, StringComparison.Ordinal) ||
            string.Equals(revision, current.ContentRevision, StringComparison.Ordinal))
            throw new InvalidOperationException("Choose a new content revision for the scene-backed track.");
        int checkpointCount = authoring.GetComponentsInChildren<RaceCheckpoint>(true).Length;
        if (authoring.Laps != generated.LapCount || checkpointCount != generated.OrderedCheckpointCount)
            throw new InvalidOperationException("The baked track must retain its original lap and checkpoint counts.");
        // Includes indexed grid, recovery and pickup anchors, checkpoint references, surfaces and renderer markers.
        if (!TrackAuthoringEditorTools.TryValidateScene(scene, out string report))
            throw new InvalidOperationException("The saved baked track is invalid:\n" + report);

        if (!File.Exists(SceneTrackManifest.AssetPath))
            throw new FileNotFoundException("The scene track manifest is missing.", SceneTrackManifest.AssetPath);
        string previousJson = File.ReadAllText(SceneTrackManifest.AssetPath);
        SceneTrackManifest manifest = SceneTrackManifest.Parse(previousJson);
        if (manifest.Tracks.Any(track => string.Equals(track.Id, id, StringComparison.Ordinal)))
            throw new InvalidOperationException("The manifest already registers this stable track ID.");
        var overrideRecord = new SceneTrackRecord(id, generated.DisplayName, generated.FlowDescription,
            generated.PreviewMetadata, scene.path, revision, generated.LapCount,
            generated.OrderedCheckpointCount, true);
        string updatedJson = manifest.WithAdded(overrideRecord).ToJson();

        EditorBuildSettingsScene[] previousBuildScenes = EditorBuildSettings.scenes;
        int buildSceneIndex = -1;
        for (int i = 0; i < previousBuildScenes.Length; i++)
        {
            if (!string.Equals(previousBuildScenes[i].path, scene.path, StringComparison.Ordinal)) continue;
            if (buildSceneIndex >= 0)
                throw new InvalidOperationException("The baked scene appears more than once in Build Settings.");
            buildSceneIndex = i;
        }
        var nextBuildScenes = new List<EditorBuildSettingsScene>(previousBuildScenes);
        if (buildSceneIndex < 0) nextBuildScenes.Add(new EditorBuildSettingsScene(scene.path, true));
        else nextBuildScenes[buildSceneIndex] = new EditorBuildSettingsScene(scene.path, true);

        byte[] previousSceneBytes = File.ReadAllBytes(scene.path);
        bool sceneTouched = false;
        bool buildTouched = false;
        bool manifestTouched = false;
        try
        {
            sceneTouched = true;
            authoring.Configure(id, generated.LapCount, revision);
            EditorUtility.SetDirty(authoring);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Unity could not save the updated baked track scene.");

            buildTouched = true;
            EditorBuildSettings.scenes = nextBuildScenes.ToArray();
            manifestTouched = true;
            File.WriteAllText(SceneTrackManifest.AssetPath, updatedJson);
            AssetDatabase.ImportAsset(SceneTrackManifest.AssetPath, ImportAssetOptions.ForceUpdate);
            TrackCatalog.ReloadSceneManifest(updatedJson);
            if (!TrackCatalog.TryGet(id, out TrackDefinition activated) ||
                !activated.IsSceneAuthored ||
                !string.Equals(activated.ScenePath, scene.path, StringComparison.Ordinal) ||
                !string.Equals(activated.ContentRevision, revision, StringComparison.Ordinal))
                throw new InvalidOperationException("The catalog did not accept the baked track override.");
        }
        catch (Exception activationError)
        {
            var rollbackErrors = new List<string>();
            if (manifestTouched) TryRestore(() =>
            {
                File.WriteAllText(SceneTrackManifest.AssetPath, previousJson);
                AssetDatabase.ImportAsset(SceneTrackManifest.AssetPath, ImportAssetOptions.ForceUpdate);
                TrackCatalog.ReloadSceneManifest(previousJson);
            }, "manifest", rollbackErrors);
            if (buildTouched) TryRestore(() => EditorBuildSettings.scenes = previousBuildScenes,
                "build scene list", rollbackErrors);
            if (sceneTouched) TryRestore(() =>
            {
                File.WriteAllBytes(scene.path, previousSceneBytes);
                AssetDatabase.ImportAsset(scene.path, ImportAssetOptions.ForceUpdate);
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            }, "baked scene", rollbackErrors);
            if (rollbackErrors.Count != 0)
                throw new InvalidOperationException("Activation failed: " + activationError.Message +
                    ". Restoration also failed for " + string.Join(", ", rollbackErrors) + ".", activationError);
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

public sealed class BakedTrackActivationWindow : EditorWindow
{
    private string revision = string.Empty;

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Open a saved baked track scene before activation.", EditorStyles.wordWrappedLabel);
        revision = EditorGUILayout.TextField("New content revision", revision);
        if (!GUILayout.Button("Activate Baked Track")) return;
        try
        {
            string path = BakedTrackActivationCommand.Activate(revision);
            Debug.Log("[TrackAuthoring] Activated scene-backed built-in track: " + path);
            Close();
        }
        catch (Exception exception)
        {
            Debug.LogError("[TrackAuthoring] Could not activate baked track: " + exception);
            EditorUtility.DisplayDialog("Baked track was not activated", exception.Message, "OK");
        }
    }
}
