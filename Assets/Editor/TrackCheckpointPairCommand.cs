using System;
using Core;
using Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Adds one checkpoint and its matching recovery pose to an editable track scene.</summary>
public static class TrackCheckpointPairCommand
{
    private const string MenuPath = "Tools/ReReVolt/Tracks/Add Checkpoint and Recovery Pair";
    private const string UndoName = "Add track checkpoint and recovery";
    private static readonly Vector3 TriggerSize = new(6.2f, 2.5f, .7f);

    [MenuItem(MenuPath)]
    private static void AddFromMenu()
    {
        try
        {
            RaceCheckpoint checkpoint = AddToOpenScene();
            Selection.activeGameObject = checkpoint.gameObject;
            EditorGUIUtility.PingObject(checkpoint.gameObject);
        }
        catch (Exception exception)
        {
            Debug.LogError("[TrackAuthoring] Could not add checkpoint pair: " + exception);
            EditorUtility.DisplayDialog("Checkpoint pair was not added", exception.Message, "OK");
        }
    }

    public static RaceCheckpoint AddToOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before editing the track.");
        Scene scene = SceneManager.GetActiveScene();
        if (SceneManager.sceneCount != 1 || !scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrEmpty(scene.path))
            throw new InvalidOperationException("Open a saved editable track scene alone first.");
        GameObject[] roots = scene.GetRootGameObjects();
        if (roots.Length != 1 || !roots[0].TryGetComponent(out TrackAuthoringRoot root) ||
            !string.Equals(scene.path, SceneTrackManifest.ScenePathFor(root.StableId), StringComparison.Ordinal))
            throw new InvalidOperationException("Open a custom track scene with one matching TrackAuthoringRoot.");

        RaceCheckpoint[] checkpoints = root.GetComponentsInChildren<RaceCheckpoint>(true);
        var seen = new bool[checkpoints.Length];
        foreach (RaceCheckpoint checkpoint in checkpoints)
        {
            int index = checkpoint.CheckpointIndex;
            if (index < 0 || index >= seen.Length || seen[index])
                throw new InvalidOperationException(
                    "Fix duplicate or missing checkpoint indices before appending another pair.");
            seen[index] = true;
        }
        var recoverySeen = new bool[checkpoints.Length];
        foreach (TrackAuthoringAnchor anchor in root.GetComponentsInChildren<TrackAuthoringAnchor>(true))
        {
            if (anchor.Kind != TrackAnchorKind.Recovery) continue;
            int index = anchor.Index;
            if (index < 0 || index >= recoverySeen.Length || recoverySeen[index])
                throw new InvalidOperationException(
                    "Fix duplicate, missing, or extra Recovery anchors before appending another pair.");
            recoverySeen[index] = true;
        }
        for (int index = 0; index < recoverySeen.Length; index++)
            if (!recoverySeen[index])
                throw new InvalidOperationException("Add the missing Recovery anchor for checkpoint " + index + " first.");

        Vector3 roadPoint = ChooseRoadPoint(root.transform);
        Quaternion heading = ChooseHeading(root.transform, checkpoints);
        int nextIndex = checkpoints.Length;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        try
        {
            var recoveryObject = new GameObject($"Recovery_{nextIndex:00}");
            Undo.RegisterCreatedObjectUndo(recoveryObject, UndoName);
            Undo.SetTransformParent(recoveryObject.transform, root.transform, UndoName);
            Undo.RecordObject(recoveryObject.transform, UndoName);
            recoveryObject.transform.SetPositionAndRotation(roadPoint + Vector3.up * .25f, heading);
            TrackAuthoringAnchor recovery = Undo.AddComponent<TrackAuthoringAnchor>(recoveryObject);
            Undo.RecordObject(recovery, UndoName);
            recovery.Configure(TrackAnchorKind.Recovery, nextIndex);

            var checkpointObject = new GameObject($"Checkpoint_{nextIndex:00}");
            Undo.RegisterCreatedObjectUndo(checkpointObject, UndoName);
            Undo.SetTransformParent(checkpointObject.transform, root.transform, UndoName);
            Undo.RecordObject(checkpointObject.transform, UndoName);
            checkpointObject.transform.SetPositionAndRotation(roadPoint + Vector3.up * 1.15f, heading);
            BoxCollider trigger = Undo.AddComponent<BoxCollider>(checkpointObject);
            Undo.RecordObject(trigger, UndoName);
            trigger.isTrigger = true;
            trigger.enabled = false;
            trigger.size = TriggerSize;
            RaceCheckpoint checkpoint = Undo.AddComponent<RaceCheckpoint>(checkpointObject);
            Undo.RecordObject(checkpoint, UndoName);
            checkpoint.Configure(null, nextIndex, recovery.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            return checkpoint;
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    private static Vector3 ChooseRoadPoint(Transform root)
    {
        Transform selected = Selection.activeTransform;
        if (selected != null && selected != root && selected.IsChildOf(root))
            return selected.position;
        SceneView view = SceneView.lastActiveSceneView;
        return view != null ? view.pivot : root.position;
    }

    private static Quaternion ChooseHeading(Transform root, RaceCheckpoint[] checkpoints)
    {
        Transform selected = Selection.activeTransform;
        Vector3 forward = selected != null && selected != root && selected.IsChildOf(root)
            ? selected.forward : checkpoints.Length > 0
                ? FindLast(checkpoints).transform.forward : Vector3.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > .001f
            ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
    }

    private static RaceCheckpoint FindLast(RaceCheckpoint[] checkpoints)
    {
        RaceCheckpoint last = checkpoints[0];
        foreach (RaceCheckpoint checkpoint in checkpoints)
            if (checkpoint.CheckpointIndex > last.CheckpointIndex) last = checkpoint;
        return last;
    }
}
