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
            int lapCount, int orderedCheckpointCount, TrackRuntimeBuilder builder, string scenePath = null,
            string contentRevision = "code-v1")
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
            ScenePath = scenePath;
            ContentRevision = contentRevision;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string FlowDescription { get; }
        public string PreviewMetadata { get; }
        public int LapCount { get; }
        public int OrderedCheckpointCount { get; }
        public bool IsAvailable => Builder != null || !string.IsNullOrWhiteSpace(ScenePath);
        public bool IsSceneAuthored => !string.IsNullOrWhiteSpace(ScenePath);
        public string ScenePath { get; }
        public string ContentRevision { get; }
        internal TrackRuntimeBuilder Builder { get; }
    }

    /// <summary>
    /// Generated courses and the beta template keep their original definitions. Scene entries are loaded from
    /// a deterministic manifest; the scene itself owns geometry, anchors, and checkpoints.
    /// </summary>
    public static class TrackCatalog
    {
        public const string WorkshopCircuitId = "rc-club-workshop";
        public const string PatioSweepId = "sunset-patio-sweep";
        public const string MarketAislesId = "market-aisle-dash";
        public const string BridgeExhibitionId = "club-bridge-exhibition";
        public const string BetaTemplateId = "custom-beta-track";
        public const string BetaTemplateScenePath = "Assets/Scenes/Track_BetaTemplate.unity";

        private static readonly TrackDefinition[] entries =
        {
            new(WorkshopCircuitId, "RC CLUB WORKSHOP",
                "BALANCED FOUR-CORNER CLUB CIRCUIT",
                "CUTTING-MAT ROAD / WORKSHOP SCALE / 8 CHECKPOINTS",
                3, PrototypeCircuitBuilder.OrderedCheckpointCount,
                (race, parent, visualStyle) => PrototypeCircuitBuilder.Build(race, parent, visualStyle)),
            new(PatioSweepId, "SUNSET PATIO SWEEP",
                "FAST, WIDE SWEEPS AROUND A CLUB PATIO",
                "TERRACOTTA PATIO / LONG ARCS / 11 CHECKPOINTS",
                3, 11, Stage9CAuthoredTrackBuilder.BuildPatioSweep),
            new(MarketAislesId, "MARKET AISLE DASH",
                "TECHNICAL STOP-AND-TURN STORAGE AISLES",
                "CARDBOARD LANES / SWITCHBACKS / 11 CHECKPOINTS",
                3, 11, Stage9CAuthoredTrackBuilder.BuildMarketAisles),
            new(BridgeExhibitionId, "CLUB BRIDGE EXHIBITION",
                "FLOWING S-BENDS WITH A LOW SHOW BRIDGE",
                "METAL DECK / GENTLE ELEVATION / 12 CHECKPOINTS",
                3, 12, Stage9CAuthoredTrackBuilder.BuildBridgeExhibition)
        };

        private static readonly TrackDefinition betaTemplate = new(BetaTemplateId, "BETA TRACK TEMPLATE",
            "EDITOR-AUTHORED TEST COURSE", "EDITABLE ROAD / 8 CHECKPOINTS", 3, 8,
            null, BetaTemplateScenePath, "beta-template-v1");
        private static TrackDefinition[] localEntries = BuildSelectableEntries();

        // Generated builders remain a useful synchronous subset for existing callers.
        public static IReadOnlyList<TrackDefinition> Entries => entries;
        public static IReadOnlyList<TrackDefinition> SelectableEntries => localEntries;
        public static IReadOnlyList<TrackDefinition> LocalEntries => localEntries;
        public static TrackDefinition Default => entries[0];

        /// <summary>Refresh after an Editor authoring command changes the manifest without a domain reload.</summary>
#if UNITY_EDITOR
        public static void ReloadSceneManifest(string json) => localEntries = BuildSelectableEntries(json);
#endif

        private static TrackDefinition[] BuildSelectableEntries(string manifestJson = null)
        {
            var result = new List<TrackDefinition>(entries.Length + 1);
            result.AddRange(entries);
            result.Add(betaTemplate);
            if (manifestJson == null)
            {
                TextAsset asset = Resources.Load<TextAsset>(SceneTrackManifest.ResourceName);
                if (asset == null)
                {
                    Debug.LogError("[TrackCatalog] Missing scene track manifest: " + SceneTrackManifest.AssetPath);
                    return result.ToArray();
                }
                manifestJson = asset.text;
            }
            try
            {
                SceneTrackManifest manifest = SceneTrackManifest.Parse(manifestJson);
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (TrackDefinition builtIn in result) ids.Add(builtIn.Id);
                foreach (SceneTrackRecord track in manifest.Tracks)
                {
                    if (!ids.Add(track.Id))
                        throw new FormatException($"Scene track ID '{track.Id}' conflicts with an existing track.");
                    result.Add(new TrackDefinition(track.Id, track.DisplayName, track.Description,
                        track.PreviewMetadata, track.LapCount, track.OrderedCheckpointCount,
                        null, track.ScenePath, track.ContentRevision));
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[TrackCatalog] Invalid scene track manifest: " + exception.Message);
                return new[] { entries[0], entries[1], entries[2], entries[3], betaTemplate };
            }
            return result.ToArray();
        }

        public static bool TryGet(string id, out TrackDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            for (int i = 0; i < localEntries.Length; i++)
            {
                if (!string.Equals(localEntries[i].Id, id, StringComparison.Ordinal)) continue;
                definition = localEntries[i];
                return true;
            }
            return false;
        }

        public static bool IsAvailable(string id) => TryGet(id, out TrackDefinition definition) && definition.IsAvailable;

        public static bool TryGetLocal(string id, out TrackDefinition definition)
        {
            if (TryGet(id, out definition)) return true;
            definition = string.Equals(id, betaTemplate.Id, StringComparison.Ordinal) ? betaTemplate : null;
            return definition != null;
        }

        public static bool IsLocalAvailable(string id) => TryGetLocal(id, out TrackDefinition definition) && definition.IsAvailable;
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
            if (SelectedTrack.IsSceneAuthored)
                throw new InvalidOperationException($"Track '{SelectedTrackId}' requires an additive scene load.");
            ITrackRuntime runtime = SelectedTrack.Builder(race, parent, visualStyle);
            if (runtime == null) throw new InvalidOperationException($"Track '{SelectedTrackId}' returned no runtime.");
            return runtime;
        }
    }
}
