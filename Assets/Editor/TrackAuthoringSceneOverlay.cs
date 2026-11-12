using System.Collections.Generic;
using Core;
using Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only Scene View guidance while a track authoring root is selected.</summary>
[CustomEditor(typeof(TrackAuthoringRoot))]
public sealed class TrackAuthoringSceneOverlay : Editor
{
    private static readonly Color CheckpointColor = new(.94f, .38f, .82f);
    private static readonly Color GridColor = new(.2f, .85f, 1f);
    private static readonly Color RecoveryColor = new(.35f, 1f, .48f);
    private static readonly Color PickupColor = new(1f, .84f, .25f);
    private static readonly Color IssueColor = new(1f, .35f, .3f);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }

    private void OnSceneGUI()
    {
        var root = (TrackAuthoringRoot)target;
        RaceCheckpoint[] checkpoints = root.GetComponentsInChildren<RaceCheckpoint>(true);
        TrackAuthoringAnchor[] anchors = root.GetComponentsInChildren<TrackAuthoringAnchor>(true);

        var checkpointIndices = new List<int>(checkpoints.Length);
        var gridIndices = new List<int>();
        var recoveryIndices = new List<int>();
        var pickupIndices = new List<int>();
        foreach (RaceCheckpoint checkpoint in checkpoints)
            checkpointIndices.Add(checkpoint.CheckpointIndex);
        foreach (TrackAuthoringAnchor anchor in anchors)
        {
            switch (anchor.Kind)
            {
                case TrackAnchorKind.Grid: gridIndices.Add(anchor.Index); break;
                case TrackAnchorKind.Recovery: recoveryIndices.Add(anchor.Index); break;
                case TrackAnchorKind.Pickup: pickupIndices.Add(anchor.Index); break;
            }
        }

        var checkpointAudit = new IndexAudit(checkpointIndices, checkpoints.Length);
        var gridAudit = new IndexAudit(gridIndices, 4);
        var recoveryAudit = new IndexAudit(recoveryIndices, checkpoints.Length);
        var pickupAudit = new IndexAudit(pickupIndices, 4);

        Color previousColor = Handles.color;
        DrawCheckpointRoute(checkpoints, checkpointAudit);
        foreach (RaceCheckpoint checkpoint in checkpoints)
        {
            bool issue = checkpointAudit.HasIssue(checkpoint.CheckpointIndex);
            Color color = issue ? IssueColor : CheckpointColor;
            Vector3 position = checkpoint.transform.position;
            Handles.color = color;
            Handles.DrawWireDisc(position, Vector3.up, .75f);
            DrawArrow(position, checkpoint.transform.forward, 1.6f, color);
            DrawLabel(position + Vector3.up * 1.05f,
                $"CP {checkpoint.CheckpointIndex:00}{IssueSuffix(checkpointAudit, checkpoint.CheckpointIndex)}", issue);
        }

        foreach (TrackAuthoringAnchor anchor in anchors)
        {
            IndexAudit audit;
            Color kindColor;
            string prefix;
            switch (anchor.Kind)
            {
                case TrackAnchorKind.Grid:
                    audit = gridAudit; kindColor = GridColor; prefix = "GRID"; break;
                case TrackAnchorKind.Recovery:
                    audit = recoveryAudit; kindColor = RecoveryColor; prefix = "REC"; break;
                case TrackAnchorKind.Pickup:
                    audit = pickupAudit; kindColor = PickupColor; prefix = "PICKUP"; break;
                default: continue;
            }

            bool issue = audit.HasIssue(anchor.Index);
            Color color = issue ? IssueColor : kindColor;
            Vector3 position = anchor.transform.position;
            Handles.color = color;
            Handles.DrawWireDisc(position, Vector3.up,
                anchor.Kind == TrackAnchorKind.Pickup ? .6f : .45f);
            if (anchor.Kind != TrackAnchorKind.Pickup)
                DrawArrow(position, anchor.transform.forward, 1.4f, color);
            DrawLabel(position + Vector3.up * .65f,
                $"{prefix} {anchor.Index:00}{IssueSuffix(audit, anchor.Index)}", issue);
        }
        Handles.color = previousColor;

        DrawLegend(checkpointAudit, gridAudit, recoveryAudit, pickupAudit);
    }

    private static void DrawCheckpointRoute(RaceCheckpoint[] checkpoints, IndexAudit audit)
    {
        if (checkpoints.Length < 2) return;
        var byIndex = new Dictionary<int, RaceCheckpoint>();
        foreach (RaceCheckpoint checkpoint in checkpoints)
            if (!audit.HasIssue(checkpoint.CheckpointIndex))
                byIndex.Add(checkpoint.CheckpointIndex, checkpoint);

        for (int index = 0; index < checkpoints.Length; index++)
        {
            if (!byIndex.TryGetValue(index, out RaceCheckpoint from) ||
                !byIndex.TryGetValue((index + 1) % checkpoints.Length, out RaceCheckpoint to))
                continue;
            Vector3 start = from.transform.position + Vector3.up * .35f;
            Vector3 end = to.transform.position + Vector3.up * .35f;
            Vector3 direction = end - start;
            if (direction.sqrMagnitude < .01f) continue;
            Handles.color = new Color(CheckpointColor.r, CheckpointColor.g, CheckpointColor.b, .65f);
            Handles.DrawAAPolyLine(2.5f, start, end);
            DrawArrow((start + end) * .5f - direction.normalized * .4f,
                direction, .8f, CheckpointColor);
        }
    }

    private static void DrawArrow(Vector3 origin, Vector3 direction, float length, Color color)
    {
        if (direction.sqrMagnitude < .0001f) return;
        Vector3 forward = direction.normalized;
        Vector3 side = Vector3.Cross(forward, Vector3.up).normalized;
        if (side.sqrMagnitude < .0001f) side = Vector3.right;
        Vector3 tip = origin + forward * length;
        Handles.color = color;
        Handles.DrawAAPolyLine(3f, origin, tip);
        Handles.DrawAAPolyLine(3f, tip, tip - forward * .35f + side * .22f);
        Handles.DrawAAPolyLine(3f, tip, tip - forward * .35f - side * .22f);
    }

    private static void DrawLabel(Vector3 position, string text, bool issue)
    {
        var style = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = issue ? IssueColor :
            (EditorGUIUtility.isProSkin ? Color.white : Color.black);
        Handles.Label(position, text, style);
    }

    private static string IssueSuffix(IndexAudit audit, int index)
    {
        string suffix = audit.Duplicates.Contains(index) ? "  DUPLICATE" : string.Empty;
        if (audit.OutOfRange.Contains(index)) suffix += "  OUT OF RANGE";
        return suffix;
    }

    private static void DrawLegend(IndexAudit checkpoints, IndexAudit grid,
        IndexAudit recovery, IndexAudit pickups)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(12f, 46f, 440f, 240f), GUIContent.none, EditorStyles.helpBox);
        GUILayout.Label("TRACK AUTHORING  ·  selected root", EditorStyles.boldLabel);
        GUILayout.Label("Arrows show forward pose; lines show checkpoint order. Red marks bad indices.",
            EditorStyles.wordWrappedMiniLabel);
        DrawStatus("CP", CheckpointColor, checkpoints);
        DrawStatus("GRID", GridColor, grid);
        DrawStatus("REC", RecoveryColor, recovery);
        DrawStatus("PICKUP", PickupColor, pickups);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private static void DrawStatus(string name, Color color, IndexAudit audit)
    {
        var style = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
        string tint = ColorUtility.ToHtmlStringRGB(color);
        string status = $"<color=#{tint}>■ {name}</color>  {audit.Observed}/{audit.Expected}";
        if (audit.Missing.Count > 0) status += "  missing: " + FormatIndices(audit.Missing);
        if (audit.Duplicates.Count > 0) status += "  duplicate: " + FormatIndices(audit.Duplicates);
        if (audit.OutOfRange.Count > 0) status += "  out of range: " + FormatIndices(audit.OutOfRange);
        GUILayout.Label(status, style);
    }

    private static string FormatIndices(SortedSet<int> indices)
    {
        var shown = new List<string>(8);
        foreach (int index in indices)
        {
            if (shown.Count == 8) break;
            shown.Add(index.ToString());
        }
        string result = string.Join(", ", shown);
        return indices.Count > shown.Count ? result + $" +{indices.Count - shown.Count}" : result;
    }

    private sealed class IndexAudit
    {
        public readonly int Expected;
        public readonly int Observed;
        public readonly SortedSet<int> Missing = new();
        public readonly SortedSet<int> Duplicates = new();
        public readonly SortedSet<int> OutOfRange = new();

        public IndexAudit(List<int> indices, int expected)
        {
            Expected = expected;
            Observed = indices.Count;
            var seen = new HashSet<int>();
            foreach (int index in indices)
            {
                if (!seen.Add(index)) Duplicates.Add(index);
                if (index < 0 || index >= expected) OutOfRange.Add(index);
            }
            for (int index = 0; index < expected; index++)
                if (!seen.Contains(index)) Missing.Add(index);
        }

        public bool HasIssue(int index) => Duplicates.Contains(index) || OutOfRange.Contains(index);
    }
}
