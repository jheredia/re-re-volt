using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using Core;
using Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Imports a portable player-made layout and bakes an editable, build-ready track scene.</summary>
public static class InGameTrackSceneBakeCommand
{
    [MenuItem("Tools/ReReVolt/Tracks/Import In-Game Layout and Bake Scene")]
    private static void ImportFromMenu()
    {
        string path = EditorUtility.OpenFilePanel("Choose an exported RC track JSON", "", "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            string scenePath = BakeFile(path);
            Debug.Log("[TrackAuthoring] Imported player layout as " + scenePath);
            EditorUtility.DisplayDialog("Track scene ready",
                "The layout is an editable scene and is registered for the next player build:\n" + scenePath,
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogError("[TrackAuthoring] Player layout bake failed: " + exception);
            EditorUtility.DisplayDialog("Track import failed", exception.Message, "OK");
        }
    }

    public static string BakeFile(string jsonPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before importing a layout.");
        if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
            throw new FileNotFoundException("Choose an exported track JSON file.", jsonPath);
        string localId = Path.GetFileNameWithoutExtension(jsonPath);
        var store = new InGameTrackDraftStore(storageDirectory: Path.GetDirectoryName(jsonPath));
        if (!store.TryLoad(localId, out InGameTrackDraft draft, out string error))
            throw new InvalidOperationException("Could not import this layout: " + error);
        IReadOnlyList<string> issues = InGameTrackRouteValidator.Validate(draft);
        if (issues.Count != 0)
            throw new InvalidOperationException("Layout is not raceable: " + string.Join("; ", issues));

        string id = TrackCatalog.LocalDraftTrackId(draft.LocalId);
        string revision = InGameTrackDraftStore.ContentRevision(draft);
        if (!SceneTrackManifest.IsValidId(id) || !SceneTrackManifest.IsValidRevision(revision))
            throw new InvalidOperationException("The layout's stable ID or content revision is invalid.");
        string folder = SceneTrackManifest.SceneFolder + "/" + id;
        string scenePath = SceneTrackManifest.ScenePathFor(id);
        if (AssetDatabase.IsValidFolder(folder) || Directory.Exists(folder) || File.Exists(scenePath))
            throw new IOException("A baked scene already exists for this layout ID. Make a copy with a new ID first.");
        if (!File.Exists(SceneTrackManifest.AssetPath))
            throw new FileNotFoundException("The scene track manifest is missing.", SceneTrackManifest.AssetPath);
        string previousManifest = File.ReadAllText(SceneTrackManifest.AssetPath);
        SceneTrackManifest manifest = SceneTrackManifest.Parse(previousManifest);
        if (TrackCatalog.TryGet(id, out _) || manifest.Tracks.Any(track => track.Id == id))
            throw new InvalidOperationException("This track ID is already registered.");

        Scene originalScene = SceneManager.GetActiveScene();
        if (!originalScene.IsValid() || !originalScene.isLoaded)
            throw new InvalidOperationException("Open a Unity Editor scene before importing a layout.");
        UnityEngine.Object originalSelection = Selection.activeObject;
        EditorBuildSettingsScene[] previousBuildScenes = EditorBuildSettings.scenes;
        Scene temporaryScene = default;
        ITrackRuntime runtime = null;
        GameObject raceObject = null;
        bool folderCreated = false;
        bool manifestChanged = false;
        bool buildChanged = false;
        Exception failure = null;
        var cleanupErrors = new List<string>();
        try
        {
            if (!AssetDatabase.IsValidFolder(SceneTrackManifest.SceneFolder))
            {
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets/Scenes", "CustomTracks")))
                    throw new IOException("Could not create the custom track folder.");
            }
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(SceneTrackManifest.SceneFolder, id)))
                throw new IOException("Could not create this track's asset folder.");
            folderCreated = true;

            temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (!SceneManager.SetActiveScene(temporaryScene))
                throw new InvalidOperationException("Could not activate the isolated bake scene.");
            raceObject = new GameObject("TemporaryLayoutBakeRace");
            RaceManager race = raceObject.AddComponent<RaceManager>();
            if (!race.ConfigureRace(3, draft.Layout.Route.Count))
                throw new InvalidOperationException("Could not configure the temporary race contract.");
            runtime = InGameTrackRuntimeBuilder.Build(draft, race);
            if (runtime?.Root == null || runtime.Grid?.Length != 4 ||
                runtime.Checkpoints?.Length != draft.Layout.Route.Count ||
                runtime.PickupPositions?.Count != 4)
                throw new InvalidOperationException("The layout builder returned an incomplete track.");
            GameObject root = runtime.Root;
            root.name = "TrackAuthoringRoot";
            TrackAuthoringRoot authoring = root.AddComponent<TrackAuthoringRoot>();
            authoring.Configure(id, 3, revision);
            for (int i = 0; i < runtime.Grid.Length; i++)
                runtime.Grid[i].gameObject.AddComponent<TrackAuthoringAnchor>()
                    .Configure(TrackAnchorKind.Grid, i);
            for (int i = 0; i < runtime.Checkpoints.Length; i++)
            {
                RaceCheckpoint checkpoint = runtime.Checkpoints[i];
                if (checkpoint == null || checkpoint.CheckpointIndex != i)
                    throw new InvalidOperationException("Checkpoint order is incomplete at " + i);
                Transform recovery = new SerializedObject(checkpoint).FindProperty("recoveryAnchor")
                    .objectReferenceValue as Transform;
                if (recovery == null || recovery.parent != root.transform)
                    throw new InvalidOperationException("Checkpoint " + i + " has no owned recovery anchor.");
                recovery.gameObject.AddComponent<TrackAuthoringAnchor>()
                    .Configure(TrackAnchorKind.Recovery, i);
                checkpoint.Configure(null, i, recovery);
                checkpoint.GetComponent<Collider>().enabled = false;
            }
            for (int i = 0; i < runtime.PickupPositions.Count; i++)
            {
                GameObject pickup = new($"Pickup_{i:00}");
                pickup.transform.SetParent(root.transform, false);
                pickup.transform.position = runtime.PickupPositions[i];
                pickup.AddComponent<TrackAuthoringAnchor>().Configure(TrackAnchorKind.Pickup, i);
            }
            UnityEngine.Object.DestroyImmediate(raceObject);
            raceObject = null;
            Stage9CGeneratedTrackBakeCommand.PersistRuntimeAssets(authoring, folder);
            Stage9CGeneratedTrackBakeCommand.ConfigureVisuals(authoring, runtime);
            if (!TrackAuthoringEditorTools.TryValidateScene(temporaryScene, out string report))
                throw new InvalidOperationException("Baked track scene failed validation:\n" + report);
            if (!EditorSceneManager.SaveScene(temporaryScene, scenePath))
                throw new IOException("Unity could not save the track scene.");

            var record = new SceneTrackRecord(id, draft.Name.ToUpperInvariant(),
                "PLAYER-BUILT CLUB LOOP", $"BAKED LOCAL LAYOUT / {runtime.Checkpoints.Length} CHECKPOINTS",
                scenePath, revision, 3, runtime.Checkpoints.Length);
            string updatedManifest = manifest.WithAdded(record).ToJson();
            var scenes = new List<EditorBuildSettingsScene>(previousBuildScenes)
            { new(scenePath, true) };
            EditorBuildSettings.scenes = scenes.ToArray();
            buildChanged = true;
            manifestChanged = true;
            File.WriteAllText(SceneTrackManifest.AssetPath, updatedManifest);
            AssetDatabase.ImportAsset(SceneTrackManifest.AssetPath, ImportAssetOptions.ForceUpdate);
            TrackCatalog.ReloadSceneManifest(updatedManifest);
            AssetDatabase.SaveAssets();
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            TryCleanup(() =>
            {
                if (raceObject != null) UnityEngine.Object.DestroyImmediate(raceObject);
            }, "temporary race", cleanupErrors);
            TryCleanup(() =>
            {
                if (SceneManager.GetActiveScene() != originalScene &&
                    !SceneManager.SetActiveScene(originalScene))
                    throw new InvalidOperationException("Could not reactivate the original scene.");
            }, "original scene", cleanupErrors);
            TryCleanup(() =>
            {
                if (temporaryScene.IsValid() && temporaryScene.isLoaded &&
                    !EditorSceneManager.CloseScene(temporaryScene, true))
                    throw new InvalidOperationException("Could not close the temporary scene.");
            }, "temporary scene", cleanupErrors);
            TryCleanup(() => runtime?.Dispose(), "generated runtime", cleanupErrors);
            TryCleanup(() => Selection.activeObject = originalSelection, "selection", cleanupErrors);
        }

        if (failure == null && cleanupErrors.Count == 0) return scenePath;
        // The temporary scene is closed before deleting its new assets. Never touch
        // the preexisting manifest, build list, or author's active scene on success.
        if (manifestChanged)
            TryCleanup(() =>
            {
                File.WriteAllText(SceneTrackManifest.AssetPath, previousManifest);
                AssetDatabase.ImportAsset(SceneTrackManifest.AssetPath, ImportAssetOptions.ForceUpdate);
                TrackCatalog.ReloadSceneManifest(previousManifest);
            }, "manifest rollback", cleanupErrors);
        if (buildChanged)
            TryCleanup(() => EditorBuildSettings.scenes = previousBuildScenes,
                "build list rollback", cleanupErrors);
        if (folderCreated)
            TryCleanup(() =>
            {
                if (!AssetDatabase.DeleteAsset(folder))
                    throw new IOException("Could not remove the incomplete track folder: " + folder);
            }, "new track assets rollback", cleanupErrors);
        if (cleanupErrors.Count != 0)
            throw new InvalidOperationException("Track bake failed; cleanup also failed for " +
                string.Join(", ", cleanupErrors) + ".", failure);
        ExceptionDispatchInfo.Capture(failure).Throw();
        throw new InvalidOperationException("Track bake failed without an error.");
    }

    private static void TryCleanup(Action action, string label, List<string> errors)
    {
        try { action(); }
        catch (Exception exception) { errors.Add(label + " (" + exception.Message + ")"); }
    }
}
