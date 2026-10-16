using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

namespace Presentation
{
    /// <summary>The complete runtime product owned by one authored track builder.</summary>
    public interface ITrackRuntime : IDisposable
    {
        GameObject Root { get; }
        Transform[] Grid { get; }
        RaceCheckpoint[] Checkpoints { get; }
        LayerMask DrivableMask { get; }
        IReadOnlyList<Vector3> PickupPositions { get; }
    }

    public delegate ITrackRuntime TrackRuntimeBuilder(RaceManager race, Transform parent,
        VisualStyleController visualStyle);

    /// <summary>Stable metadata and construction seam for one fixed, authored circuit.</summary>
    public sealed class TrackDefinition
    {
        public TrackDefinition(string id, string displayName, string flowDescription, string previewMetadata,
            int lapCount, int orderedCheckpointCount, TrackRuntimeBuilder builder)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A track needs a stable ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A track needs a name.", nameof(displayName));
            if (lapCount < 1) throw new ArgumentOutOfRangeException(nameof(lapCount));
            if (orderedCheckpointCount < 1) throw new ArgumentOutOfRangeException(nameof(orderedCheckpointCount));
            Id = id.Trim();
            DisplayName = displayName.Trim();
            FlowDescription = string.IsNullOrWhiteSpace(flowDescription) ? "FLOW PREVIEW PENDING" : flowDescription.Trim();
            PreviewMetadata = string.IsNullOrWhiteSpace(previewMetadata) ? "NO PREVIEW" : previewMetadata.Trim();
            LapCount = lapCount;
            OrderedCheckpointCount = orderedCheckpointCount;
            Builder = builder;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string FlowDescription { get; }
        public string PreviewMetadata { get; }
        public int LapCount { get; }
        public int OrderedCheckpointCount { get; }
        public bool IsAvailable => Builder != null;
        internal TrackRuntimeBuilder Builder { get; }
    }

    /// <summary>
    /// Authored track roster. Stage 9C intentionally starts with one genuine entry; later content builders add
    /// definitions here rather than changing demo-flow or session authority.
    /// </summary>
    public static class TrackCatalog
    {
        public const string WorkshopCircuitId = "rc-club-workshop";

        private static readonly TrackDefinition[] entries =
        {
            new(WorkshopCircuitId, "RC CLUB WORKSHOP",
                "BALANCED FOUR-CORNER CLUB CIRCUIT",
                "CUTTING-MAT ROAD / WORKSHOP SCALE / 8 CHECKPOINTS",
                3, PrototypeCircuitBuilder.OrderedCheckpointCount,
                (race, parent, visualStyle) => PrototypeCircuitBuilder.Build(race, parent, visualStyle))
        };

        public static IReadOnlyList<TrackDefinition> Entries => entries;
        public static TrackDefinition Default => entries[0];

        public static bool TryGet(string id, out TrackDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!string.Equals(entries[i].Id, id, StringComparison.Ordinal)) continue;
                definition = entries[i];
                return true;
            }
            return false;
        }

        public static bool IsAvailable(string id) => TryGet(id, out TrackDefinition definition) && definition.IsAvailable;
    }

    /// <summary>Explicit local-session track choice; suitable for later serialization by Stage 10 authority.</summary>
    public sealed class TrackSession
    {
        private readonly TrackDefinition[] entries;
        private readonly Dictionary<string, TrackDefinition> entriesById;
        private int selectedIndex;

        public TrackSession(IReadOnlyList<TrackDefinition> catalog, string preferredTrackId = null)
        {
            if (catalog == null || catalog.Count == 0) throw new ArgumentException("A track catalog cannot be empty.", nameof(catalog));
            entries = new TrackDefinition[catalog.Count];
            entriesById = new Dictionary<string, TrackDefinition>(catalog.Count, StringComparer.Ordinal);
            int firstAvailable = -1;
            for (int i = 0; i < catalog.Count; i++)
            {
                TrackDefinition definition = catalog[i] ?? throw new ArgumentException("Track entries cannot be null.", nameof(catalog));
                if (!entriesById.TryAdd(definition.Id, definition))
                    throw new ArgumentException($"Duplicate track ID '{definition.Id}'.", nameof(catalog));
                entries[i] = definition;
                if (firstAvailable < 0 && definition.IsAvailable) firstAvailable = i;
            }
            if (firstAvailable < 0) throw new ArgumentException("A track catalog needs at least one available entry.", nameof(catalog));

            selectedIndex = firstAvailable;
            if (!string.IsNullOrWhiteSpace(preferredTrackId))
                for (int i = 0; i < entries.Length; i++)
                    if (entries[i].IsAvailable && string.Equals(entries[i].Id, preferredTrackId, StringComparison.Ordinal))
                        selectedIndex = i;
        }

        public IReadOnlyList<TrackDefinition> Entries => entries;
        public TrackDefinition SelectedTrack => entries[selectedIndex];
        public string SelectedTrackId => SelectedTrack.Id;
        public bool CanStart => SelectedTrack.IsAvailable;

        public bool TrySelect(string id)
        {
            if (!entriesById.TryGetValue(id ?? string.Empty, out TrackDefinition definition)) return false;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!ReferenceEquals(entries[i], definition)) continue;
                selectedIndex = i;
                return true;
            }
            return false;
        }

        public bool TryCycle(int direction)
        {
            if (direction == 0) return false;
            selectedIndex = (selectedIndex + (direction < 0 ? -1 : 1) + entries.Length) % entries.Length;
            return true;
        }

        public ITrackRuntime BuildSelected(RaceManager race, Transform parent = null,
            VisualStyleController visualStyle = null)
        {
            if (!CanStart) throw new InvalidOperationException($"Track '{SelectedTrackId}' is not available.");
            ITrackRuntime runtime = SelectedTrack.Builder(race, parent, visualStyle);
            if (runtime == null) throw new InvalidOperationException($"Track '{SelectedTrackId}' returned no runtime.");
            return runtime;
        }
    }
}
