using System;
using System.Collections.Generic;

namespace Presentation
{
    /// <summary>Integer road coordinates; heading is clockwise in 45-degree steps from east.</summary>
    public readonly struct TrackSocket : IEquatable<TrackSocket>
    {
        public TrackSocket(int x, int z, int level, int heading)
        {
            if (heading < 0 || heading > 7) throw new ArgumentOutOfRangeException(nameof(heading));
            X = x;
            Z = z;
            Level = level;
            Heading = heading;
        }

        public int X { get; }
        public int Z { get; }
        public int Level { get; }
        // The direction of travel through the socket, rather than the outward face of a mesh.
        public int Heading { get; }

        public bool Equals(TrackSocket other) => X == other.X && Z == other.Z &&
                                                  Level == other.Level && Heading == other.Heading;
        public override bool Equals(object obj) => obj is TrackSocket other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return (((X * 397) ^ Z) * 397 ^ Level) * 397 ^ Heading; }
        }
        public static bool operator ==(TrackSocket left, TrackSocket right) => left.Equals(right);
        public static bool operator !=(TrackSocket left, TrackSocket right) => !left.Equals(right);
    }

    /// <summary>Road data carries the future grip choice independently of visual theme.</summary>
    public enum TrackRoadSurface { Standard, Slick, HighGrip }

    public sealed class TrackPieceSpec
    {
        internal TrackPieceSpec(string id, int forwardCells, int exitCells, int lateralCells,
            int turnSteps, int levelChange, bool raisedOnly, bool diagonalOnly, bool cardinalOnly)
        {
            Id = id;
            ForwardCells = forwardCells;
            ExitCells = exitCells;
            LateralCells = lateralCells;
            TurnSteps = turnSteps;
            LevelChange = levelChange;
            RaisedOnly = raisedOnly;
            DiagonalOnly = diagonalOnly;
            CardinalOnly = cardinalOnly;
        }

        public string Id { get; }
        public int ForwardCells { get; }
        public int ExitCells { get; }
        public int LateralCells { get; }
        public int TurnSteps { get; }
        public int LevelChange { get; }
        public bool RaisedOnly { get; }
        public bool DiagonalOnly { get; }
        public bool CardinalOnly { get; }
    }

    /// <summary>Allowlisted first road kit. Exact geometry and vehicle clearance remain a later compiler gate.</summary>
    public static class InGameTrackKit
    {
        public const string Version = "kit-1";
        public const string StartId = "start-straight";

        private static readonly TrackPieceSpec[] Pieces =
        {
            new(StartId, 8, 0, 0, 0, 0, false, false, false),
            new("straight-short", 4, 0, 0, 0, 0, false, false, true),
            new("straight-long", 8, 0, 0, 0, 0, false, false, true),
            new("diagonal-short", 4, 0, 0, 0, 0, false, true, false),
            new("diagonal-long", 8, 0, 0, 0, 0, false, true, false),
            new("transition-left-45", 3, 2, 0, 1, 0, false, false, false),
            new("transition-right-45", 3, 2, 0, -1, 0, false, false, false),
            new("corner-left-tight", 2, 2, 0, 2, 0, false, false, false),
            new("corner-right-tight", 2, 2, 0, -2, 0, false, false, false),
            new("corner-left-wide", 4, 4, 0, 2, 0, false, false, false),
            new("corner-right-wide", 4, 4, 0, -2, 0, false, false, false),
            new("s-bend-left", 6, 0, 2, 0, 0, false, false, false),
            new("s-bend-right", 6, 0, -2, 0, 0, false, false, false),
            new("ramp-up", 8, 0, 0, 0, 1, false, false, false),
            new("ramp-down", 8, 0, 0, 0, -1, false, false, false),
            new("bridge-straight", 4, 0, 0, 0, 0, true, false, false),
            new("bridge-corner-left", 4, 4, 0, 2, 0, true, false, false),
            new("bridge-corner-right", 4, 4, 0, -2, 0, true, false, false)
        };

        private static readonly Dictionary<string, TrackPieceSpec> ById = BuildIndex();
        private static readonly IReadOnlyList<TrackPieceSpec> ReadOnlyPieces = Array.AsReadOnly(Pieces);

        public static IReadOnlyList<TrackPieceSpec> All => ReadOnlyPieces;

        public static bool TryGet(string id, out TrackPieceSpec spec) =>
            ById.TryGetValue(id ?? string.Empty, out spec);

        private static Dictionary<string, TrackPieceSpec> BuildIndex()
        {
            var result = new Dictionary<string, TrackPieceSpec>(Pieces.Length, StringComparer.Ordinal);
            foreach (TrackPieceSpec spec in Pieces) result.Add(spec.Id, spec);
            return result;
        }

        internal static TrackSocket Exit(TrackSocket entry, TrackPieceSpec spec)
        {
            int exitHeading = (entry.Heading + spec.TurnSteps + 8) % 8;
            (int dx, int dz) = Direction(entry.Heading);
            (int ex, int ez) = Direction(exitHeading);
            (int sx, int sz) = Direction((entry.Heading + 2) % 8);
            return new TrackSocket(entry.X + dx * spec.ForwardCells + ex * spec.ExitCells - sx * spec.LateralCells,
                entry.Z + dz * spec.ForwardCells + ez * spec.ExitCells - sz * spec.LateralCells,
                entry.Level + spec.LevelChange, exitHeading);
        }

        private static (int x, int z) Direction(int heading)
        {
            // Clockwise compass ordering in the XZ plane: E, SE, S, SW, W, NW, N, NE.
            return heading switch
            {
                0 => (1, 0), 1 => (1, -1), 2 => (0, -1), 3 => (-1, -1),
                4 => (-1, 0), 5 => (-1, 1), 6 => (0, 1), _ => (1, 1)
            };
        }
    }

    public sealed class InGameTrackLimits
    {
        public InGameTrackLimits(int maximumLevel = 3, int maximumPieces = 128, int minimumLoopPieces = 12,
            int maximumCoordinate = 512)
        {
            if (maximumLevel < 1 || maximumLevel > 32) throw new ArgumentOutOfRangeException(nameof(maximumLevel));
            if (maximumPieces < 4 || maximumPieces > 1024) throw new ArgumentOutOfRangeException(nameof(maximumPieces));
            if (minimumLoopPieces < 4 || minimumLoopPieces > maximumPieces)
                throw new ArgumentOutOfRangeException(nameof(minimumLoopPieces));
            if (maximumCoordinate < 32 || maximumCoordinate > 8192)
                throw new ArgumentOutOfRangeException(nameof(maximumCoordinate));
            MaximumLevel = maximumLevel;
            MaximumPieces = maximumPieces;
            MinimumLoopPieces = minimumLoopPieces;
            MaximumCoordinate = maximumCoordinate;
        }

        public int MaximumLevel { get; }
        public int MaximumPieces { get; }
        public int MinimumLoopPieces { get; }
        public int MaximumCoordinate { get; }
    }

    public readonly struct TrackPiecePlacement
    {
        internal TrackPiecePlacement(TrackPieceSpec spec, TrackSocket entry, TrackSocket exit,
            TrackRoadSurface surface)
        { Spec = spec; Entry = entry; Exit = exit; Surface = surface; }

        public TrackPieceSpec Spec { get; }
        public TrackSocket Entry { get; }
        public TrackSocket Exit { get; }
        public TrackRoadSurface Surface { get; }
    }

    /// <summary>
    /// Editable ordered draft. Snapping changes only this model; it never creates race checkpoints
    /// or updates a live TrackSession. A later compiler owns geometry and physical clearance.
    /// </summary>
    public sealed class InGameTrackLayout
    {
        private readonly List<TrackPiecePlacement> route = new();
        private readonly InGameTrackLimits limits;
        private readonly IReadOnlyList<TrackPiecePlacement> routeView;

        public InGameTrackLayout(InGameTrackLimits limits = null, int startHeading = 0)
        {
            this.limits = limits ?? new InGameTrackLimits();
            routeView = route.AsReadOnly();
            var entry = new TrackSocket(0, 0, 0, startHeading);
            TrackPieceSpec start = InGameTrackKit.All[0];
            route.Add(new TrackPiecePlacement(start, entry, InGameTrackKit.Exit(entry, start),
                TrackRoadSurface.Standard));
        }

        public IReadOnlyList<TrackPiecePlacement> Route => routeView;
        public TrackSocket OpenEnd => route[route.Count - 1].Exit;
        public bool IsClosed => route.Count > 1 && OpenEnd == route[0].Entry;

        public bool TryAppend(string pieceId, TrackRoadSurface surface, out string error)
        {
            if (IsClosed) { error = "The route is already closed."; return false; }
            if (route.Count >= limits.MaximumPieces) { error = "Road piece limit reached."; return false; }
            if (!InGameTrackKit.TryGet(pieceId, out TrackPieceSpec spec) || spec.Id == InGameTrackKit.StartId)
            { error = "Choose an allowlisted road piece."; return false; }
            if (!Enum.IsDefined(typeof(TrackRoadSurface), surface))
            { error = "Choose a supported road surface."; return false; }
            TrackSocket entry = OpenEnd;
            if (spec.RaisedOnly && entry.Level == 0)
            { error = "Bridge pieces require a raised road level."; return false; }
            if ((spec.DiagonalOnly && entry.Heading % 2 == 0) ||
                (spec.CardinalOnly && entry.Heading % 2 != 0))
            { error = "This road piece does not fit the open end's heading."; return false; }
            TrackSocket exit = InGameTrackKit.Exit(entry, spec);
            if (exit.Level < 0 || exit.Level > limits.MaximumLevel)
            { error = "This ramp leaves the supported height range."; return false; }
            if (Math.Abs(exit.X) > limits.MaximumCoordinate || Math.Abs(exit.Z) > limits.MaximumCoordinate)
            { error = "This piece leaves the editor footprint."; return false; }
            route.Add(new TrackPiecePlacement(spec, entry, exit, surface));
            error = null;
            return true;
        }

        public bool TryRemoveLast()
        {
            if (route.Count <= 1) return false;
            route.RemoveAt(route.Count - 1);
            return true;
        }

        /// <summary>Topology checks for draft feedback; geometry and car clearance are separate blockers.</summary>
        public IReadOnlyList<string> ValidateTopology()
        {
            var errors = new List<string>();
            if (route.Count < limits.MinimumLoopPieces)
                errors.Add($"Add at least {limits.MinimumLoopPieces} road pieces for a raceable loop.");
            if (!IsClosed)
                errors.Add("Connect the open end back to the start at the same height and heading.");
            var visited = new HashSet<TrackSocket> { route[0].Entry };
            for (int i = 0; i < route.Count; i++)
            {
                TrackPiecePlacement piece = route[i];
                if (i > 0 && piece.Entry != route[i - 1].Exit)
                    errors.Add($"Piece {i + 1} is disconnected from the previous piece.");
                if (!visited.Add(piece.Exit) && !(i == route.Count - 1 && piece.Exit == route[0].Entry))
                    errors.Add($"Piece {i + 1} revisits an existing socket.");
            }
            return errors;
        }
    }
}
