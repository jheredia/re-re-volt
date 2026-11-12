using System;
using System.Collections.Generic;
using System.IO;
using Core;
using Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Bakes the current generated Patio layout for editing without changing track registration.</summary>
public static class PatioSweepBakeCommand
{
    private const string MenuPath = "Tools/ReReVolt/Tracks/Bake Sunset Patio Sweep Scene";
    private const string Folder = SceneTrackManifest.SceneFolder + "/" + TrackCatalog.PatioSweepId;
    private static readonly string ScenePath = SceneTrackManifest.ScenePathFor(TrackCatalog.PatioSweepId);

    [MenuItem(MenuPath)]
    private static void BakeFromMenu()
    {
        try
        {
            string path = Bake();
            Debug.Log("[TrackAuthoring] Baked editable Patio scene at " + path +
                      ". The playable catalog and build scenes have not changed.");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[TrackAuthoring] Patio bake canceled; created assets were removed.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[TrackAuthoring] Patio bake failed: " + exception);
            EditorUtility.DisplayDialog("Patio scene was not baked", exception.Message, "OK");
        }
    }

    public static string Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before baking a track.");
        if (!TrackCatalog.TryGet(TrackCatalog.PatioSweepId, out TrackDefinition definition) ||
            definition.IsSceneAuthored)
            throw new InvalidOperationException("The generated Patio catalog entry is unavailable.");
        if (Directory.Exists(Folder) || AssetDatabase.IsValidFolder(Folder) || File.Exists(ScenePath))
            throw new IOException("The Patio destination already exists: " + Folder);

        Scene originalScene = SceneManager.GetActiveScene();
        if (!originalScene.IsValid() || !originalScene.isLoaded)
            throw new InvalidOperationException("An active Editor scene is required before baking.");
        UnityEngine.Object originalSelection = Selection.activeObject;
        Scene temporaryScene = default;
        ITrackRuntime runtime = null;
        bool createdParent = false;
        bool createdFolder = false;
        Exception failure = null;
        var cleanupErrors = new List<string>();

        try
        {
            CheckCanceled("Preparing isolated scene", 0f);
            if (!AssetDatabase.IsValidFolder(SceneTrackManifest.SceneFolder))
            {
                string parentGuid = AssetDatabase.CreateFolder("Assets/Scenes", "CustomTracks");
                createdParent = AssetDatabase.IsValidFolder(SceneTrackManifest.SceneFolder);
                if (string.IsNullOrEmpty(parentGuid))
                    throw new IOException("Could not create the custom tracks folder.");
            }
            string folderGuid = AssetDatabase.CreateFolder(SceneTrackManifest.SceneFolder,
                TrackCatalog.PatioSweepId);
            createdFolder = AssetDatabase.IsValidFolder(Folder);
            if (string.IsNullOrEmpty(folderGuid))
                throw new IOException("Could not create the Patio asset folder.");

            temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (!SceneManager.SetActiveScene(temporaryScene))
                throw new InvalidOperationException("Could not activate the temporary bake scene.");
            var raceObject = new GameObject("TemporaryPatioBakeRaceManager");
            RaceManager race = raceObject.AddComponent<RaceManager>();
            runtime = Stage9CAuthoredTrackBuilder.BuildPatioSweep(race, null, null);
            if (runtime.Root == null || runtime.Grid.Length != Stage9CAuthoredTrackBuilder.GridCount ||
                runtime.PickupPositions.Count != Stage9CAuthoredTrackBuilder.PickupCount ||
                runtime.Checkpoints.Length != definition.OrderedCheckpointCount)
                throw new InvalidOperationException("The generated Patio layout no longer matches its catalog contract.");

            GameObject rootObject = runtime.Root;
            rootObject.name = "TrackAuthoringRoot";
            TrackAuthoringRoot authoring = rootObject.AddComponent<TrackAuthoringRoot>();
            authoring.Configure(definition.Id, definition.LapCount, definition.ContentRevision);
            for (int index = 0; index < runtime.Grid.Length; index++)
                runtime.Grid[index].gameObject.AddComponent<TrackAuthoringAnchor>()
                    .Configure(TrackAnchorKind.Grid, index);
            for (int index = 0; index < runtime.Checkpoints.Length; index++)
            {
                RaceCheckpoint checkpoint = runtime.Checkpoints[index];
                if (checkpoint == null || checkpoint.CheckpointIndex != index)
                    throw new InvalidOperationException("Patio checkpoint order changed at index " + index);
                SerializedProperty recovery = new SerializedObject(checkpoint).FindProperty("recoveryAnchor");
                Transform anchor = recovery?.objectReferenceValue as Transform;
                if (anchor == null || anchor.parent != rootObject.transform)
                    throw new InvalidOperationException("Patio checkpoint " + index + " has no owned recovery pose.");
                anchor.gameObject.AddComponent<TrackAuthoringAnchor>()
                    .Configure(TrackAnchorKind.Recovery, index);
                checkpoint.Configure(null, index, anchor);
                checkpoint.GetComponent<Collider>().enabled = false;
            }
            for (int index = 0; index < runtime.PickupPositions.Count; index++)
            {
                var pickup = new GameObject($"Pickup_{index:00}");
                pickup.transform.SetParent(rootObject.transform, false);
                pickup.transform.position = runtime.PickupPositions[index];
                pickup.AddComponent<TrackAuthoringAnchor>().Configure(TrackAnchorKind.Pickup, index);
            }
            UnityEngine.Object.DestroyImmediate(raceObject);

            CheckCanceled("Persisting Patio materials and meshes", .45f);
            PersistRuntimeAssets(authoring);
            foreach (Renderer renderer in authoring.GetComponentsInChildren<Renderer>(true))
            {
                bool decoration = renderer.GetComponent<NearCameraPresentationOccludable>() is
                    { Kind: NearCameraOcclusionKind.Decoration };
                bool outline = decoration || renderer.gameObject.name.StartsWith("Barrier_", StringComparison.Ordinal);
                renderer.gameObject.AddComponent<TrackAuthoringVisual>().Configure(outline);
            }
            if (!TrackAuthoringEditorTools.TryValidateScene(temporaryScene, out string report))
                throw new InvalidOperationException("Baked Patio scene failed structural validation:\n" + report);
            CheckCanceled("Saving Patio scene", .85f);
            if (!EditorSceneManager.SaveScene(temporaryScene, ScenePath))
                throw new IOException("Unity could not save " + ScenePath);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            TryCleanup(() =>
            {
                if (!SceneManager.SetActiveScene(originalScene))
                    throw new InvalidOperationException("Could not reactivate the original scene.");
            }, "original active scene", cleanupErrors);
            TryCleanup(() =>
            {
                if (temporaryScene.IsValid() && temporaryScene.isLoaded &&
                    !EditorSceneManager.CloseScene(temporaryScene, true))
                    throw new InvalidOperationException("Could not close the temporary scene.");
            }, "temporary scene", cleanupErrors);
            TryCleanup(() => runtime?.Dispose(), "generated runtime", cleanupErrors);
            TryCleanup(() => Selection.activeObject = originalSelection, "selection", cleanupErrors);
        }

        if (failure != null || cleanupErrors.Count != 0)
        {
            if (createdFolder)
                TryCleanup(() =>
                {
                    if (!AssetDatabase.DeleteAsset(Folder)) throw new IOException("Asset deletion failed.");
                }, "new Patio assets", cleanupErrors);
            if (createdParent)
                TryCleanup(() =>
                {
                    if (!AssetDatabase.DeleteAsset(SceneTrackManifest.SceneFolder))
                        throw new IOException("Asset deletion failed.");
                }, "new custom tracks folder", cleanupErrors);
            if (cleanupErrors.Count != 0)
                throw new InvalidOperationException("Patio bake failed; cleanup also failed for " +
                    string.Join(", ", cleanupErrors) + ".", failure);
            if (failure != null) throw failure;
        }
        return ScenePath;
    }

    private static void PersistRuntimeAssets(TrackAuthoringRoot root)
    {
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Folder, "Materials")))
            throw new IOException("Could not create the Patio material folder.");
        var materialCopies = new Dictionary<Material, Material>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material source = materials[index];
                if (source == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(source))) continue;
                if (!materialCopies.TryGetValue(source, out Material copy))
                {
                    copy = new Material(source) { name = source.name };
                    string path = $"{Folder}/Materials/Material_{materialCopies.Count:00}.mat";
                    AssetDatabase.CreateAsset(copy, path);
                    if (AssetDatabase.LoadAssetAtPath<Material>(path) != copy)
                        throw new IOException("Could not persist Patio material: " + path);
                    materialCopies.Add(source, copy);
                }
                materials[index] = copy;
            }
            renderer.sharedMaterials = materials;
        }
        var meshCopies = new Dictionary<Mesh, Mesh>();
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            filter.sharedMesh = PersistMesh(filter.sharedMesh, meshCopies);
        foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>(true))
            collider.sharedMesh = PersistMesh(collider.sharedMesh, meshCopies);
        AssetDatabase.SaveAssets();
    }

    private static Mesh PersistMesh(Mesh source, Dictionary<Mesh, Mesh> copies)
    {
        if (source == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(source))) return source;
        if (copies.TryGetValue(source, out Mesh existing)) return existing;
        Mesh copy = UnityEngine.Object.Instantiate(source);
        copy.name = source.name;
        string path = $"{Folder}/Mesh_{copies.Count:00}.asset";
        AssetDatabase.CreateAsset(copy, path);
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != copy)
            throw new IOException("Could not persist Patio mesh: " + path);
        copies.Add(source, copy);
        return copy;
    }

    private static void CheckCanceled(string step, float progress)
    {
        if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar(
                "Bake Sunset Patio Sweep", step, progress))
            throw new OperationCanceledException("Patio bake canceled.");
    }

    private static void TryCleanup(Action action, string label, List<string> errors)
    {
        try { action(); }
        catch (Exception exception) { errors.Add(label + " (" + exception.Message + ")"); }
    }
}
