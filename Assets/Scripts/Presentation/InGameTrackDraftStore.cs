using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Presentation
{
    /// <summary>One local, possibly invalid editor draft. Only the editor may mutate it.</summary>
    public sealed class InGameTrackDraft
    {
        internal InGameTrackDraft(string localId, string name, string themeId, InGameTrackLayout layout)
        { LocalId = localId; Name = name; ThemeId = themeId; Layout = layout; }

        public string LocalId { get; }
        public string Name { get; private set; }
        public string ThemeId { get; private set; }
        public InGameTrackLayout Layout { get; }

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

        private static SavedDraft Encode(InGameTrackDraft draft)
        {
            var result = new SavedDraft
            {
                schemaVersion = SchemaVersion,
                kitVersion = InGameTrackKit.Version,
                localId = draft.LocalId,
                name = draft.Name,
                themeId = draft.ThemeId,
                route = new SavedPiece[draft.Layout.Route.Count]
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
            return result;
        }

        private static bool IsValidLocalId(string id) =>
            id != null && id.Length == 32 && Guid.TryParseExact(id, "N", out _);
    }
}
