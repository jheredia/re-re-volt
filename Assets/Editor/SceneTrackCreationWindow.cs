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

/// <summary>Copies the beta scene into an independently editable, registered course.</summary>
public sealed class SceneTrackCreationWindow : EditorWindow
{
    private const string MenuPath = "Tools/ReReVolt/Tracks/Create Playable Scene Track";
    private const string TemplateMaterials = "Assets/Scenes/TrackTemplateMaterials/";

    private string id = "new-club-track";
    private string displayName = "NEW CLUB TRACK";
    private string description = "A NEW CLUB CIRCUIT";
    private string revision = "v1";

    [MenuItem(MenuPath)]
    private static void Open()
    {
        SceneTrackCreationWindow window = GetWindow<SceneTrackCreationWindow>(true, "Create Scene Track");
        window.minSize = new Vector2(420, 260);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Duplicate the playable beta template", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Use a permanent lowercase ID. Change the revision whenever the scene's playable content changes.", MessageType.Info);
        id = EditorGUILayout.TextField("Stable ID", id);
        displayName = EditorGUILayout.TextField("Display name", displayName);
        EditorGUILayout.LabelField("Flow description");
        description = EditorGUILayout.TextArea(description, GUILayout.MinHeight(44));
        revision = EditorGUILayout.TextField("Content revision", revision);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Create and register track", GUILayout.Height(30)))
        {
            try
            {
                string path = CreateTrack(id, displayName, description, revision);
                Debug.Log("[TrackAuthoring] Created and registered " + path);
                Close();
            }
            catch (Exception exception)
            {
                Debug.LogError("[TrackAuthoring] Could not create scene track: " + exception);
                EditorUtility.DisplayDialog("Scene track was not created", exception.Message, "OK");
            }
        }
    }

    private static string CreateTrack(string rawId, string rawName, string rawDescription, string rawRevision)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play mode before creating a track.");
        string trackId = rawId?.Trim() ?? string.Empty;
        string name = rawName?.Trim() ?? string.Empty;
        string flow = rawDescription?.Trim() ?? string.Empty;
        string contentRevision = rawRevision?.Trim() ?? string.Empty;
        if (!SceneTrackManifest.IsValidId(trackId))
            throw new ArgumentException("Stable ID must start with a lowercase letter and contain only lowercase letters, digits, and hyphens (up to 64 characters, no trailing hyphen).");
        if (name.Length == 0 || flow.Length == 0 || contentRevision.Length == 0)
            throw new ArgumentException("Display name, flow description, and content revision are required.");
        if (!SceneTrackManifest.IsValidRevision(contentRevision))
            throw new ArgumentException("Content revision must use 1 to 64 letters, digits, periods, underscores, or hyphens.");
        if (name.Contains("\n") || flow.Contains("\n") || contentRevision.Contains("\n"))
            throw new ArgumentException("Metadata fields must contain one line each.");

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TrackAuthoringEditorTools.TemplateScenePath) == null)
            throw new FileNotFoundException("The beta track template scene is missing.", TrackAuthoringEditorTools.TemplateScenePath);
        if (!File.Exists(SceneTrackManifest.AssetPath))
            throw new FileNotFoundException("The scene track manifest is missing.", SceneTrackManifest.AssetPath);
        string previousManifestJson = File.ReadAllText(SceneTrackManifest.AssetPath);
        SceneTrackManifest manifest = SceneTrackManifest.Parse(previousManifestJson);
        if (TrackCatalog.TryGet(trackId, out _) || manifest.Tracks.Any(track => track.Id == trackId))
            throw new InvalidOperationException($"Track ID '{trackId}' is already registered.");

        string folder = SceneTrackManifest.SceneFolder + "/" + trackId;
        string scenePath = SceneTrackManifest.ScenePathFor(trackId);
        if (AssetDatabase.IsValidFolder(folder) || File.Exists(scenePath))
            throw new IOException("A track folder or scene already exists at " + folder);

        string[] materialPaths = AssetDatabase.GetDependencies(TrackAuthoringEditorTools.TemplateScenePath, true)
            .Where(path => path.StartsWith(TemplateMaterials, StringComparison.Ordinal) && path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (materialPaths.Length == 0)
            throw new InvalidOperationException("The template has no referenced materials to duplicate.");
        foreach (string path in materialPaths)
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                throw new IOException("Could not load template material: " + path);

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            throw new OperationCanceledException("Scene creation was canceled.");

        string previousScenePath = SceneManager.GetActiveScene().path;
        EditorBuildSettingsScene[] previousBuildScenes = EditorBuildSettings.scenes;
        bool createdParentFolder = false;
        bool createdTrackFolder = false;
        bool manifestChanged = false;
        bool buildSettingsChanged = false;
        try
        {
            if (!AssetDatabase.IsValidFolder(SceneTrackManifest.SceneFolder))
            {
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets/Scenes", "CustomTracks")))
                    throw new IOException("Could not create the custom tracks folder.");
                createdParentFolder = true;
            }
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(SceneTrackManifest.SceneFolder, trackId)))
                throw new IOException("Could not create the new track folder.");
            createdTrackFolder = true;
            string materialFolder = folder + "/Materials";
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, "Materials")))
                throw new IOException("Could not create the new track material folder.");
            var replacementMaterials = new Dictionary<Material, Material>();
            foreach (string sourcePath in materialPaths)
            {
                string destinationPath = materialFolder + "/" + Path.GetFileName(sourcePath);
                if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    throw new IOException("Could not duplicate template material: " + sourcePath);
                Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                Material replacement = AssetDatabase.LoadAssetAtPath<Material>(destinationPath);
                if (replacement == null) throw new IOException("Could not load duplicated material: " + destinationPath);
                replacementMaterials.Add(source, replacement);
            }
            if (!AssetDatabase.CopyAsset(TrackAuthoringEditorTools.TemplateScenePath, scenePath))
                throw new IOException("Could not duplicate the template scene.");

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Length != 1 || !roots[0].TryGetComponent(out TrackAuthoringRoot authoring))
                throw new InvalidOperationException("The duplicated scene needs exactly one TrackAuthoringRoot as its only root.");
            Undo.RecordObject(authoring, "Set scene track identity");
            authoring.Configure(trackId, authoring.Laps, contentRevision);
            EditorUtility.SetDirty(authoring);

            foreach (Renderer renderer in authoring.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && replacementMaterials.TryGetValue(materials[i], out Material replacement))
                    { materials[i] = replacement; changed = true; }
                if (!changed) continue;
                Undo.RecordObject(renderer, "Assign track materials");
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }

            if (!TrackAuthoringEditorTools.TryValidateScene(scene, out string report))
                throw new InvalidOperationException("The duplicated scene is invalid:\n" + report);
            int checkpointCount = authoring.GetComponentsInChildren<RaceCheckpoint>(true).Length;
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Unity could not save the new track scene.");

            var registeredScenes = new List<EditorBuildSettingsScene>(previousBuildScenes)
            { new EditorBuildSettingsScene(scenePath, true) };
            buildSettingsChanged = true;
            EditorBuildSettings.scenes = registeredScenes.ToArray();

            var record = new SceneTrackRecord(trackId, name, flow,
                $"SCENE-AUTHORED / {checkpointCount} CHECKPOINTS", scenePath,
                contentRevision, authoring.Laps, checkpointCount);
            string updatedManifestJson = manifest.WithAdded(record).ToJson();
            manifestChanged = true;
            File.WriteAllText(SceneTrackManifest.AssetPath, updatedManifestJson);
            AssetDatabase.ImportAsset(SceneTrackManifest.AssetPath, ImportAssetOptions.ForceUpdate);
            TrackCatalog.ReloadSceneManifest(updatedManifestJson);
            Selection.activeGameObject = authoring.gameObject;
            EditorGUIUtility.PingObject(authoring.gameObject);
            return scenePath;
        }
        catch (Exception creationError)
        {
            var rollbackErrors = new List<string>();
            if (manifestChanged)
                TryRollback(() =>
                {
                    File.WriteAllText(SceneTrackManifest.AssetPath, previousManifestJson);
                    AssetDatabase.ImportAsset(SceneTrackManifest.AssetPath, ImportAssetOptions.ForceUpdate);
                    TrackCatalog.ReloadSceneManifest(previousManifestJson);
                }, "manifest", rollbackErrors);
            if (buildSettingsChanged)
                TryRollback(() => EditorBuildSettings.scenes = previousBuildScenes,
                    "build scene list", rollbackErrors);
            if (!string.IsNullOrEmpty(previousScenePath))
                TryRollback(() => EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single),
                    "previous scene", rollbackErrors);
            else
                TryRollback(() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single),
                    "previous scene", rollbackErrors);
            if (createdTrackFolder)
                TryRollback(() => { if (!AssetDatabase.DeleteAsset(folder)) throw new IOException("Asset deletion failed."); },
                    "new track assets", rollbackErrors);
            if (createdParentFolder)
                TryRollback(() => { if (!AssetDatabase.DeleteAsset(SceneTrackManifest.SceneFolder)) throw new IOException("Asset deletion failed."); },
                    "custom tracks folder", rollbackErrors);
            if (rollbackErrors.Count != 0)
                throw new InvalidOperationException("Creation failed: " + creationError.Message +
                    ". Cleanup also failed for " + string.Join(", ", rollbackErrors) + ".", creationError);
            throw;
        }
    }

    private static void TryRollback(Action action, string label, List<string> errors)
    {
        try { action(); }
        catch (Exception exception)
        {
            errors.Add(label + " (" + exception.Message + ")");
        }
    }
}
