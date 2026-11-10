using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Core;
using Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates and validates a scene designers can edit without changing the playable catalog.</summary>
public static class TrackAuthoringEditorTools
{
    public const string TemplateScenePath = "Assets/Scenes/Track_BetaTemplate.unity";
    private const string MaterialFolder = "Assets/Scenes/TrackTemplateMaterials";
    private const string MenuRoot = "Tools/ReReVolt/Tracks/";
    private const float RoadWidth = 7f;

    private static readonly Vector3[] Route =
    {
        new(-18f, 0f, -18f), new(-5f, 0f, -18f), new(12f, 0f, -18f),
        new(18f, 0f, -5f), new(18f, 0f, 12f), new(5f, 0f, 18f),
        new(-12f, 0f, 18f), new(-18f, 0f, 5f)
    };

    [MenuItem(MenuRoot + "Create Editable Beta Track Template")]
    public static void CreateTemplateAsset()
    {
        if (File.Exists(TemplateScenePath))
        {
            Debug.LogWarning("[TrackAuthoring] Template already exists: " + TemplateScenePath);
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Material road = CreateMaterial("Road", new Color(.25f, .35f, .43f));
        Material ground = CreateMaterial("Ground", new Color(.43f, .38f, .29f));
        Material accent = CreateMaterial("Checkpoint", new Color(.95f, .73f, .21f));
        Material pickup = CreateMaterial("Pickup", new Color(.2f, .82f, .75f));

        var root = new GameObject("TrackAuthoringRoot");
        TrackAuthoringRoot authoring = root.AddComponent<TrackAuthoringRoot>();
        authoring.Configure("custom-beta-track", 3);
        CreateBox(root.transform, "Ground", new Vector3(0, -.7f, 0),
            new Vector3(50, 1f, 50), ground, 0, TrackSurfaceKind.WorkbenchWood);
        for (int i = 0; i < Route.Length; i++)
        {
            Vector3 from = Route[i];
            Vector3 to = Route[(i + 1) % Route.Length];
            Vector3 delta = to - from;
            GameObject roadPiece = CreateBox(root.transform, $"Road_{i:00}",
                (from + to) * .5f, new Vector3(RoadWidth, .2f, delta.magnitude + RoadWidth * .4f),
                road, PrototypeCircuitBuilder.DrivableLayer, TrackSurfaceKind.CuttingMat);
            roadPiece.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

            Quaternion heading = roadPiece.transform.rotation;
            Transform recovery = CreateAnchor(root.transform, TrackAnchorKind.Recovery, i,
                from + Vector3.up * .25f, heading);
            var trigger = new GameObject($"Checkpoint_{i:00}");
            trigger.transform.SetParent(root.transform, false);
            trigger.transform.SetPositionAndRotation(from + Vector3.up * 1.15f, heading);
            BoxCollider box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.enabled = false;
            box.size = new Vector3(RoadWidth - .8f, 2.5f, .7f);
            trigger.AddComponent<RaceCheckpoint>().Configure(null, i, recovery);
            CreateBox(root.transform, $"CheckpointStripe_{i:00}", from + Vector3.up * .12f,
                new Vector3(RoadWidth - .8f, .04f, .25f), accent, 0,
                TrackSurfaceKind.Unclassified, false).transform.rotation = heading;
        }

        for (int i = 0; i < 4; i++)
        {
            float across = i % 2 == 0 ? -1.35f : 1.35f;
            float back = i / 2 == 0 ? 2.7f : 5.3f;
            CreateAnchor(root.transform, TrackAnchorKind.Grid, i,
                Route[0] + Vector3.right * back + Vector3.forward * across + Vector3.up * .76f,
                Quaternion.LookRotation(Vector3.right));
            Vector3 pickupPoint = Route[(i * 2 + 1) % Route.Length] + Vector3.up * .5f;
            CreateAnchor(root.transform, TrackAnchorKind.Pickup, i, pickupPoint, Quaternion.identity);
            CreateBox(root.transform, $"PickupPlaceholder_{i + 1}", pickupPoint,
                new Vector3(.45f, .45f, .45f), pickup, 0, TrackSurfaceKind.Unclassified, false);
        }

        if (!TryValidateScene(scene, out string report))
            throw new InvalidOperationException("Generated track template failed validation:\n" + report);
        if (!EditorSceneManager.SaveScene(scene, TemplateScenePath))
            throw new IOException("Unity could not save " + TemplateScenePath);
        Selection.activeGameObject = root;
        Debug.Log("[TrackAuthoring] Editable template saved at " + TemplateScenePath +
                  ". Duplicate and rename this scene before designing the beta track.");
    }

    [MenuItem(MenuRoot + "Validate Open Track Scene")]
    public static void ValidateOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (TryValidateScene(scene, out string report))
            Debug.Log("[TrackAuthoring] " + scene.path + " passes structural validation.");
        else
            Debug.LogError("[TrackAuthoring] " + scene.path + " needs changes:\n" + report);
    }

    /// <summary>Batch validation reopens the serialized scene rather than checking only in-memory construction.</summary>
    public static void ValidateSavedTemplate()
    {
        Scene scene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Single);
        if (!TryValidateScene(scene, out string report))
            throw new InvalidOperationException("Saved track template is invalid:\n" + report);
        Debug.Log("[TrackAuthoring] Saved template passes structural validation: " + TemplateScenePath);
    }

    public static bool TryValidateScene(Scene scene, out string report)
    {
        var issues = new StringBuilder();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            report = "Open a loaded track scene before validation.";
            return false;
        }
        TrackAuthoringRoot root = null;
        GameObject[] sceneRoots = scene.GetRootGameObjects();
        foreach (GameObject sceneRoot in sceneRoots)
        {
            foreach (TrackAuthoringRoot candidate in sceneRoot.GetComponentsInChildren<TrackAuthoringRoot>(true))
            {
                if (root == null) root = candidate;
                else issues.AppendLine("Keep exactly one TrackAuthoringRoot in the scene.");
            }
            if (sceneRoot.GetComponentInChildren<Camera>(true) != null ||
                sceneRoot.GetComponentInChildren<AudioListener>(true) != null)
                issues.AppendLine("Remove cameras and AudioListeners from the track scene.");
            if (sceneRoot.GetComponentInChildren<RaceManager>(true) != null ||
                sceneRoot.GetComponentInChildren<Stage3Bootstrap>(true) != null)
                issues.AppendLine("Remove RaceManager and Stage3Bootstrap from the track scene.");
        }
        if (root == null) issues.AppendLine("Add one TrackAuthoringRoot to the track scene.");
        else
        {
            foreach (string issue in root.Validate()) issues.AppendLine(issue);
            foreach (GameObject sceneRoot in sceneRoots)
                if (sceneRoot != root.gameObject)
                    issues.AppendLine($"Move scene root '{sceneRoot.name}' under TrackAuthoringRoot so it unloads with the track.");

            var recoveryByIndex = new Dictionary<int, Transform>();
            foreach (TrackAuthoringAnchor anchor in root.GetComponentsInChildren<TrackAuthoringAnchor>(true))
                if (anchor.Kind == TrackAnchorKind.Recovery && !recoveryByIndex.ContainsKey(anchor.Index))
                    recoveryByIndex.Add(anchor.Index, anchor.transform);
            foreach (RaceCheckpoint checkpoint in root.GetComponentsInChildren<RaceCheckpoint>(true))
            {
                SerializedProperty recovery = new SerializedObject(checkpoint).FindProperty("recoveryAnchor");
                if (!recoveryByIndex.TryGetValue(checkpoint.CheckpointIndex, out Transform expected) ||
                    recovery == null || recovery.objectReferenceValue != expected)
                    issues.AppendLine($"Checkpoint {checkpoint.CheckpointIndex} must reference its matching Recovery anchor.");
            }
        }
        report = issues.ToString().TrimEnd();
        return report.Length == 0;
    }

    private static Material CreateMaterial(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Scenes", "TrackTemplateMaterials");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable for the track template.");
        var material = new Material(shader) { name = "Template " + name };
        material.SetColor("_BaseColor", color);
        string path = MaterialFolder + "/" + name + ".mat";
        if (File.Exists(path))
        {
            UnityEngine.Object.DestroyImmediate(material);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 scale,
        Material material, int layer, TrackSurfaceKind surface, bool colliderEnabled = true)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = position;
        box.transform.localScale = scale;
        box.layer = layer;
        box.GetComponent<Renderer>().sharedMaterial = material;
        box.AddComponent<TrackAuthoringVisual>();
        box.GetComponent<Collider>().enabled = colliderEnabled;
        if (surface != TrackSurfaceKind.Unclassified)
            box.AddComponent<TrackSurfaceMarker>().Configure(surface);
        return box;
    }

    private static Transform CreateAnchor(Transform parent, TrackAnchorKind kind, int index,
        Vector3 position, Quaternion rotation)
    {
        GameObject anchor = new($"{kind}_{index:00}");
        anchor.transform.SetParent(parent, false);
        anchor.transform.SetPositionAndRotation(position, rotation);
        anchor.AddComponent<TrackAuthoringAnchor>().Configure(kind, index);
        return anchor.transform;
    }
}
