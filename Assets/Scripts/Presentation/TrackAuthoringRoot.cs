using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Scene-authored data only. A later scene loader binds these references to the existing
    /// ITrackRuntime owners; this component never starts a race or owns multiplayer authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrackAuthoringRoot : MonoBehaviour
    {
        [SerializeField] private string stableId = "custom-beta-track";
        [SerializeField] private string contentRevision = "beta-template-v1";
        [Min(1)] [SerializeField] private int laps = 3;
        [SerializeField] private LayerMask drivableMask = 1 << PrototypeCircuitBuilder.DrivableLayer;

        public string StableId => stableId;
        public string ContentRevision => contentRevision;
        public int Laps => laps;
        public LayerMask DrivableMask => drivableMask;

        public void Configure(string id, int lapCount)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A track needs a stable ID.", nameof(id));
            if (lapCount < 1) throw new ArgumentOutOfRangeException(nameof(lapCount));
            stableId = id.Trim();
            laps = lapCount;
        }

        /// <summary>Returns actionable authoring errors without changing the scene.</summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(stableId)) errors.Add("Set a non-empty stable track ID.");
            if (string.IsNullOrWhiteSpace(contentRevision)) errors.Add("Set a non-empty content revision.");
            if (laps < 1) errors.Add("Lap count must be at least one.");
            if (drivableMask.value == 0) errors.Add("Assign a non-empty drivable layer mask.");

            TrackAuthoringAnchor[] anchors = GetComponentsInChildren<TrackAuthoringAnchor>(true);
            ValidateAnchors(anchors, TrackAnchorKind.Grid, 4, errors);
            ValidateAnchors(anchors, TrackAnchorKind.Pickup, 4, errors);

            RaceCheckpoint[] checkpoints = GetComponentsInChildren<RaceCheckpoint>(true);
            if (checkpoints.Length < 4) errors.Add("Place at least four ordered RaceCheckpoint triggers.");
            var checkpointIndices = new HashSet<int>();
            foreach (RaceCheckpoint checkpoint in checkpoints)
            {
                if (!checkpointIndices.Add(checkpoint.CheckpointIndex))
                    errors.Add($"Duplicate checkpoint index {checkpoint.CheckpointIndex}.");
                Collider trigger = checkpoint.GetComponent<Collider>();
                if (trigger == null || !trigger.isTrigger)
                    errors.Add($"Checkpoint '{checkpoint.name}' needs a trigger Collider.");
                else if (trigger.enabled)
                    errors.Add($"Checkpoint '{checkpoint.name}' trigger must be disabled in the authored scene; the host loader enables it after binding race authority.");
            }
            for (int index = 0; index < checkpoints.Length; index++)
                if (!checkpointIndices.Contains(index)) errors.Add($"Missing checkpoint index {index}.");
            ValidateAnchors(anchors, TrackAnchorKind.Recovery, checkpoints.Length, errors);

            int drivableCount = 0;
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                if ((drivableMask.value & (1 << collider.gameObject.layer)) == 0) continue;
                drivableCount++;
                if (collider.isTrigger)
                    errors.Add($"Drivable collider '{collider.name}' must not be a trigger.");
                TrackSurfaceMarker marker = collider.GetComponentInParent<TrackSurfaceMarker>(true);
                if (marker == null || marker.Kind == TrackSurfaceKind.Unclassified)
                    errors.Add($"Drivable collider '{collider.name}' needs a classified TrackSurfaceMarker.");
            }
            if (drivableCount == 0) errors.Add("Place at least one marked drivable collider on the drivable layer.");
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer.GetComponent<TrackAuthoringVisual>() == null)
                    errors.Add($"Renderer '{renderer.name}' needs a TrackAuthoringVisual marker.");
            if (GetComponentInChildren<Camera>(true) != null || GetComponentInChildren<AudioListener>(true) != null)
                errors.Add("Track scene must not contain a Camera or AudioListener.");
            if (GetComponentInChildren<RaceManager>(true) != null || GetComponentInChildren<Stage3Bootstrap>(true) != null)
                errors.Add("Track scene must not contain a RaceManager or Stage3Bootstrap.");
            return errors;
        }

        private static void ValidateAnchors(TrackAuthoringAnchor[] anchors, TrackAnchorKind kind,
            int expectedCount, List<string> errors)
        {
            var seen = new HashSet<int>();
            int count = 0;
            foreach (TrackAuthoringAnchor anchor in anchors)
            {
                if (anchor.Kind != kind) continue;
                count++;
                if (!seen.Add(anchor.Index)) errors.Add($"Duplicate {kind} anchor index {anchor.Index}.");
                if (anchor.Index >= expectedCount)
                    errors.Add($"{kind} anchor index {anchor.Index} exceeds {expectedCount - 1}.");
            }
            if (count != expectedCount)
                errors.Add($"Expected {expectedCount} {kind} anchors; found {count}.");
            for (int index = 0; index < expectedCount; index++)
                if (!seen.Contains(index)) errors.Add($"Missing {kind} anchor index {index}.");
        }
    }
}
