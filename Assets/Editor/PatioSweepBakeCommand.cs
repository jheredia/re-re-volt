using System;
using System.Collections.Generic;
using System.IO;
using Core;
using Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Preserves the original Patio entry point for existing editor callers.</summary>
public static class PatioSweepBakeCommand
{
    public static string Bake() => Stage9CGeneratedTrackBakeCommand.Bake(TrackCatalog.PatioSweepId);
}

/// <summary>Bakes a Stage 9C generated course into an isolated, editable scene.</summary>
public static class Stage9CGeneratedTrackBakeCommand
{
    private const string MenuRoot = "Tools/ReReVolt/Tracks/";

    [MenuItem(MenuRoot + "Bake Sunset Patio Sweep Scene")]
    private static void BakePatioFromMenu() => BakeFromMenu(TrackCatalog.PatioSweepId);

    [MenuItem(MenuRoot + "Bake Market Aisle Dash Scene")]
    private static void BakeMarketFromMenu() => BakeFromMenu(TrackCatalog.MarketAislesId);

    [MenuItem(MenuRoot + "Bake Club Bridge Exhibition Scene")]
    private static void BakeBridgeFromMenu() => BakeFromMenu(TrackCatalog.BridgeExhibitionId);

    private static void BakeFromMenu(string id)
    {
        try
        {
            string path = Bake(id);
            Debug.Log("[TrackAuthoring] Baked editable scene at " + path +
                      ". The playable catalog and build scenes have not changed.");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[TrackAuthoring] Generated track bake canceled; created assets were removed.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[TrackAuthoring] Generated track bake failed: " + exception);
            EditorUtility.DisplayDialog("Track scene was not baked", exception.Message, "OK");
        }
    }

    public static string Bake(string id)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before baking a track.");
        TrackRuntimeBuilder builder = BuilderFor(id);
        if (!TrackCatalog.TryGet(id, out TrackDefinition definition) || definition.IsSceneAuthored ||
            !SceneTrackManifest.IsValidRevision(definition.ContentRevision))
            throw new InvalidOperationException("The generated track catalog entry is unavailable: " + id);
        string folder = SceneTrackManifest.SceneFolder + "/" + id;
        string scenePath = SceneTrackManifest.ScenePathFor(id);
        if (Directory.Exists(folder) || AssetDatabase.IsValidFolder(folder) || File.Exists(scenePath))
            throw new IOException("The track destination already exists: " + folder);

        Scene originalScene = SceneManager.GetActiveScene();
        if (!originalScene.IsValid() || !originalScene.isLoaded)
            throw new InvalidOperationException("An active Editor scene is required before baking.");
        UnityEngine.Object originalSelection = Selection.activeObject;
        Scene temporaryScene = default;
        ITrackRuntime runtime = null;
        bool parentExistedBefore = AssetDatabase.IsValidFolder(SceneTrackManifest.SceneFolder) ||
                                   Directory.Exists(SceneTrackManifest.SceneFolder);
        Exception failure = null;
        var cleanupErrors = new List<string>();

        try
        {
            CheckCanceled(definition.DisplayName, "Preparing isolated scene", 0f);
            if (!AssetDatabase.IsValidFolder(SceneTrackManifest.SceneFolder))
            {
                string parentGuid = AssetDatabase.CreateFolder("Assets/Scenes", "CustomTracks");
                if (string.IsNullOrEmpty(parentGuid))
                    throw new IOException("Could not create the custom tracks folder.");
            }
            string folderGuid = AssetDatabase.CreateFolder(SceneTrackManifest.SceneFolder, id);
            if (string.IsNullOrEmpty(folderGuid))
                throw new IOException("Could not create the track asset folder.");

            temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (!SceneManager.SetActiveScene(temporaryScene))
                throw new InvalidOperationException("Could not activate the temporary bake scene.");
            var raceObject = new GameObject("TemporaryTrackBakeRaceManager");
            RaceManager race = raceObject.AddComponent<RaceManager>();
            runtime = builder(race, null, null);
            if (runtime?.Root == null || runtime.Grid == null ||
                runtime.Grid.Length != Stage9CAuthoredTrackBuilder.GridCount ||
                runtime.PickupPositions == null ||
                runtime.PickupPositions.Count != Stage9CAuthoredTrackBuilder.PickupCount ||
                runtime.Checkpoints == null ||
                runtime.Checkpoints.Length != definition.OrderedCheckpointCount)
                throw new InvalidOperationException("The generated " + id +
                    " layout no longer matches its catalog contract.");

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
                    throw new InvalidOperationException(id + " checkpoint order changed at index " + index);
                SerializedProperty recovery = new SerializedObject(checkpoint).FindProperty("recoveryAnchor");
                Transform anchor = recovery?.objectReferenceValue as Transform;
                if (anchor == null || anchor.parent != rootObject.transform)
                    throw new InvalidOperationException(id + " checkpoint " + index + " has no owned recovery pose.");
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

            CheckCanceled(definition.DisplayName, "Persisting materials and meshes", .45f);
            PersistRuntimeAssets(authoring, folder);
            foreach (Renderer renderer in authoring.GetComponentsInChildren<Renderer>(true))
            {
                bool decoration = renderer.GetComponent<NearCameraPresentationOccludable>() is
                    { Kind: NearCameraOcclusionKind.Decoration };
                bool outline = decoration || renderer.gameObject.name.StartsWith("Barrier_", StringComparison.Ordinal);
                renderer.gameObject.AddComponent<TrackAuthoringVisual>().Configure(outline);
            }
            if (!TrackAuthoringEditorTools.TryValidateScene(temporaryScene, out string report))
                throw new InvalidOperationException("Baked " + id + " scene failed structural validation:\n" + report);
            CheckCanceled(definition.DisplayName, "Saving scene", .85f);
            if (!EditorSceneManager.SaveScene(temporaryScene, scenePath))
                throw new IOException("Unity could not save " + scenePath);
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
            if (AssetDatabase.IsValidFolder(folder) || Directory.Exists(folder))
                TryCleanup(() => DeleteCreatedFolder(folder), "new track assets", cleanupErrors);
            if (!parentExistedBefore && (AssetDatabase.IsValidFolder(SceneTrackManifest.SceneFolder) ||
                                         Directory.Exists(SceneTrackManifest.SceneFolder)))
                TryCleanup(() => DeleteCreatedFolder(SceneTrackManifest.SceneFolder),
                    "new custom tracks folder", cleanupErrors);
            if (cleanupErrors.Count != 0)
                throw new InvalidOperationException("Generated track bake failed; cleanup also failed for " +
                    string.Join(", ", cleanupErrors) + ".", failure);
            if (failure != null) throw failure;
        }
        return scenePath;
    }

    private static TrackRuntimeBuilder BuilderFor(string id)
    {
        switch (id)
        {
            case TrackCatalog.PatioSweepId:
                return Stage9CAuthoredTrackBuilder.BuildPatioSweep;
            case TrackCatalog.MarketAislesId:
                return Stage9CAuthoredTrackBuilder.BuildMarketAisles;
            case TrackCatalog.BridgeExhibitionId:
                return Stage9CAuthoredTrackBuilder.BuildBridgeExhibition;
            default:
                throw new ArgumentException("Only Stage 9C generated tracks can be baked.", nameof(id));
        }
    }

    private static void PersistRuntimeAssets(TrackAuthoringRoot root, string folder)
    {
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, "Materials")))
            throw new IOException("Could not create the track material folder.");
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
                    string path = $"{folder}/Materials/Material_{materialCopies.Count:00}.mat";
                    AssetDatabase.CreateAsset(copy, path);
                    if (AssetDatabase.LoadAssetAtPath<Material>(path) != copy)
                        throw new IOException("Could not persist track material: " + path);
                    materialCopies.Add(source, copy);
                }
                materials[index] = copy;
            }
            renderer.sharedMaterials = materials;
        }
        var meshCopies = new Dictionary<Mesh, Mesh>();
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            filter.sharedMesh = PersistMesh(filter.sharedMesh, meshCopies, folder);
        foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>(true))
            collider.sharedMesh = PersistMesh(collider.sharedMesh, meshCopies, folder);
        AssetDatabase.SaveAssets();
    }

    private static Mesh PersistMesh(Mesh source, Dictionary<Mesh, Mesh> copies, string folder)
    {
        if (source == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(source))) return source;
        if (copies.TryGetValue(source, out Mesh existing)) return existing;
        Mesh copy = UnityEngine.Object.Instantiate(source);
        copy.name = source.name;
        string path = $"{folder}/Mesh_{copies.Count:00}.asset";
        AssetDatabase.CreateAsset(copy, path);
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != copy)
            throw new IOException("Could not persist track mesh: " + path);
        copies.Add(source, copy);
        return copy;
    }

    private static void CheckCanceled(string displayName, string step, float progress)
    {
        if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar(
                "Bake " + displayName, step, progress))
            throw new OperationCanceledException(displayName + " bake canceled.");
    }

    private static void DeleteCreatedFolder(string folder)
    {
        if (!AssetDatabase.DeleteAsset(folder))
            throw new IOException("Asset deletion failed: " + folder);
    }

    private static void TryCleanup(Action action, string label, List<string> errors)
    {
        try { action(); }
        catch (Exception exception) { errors.Add(label + " (" + exception.Message + ")"); }
    }
}
