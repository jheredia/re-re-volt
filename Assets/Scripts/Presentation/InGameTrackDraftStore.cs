using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Presentation
{
    /// <summary>One local, possibly invalid editor draft. Only the editor may mutate it.</summary>
    public sealed class InGameTrackDraft
    {
        internal InGameTrackDraft(string localId, string name, string themeId, InGameTrackLayout layout)
        {
            LocalId = localId; Name = name; ThemeId = themeId; Layout = layout;
            propsView = props.AsReadOnly();
        }

        private readonly List<InGameTrackProp> props = new();
        private readonly IReadOnlyList<InGameTrackProp> propsView;

        public string LocalId { get; }
        public string Name { get; private set; }
        public string ThemeId { get; private set; }
        public InGameTrackLayout Layout { get; }
        public IReadOnlyList<InGameTrackProp> Props => propsView;

        public bool TryAddProp(string propId, int routePiece, float u, float v, float yaw, out string error)
        {
            if (!InGameTrackPropKit.TryGet(propId, out InGameTrackPropSpec spec))
            { error = "Choose an allowlisted prop."; return false; }
            if (props.Count >= 64 || spec.Collides && CountCollidableProps() >= 24)
            { error = "Prop limit reached."; return false; }
            if (routePiece < 0 || routePiece >= Layout.Route.Count || !IsFinite(u) ||
                !IsFinite(v) || !IsFinite(yaw) || u < 0f || u > 1f || v < -1f || v > 1f ||
                yaw < -360f || yaw > 360f)
            { error = "Place the prop on a supported road surface."; return false; }
            props.Add(new InGameTrackProp(spec, routePiece, u, v, yaw));
            error = null;
            return true;
        }

        public bool TryRemoveLastProp()
        {
            if (props.Count == 0) return false;
            props.RemoveAt(props.Count - 1);
            return true;
        }

        private int CountCollidableProps()
        {
            int count = 0;
            foreach (InGameTrackProp prop in props) if (prop.Spec.Collides) count++;
            return count;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static InGameTrackDraft Create(string name = "My Club Loop", InGameTrackLimits limits = null) =>
            new(Guid.NewGuid().ToString("N"), CheckedName(name), "workshop", new InGameTrackLayout(limits));

        public void Rename(string name) => Name = CheckedName(name);

        public void SetTheme(string themeId)
        {
            if (!IsTheme(themeId)) throw new ArgumentException("Choose Workshop, Patio, or Market.", nameof(themeId));
            ThemeId = themeId;
        }

        internal static bool IsTheme(string id) => id == "workshop" || id == "patio" || id == "market";

        internal static string CheckedName(string value)
        {
            string name = value?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 48)
                throw new ArgumentException("Track name must contain 1–48 characters.", nameof(value));
            foreach (char character in name)
                if (char.IsControl(character) || character == '<' || character == '>')
                    throw new ArgumentException("Track name contains unsupported characters.", nameof(value));
            return name;
        }
    }

    /// <summary>
    /// Versioned local JSON only. Loaded coordinates are recomputed from the allowlisted kit,
    /// so a file cannot invent sockets, scene objects, or race authority.
    /// </summary>
    public sealed class InGameTrackDraftStore
    {
        private const int SchemaVersion = 1;
        private const int MaximumFileBytes = 262144;
        private readonly string directory;
        private readonly InGameTrackLimits limits;

        [Serializable]
        private sealed class SavedDraft
        {
            public int schemaVersion;
            public string kitVersion;
            public string localId;
            public string name;
            public string themeId;
            public SavedPiece[] route;
            public SavedProp[] props;
        }

        [Serializable]
        private sealed class SavedProp
        {
            public string id;
            public int routePiece;
            public float u;
            public float v;
            public float yaw;
        }

        [Serializable]
        private sealed class SavedPiece
        {
            public string id;
            public int x;
            public int z;
            public int level;
            public int heading;
            public int surface;
        }

        public InGameTrackDraftStore(InGameTrackLimits limits = null, string storageDirectory = null)
        {
            this.limits = limits ?? new InGameTrackLimits();
            directory = storageDirectory ?? Path.Combine(Application.persistentDataPath, "TrackDrafts");
        }

        public IReadOnlyList<string> ListLocalIds()
        {
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            var ids = new List<string>();
            try
            {
                foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
                {
                    string id = Path.GetFileNameWithoutExtension(path);
                    if (IsValidLocalId(id)) ids.Add(id);
                    if (ids.Count >= 256) break;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[TrackEditor] Local draft list is unavailable: " + exception.Message);
            }
            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        public bool TrySave(InGameTrackDraft draft, out string error)
        {
            if (draft == null) { error = "There is no track draft to save."; return false; }
            if (!IsValidLocalId(draft.LocalId)) { error = "The track draft ID is invalid."; return false; }
            SavedDraft data = Encode(draft);
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(data, true) + "\n");
            if (bytes.Length > MaximumFileBytes) { error = "Track draft exceeds the file size limit."; return false; }
            string path = Path.Combine(directory, draft.LocalId + ".json");
            string temporary = path + ".tmp";
            string backup = path + ".bak";
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path))
                {
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(path, backup);
                }
                try { File.Move(temporary, path); }
                catch
                {
                    if (!File.Exists(path) && File.Exists(backup)) File.Move(backup, path);
                    throw;
                }
                // Keep the last good revision so an interrupted subsequent save can recover.
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = "Track draft could not be saved: " + exception.Message;
                return false;
            }
        }

        public bool TryLoad(string localId, out InGameTrackDraft draft, out string error)
        {
            draft = null;
            if (!IsValidLocalId(localId)) { error = "The track draft ID is invalid."; return false; }
            string path = Path.Combine(directory, localId + ".json");
            if (!File.Exists(path) && File.Exists(path + ".bak")) path += ".bak";
            try
            {
                if (!File.Exists(path)) { error = "Track draft was not found."; return false; }
                var info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaximumFileBytes)
                { error = "Track draft has an invalid file size."; return false; }
                string json = File.ReadAllText(path, Encoding.UTF8);
                SavedDraft data = JsonUtility.FromJson<SavedDraft>(json);
                if (data == null || data.schemaVersion != SchemaVersion ||
                    data.kitVersion != InGameTrackKit.Version || data.localId != localId ||
                    !InGameTrackDraft.IsTheme(data.themeId) || data.route == null ||
                    data.route.Length < 1 || data.route.Length > limits.MaximumPieces)
                { error = "Track draft has an incompatible schema, kit, or route."; return false; }
                string name = InGameTrackDraft.CheckedName(data.name);
                SavedPiece first = data.route[0];
                if (first == null || first.id != InGameTrackKit.StartId || first.x != 0 ||
                    first.z != 0 || first.level != 0 || first.heading < 0 || first.heading > 7 ||
                    first.surface != (int)TrackRoadSurface.Standard)
                { error = "Track draft has an invalid start piece."; return false; }
                var layout = new InGameTrackLayout(limits, first.heading);
                for (int i = 1; i < data.route.Length; i++)
                {
                    SavedPiece piece = data.route[i];
                    if (piece == null || piece.x != layout.OpenEnd.X || piece.z != layout.OpenEnd.Z ||
                        piece.level != layout.OpenEnd.Level || piece.heading != layout.OpenEnd.Heading ||
                        !Enum.IsDefined(typeof(TrackRoadSurface), piece.surface) ||
                        !layout.TryAppend(piece.id, (TrackRoadSurface)piece.surface, out error))
                    { error = $"Track draft piece {i + 1} is invalid or disconnected."; return false; }
                }
                draft = new InGameTrackDraft(localId, name, data.themeId, layout);
                if (data.props != null)
                {
                    if (data.props.Length > 64)
                    { error = "Track draft has too many props."; draft = null; return false; }
                    foreach (SavedProp prop in data.props)
                        if (prop == null || !draft.TryAddProp(prop.id, prop.routePiece, prop.u,
                                prop.v, prop.yaw, out error))
                        { error = "Track draft contains an invalid prop."; draft = null; return false; }
                }
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                              exception is ArgumentException)
            {
                error = "Track draft could not be loaded: " + exception.Message;
                return false;
            }
        }

        public static string ContentRevision(InGameTrackDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            byte[] canonical = Encoding.UTF8.GetBytes(JsonUtility.ToJson(Encode(draft), false));
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(canonical);
            return "layout-v1-" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static SavedDraft Encode(InGameTrackDraft draft)
        {
            var result = new SavedDraft
            {
                schemaVersion = SchemaVersion,
                kitVersion = InGameTrackKit.Version,
                localId = draft.LocalId,
                name = draft.Name,
                themeId = draft.ThemeId,
                route = new SavedPiece[draft.Layout.Route.Count],
                props = new SavedProp[draft.Props.Count]
            };
            for (int i = 0; i < result.route.Length; i++)
            {
                TrackPiecePlacement piece = draft.Layout.Route[i];
                result.route[i] = new SavedPiece
                {
                    id = piece.Spec.Id, x = piece.Entry.X, z = piece.Entry.Z,
                    level = piece.Entry.Level, heading = piece.Entry.Heading,
                    surface = (int)piece.Surface
                };
            }
            for (int i = 0; i < result.props.Length; i++)
            {
                InGameTrackProp prop = draft.Props[i];
                result.props[i] = new SavedProp
                {
                    id = prop.Spec.Id, routePiece = prop.RoutePiece,
                    u = prop.U, v = prop.V, yaw = prop.Yaw
                };
            }
            return result;
        }

        private static bool IsValidLocalId(string id) =>
            id != null && id.Length == 32 && Guid.TryParseExact(id, "N", out _);
    }
}
