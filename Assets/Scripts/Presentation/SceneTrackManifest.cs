using System;
using System.Collections.Generic;
using UnityEngine;

namespace Presentation
{
    /// <summary>Serialized metadata for scene courses. The .unity scene remains the geometry authority.</summary>
    [Serializable]
    public sealed class SceneTrackManifest
    {
        public const string ResourceName = "SceneTrackManifest";
        public const string AssetPath = "Assets/Resources/SceneTrackManifest.json";
        public const string SceneFolder = "Assets/Scenes/CustomTracks";
        private const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion;
        [SerializeField] private SceneTrackRecord[] tracks = Array.Empty<SceneTrackRecord>();

        public IReadOnlyList<SceneTrackRecord> Tracks => tracks;

        public static string ScenePathFor(string id) => $"{SceneFolder}/{id}/Track_{id}.unity";

        public static SceneTrackManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Scene track manifest is empty.");
            SceneTrackManifest manifest;
            try { manifest = JsonUtility.FromJson<SceneTrackManifest>(json); }
            catch (Exception exception) { throw new FormatException("Scene track manifest is not valid JSON.", exception); }
            if (manifest == null || manifest.schemaVersion != CurrentSchemaVersion || manifest.tracks == null)
                throw new FormatException("Scene track manifest has an unsupported schema or no track array.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SceneTrackRecord track in manifest.tracks)
            {
                if (track == null || !IsValidId(track.Id) || !ids.Add(track.Id) ||
                    track.OverridesBuiltIn != IsBuiltInTrackId(track.Id) ||
                    string.Equals(track.Id, TrackCatalog.BetaTemplateId, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(track.DisplayName) ||
                    string.IsNullOrWhiteSpace(track.Description) ||
                    !IsValidRevision(track.ContentRevision) ||
                    track.LapCount < 1 || track.OrderedCheckpointCount < 4 ||
                    !string.Equals(track.ScenePath, ScenePathFor(track.Id), StringComparison.Ordinal))
                    throw new FormatException("Scene track manifest contains an invalid or duplicate entry.");
            }
            Array.Sort(manifest.tracks, (a, b) => string.CompareOrdinal(a.Id, b.Id));
            return manifest;
        }

        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64 || id[0] < 'a' || id[0] > 'z') return false;
            for (int i = 1; i < id.Length; i++)
            {
                char c = id[i];
                if ((c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '-') return false;
            }
            return id[id.Length - 1] != '-';
        }

        public static bool IsBuiltInTrackId(string id) =>
            string.Equals(id, TrackCatalog.WorkshopCircuitId, StringComparison.Ordinal) ||
            string.Equals(id, TrackCatalog.PatioSweepId, StringComparison.Ordinal) ||
            string.Equals(id, TrackCatalog.MarketAislesId, StringComparison.Ordinal) ||
            string.Equals(id, TrackCatalog.BridgeExhibitionId, StringComparison.Ordinal);

        public static bool IsValidRevision(string revision)
        {
            if (string.IsNullOrEmpty(revision) || revision.Length > 64) return false;
            foreach (char c in revision)
                if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z') &&
                    (c < '0' || c > '9') && c != '-' && c != '_' && c != '.') return false;
            return true;
        }

        public SceneTrackManifest WithAdded(SceneTrackRecord entry)
        {
            var next = new SceneTrackRecord[tracks.Length + 1];
            Array.Copy(tracks, next, tracks.Length);
            next[tracks.Length] = entry;
            return Parse(JsonUtility.ToJson(new SceneTrackManifest
            { schemaVersion = CurrentSchemaVersion, tracks = next }));
        }

        public SceneTrackManifest WithReplaced(SceneTrackRecord replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            var next = new SceneTrackRecord[tracks.Length];
            bool found = false;
            for (int i = 0; i < tracks.Length; i++)
            {
                bool matches = string.Equals(tracks[i].Id, replacement.Id, StringComparison.Ordinal);
                next[i] = matches ? replacement : tracks[i];
                found |= matches;
            }
            if (!found) throw new ArgumentException("The track ID is not registered in the manifest.", nameof(replacement));
            return Parse(JsonUtility.ToJson(new SceneTrackManifest
            { schemaVersion = CurrentSchemaVersion, tracks = next }));
        }

        public SceneTrackManifest Without(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A track ID is required.", nameof(id));
            var next = new List<SceneTrackRecord>(tracks.Length);
            bool found = false;
            foreach (SceneTrackRecord track in tracks)
            {
                if (string.Equals(track.Id, id, StringComparison.Ordinal))
                {
                    found = true;
                    continue;
                }
                next.Add(track);
            }
            if (!found) throw new ArgumentException("The track ID is not registered in the manifest.", nameof(id));
            return Parse(JsonUtility.ToJson(new SceneTrackManifest
            { schemaVersion = CurrentSchemaVersion, tracks = next.ToArray() }));
        }

        public string ToJson() => JsonUtility.ToJson(this, true) + "\n";
    }

    [Serializable]
    public sealed class SceneTrackRecord
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private string previewMetadata;
        [SerializeField] private string scenePath;
        [SerializeField] private string contentRevision;
        [SerializeField] private int lapCount;
        [SerializeField] private int orderedCheckpointCount;
        [SerializeField] private bool overridesBuiltIn;

        public SceneTrackRecord(string id, string displayName, string description, string previewMetadata,
            string scenePath, string contentRevision, int lapCount, int orderedCheckpointCount,
            bool overridesBuiltIn = false)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.previewMetadata = previewMetadata;
            this.scenePath = scenePath;
            this.contentRevision = contentRevision;
            this.lapCount = lapCount;
            this.orderedCheckpointCount = orderedCheckpointCount;
            this.overridesBuiltIn = overridesBuiltIn;
        }

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public string PreviewMetadata => previewMetadata;
        public string ScenePath => scenePath;
        public string ContentRevision => contentRevision;
        public int LapCount => lapCount;
        public int OrderedCheckpointCount => orderedCheckpointCount;
        public bool OverridesBuiltIn => overridesBuiltIn;
    }
}
