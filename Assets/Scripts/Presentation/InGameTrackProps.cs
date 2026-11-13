using System;
using System.Collections.Generic;

namespace Presentation
{
    public sealed class InGameTrackPropSpec
    {
        internal InGameTrackPropSpec(string id, float radius, float height, bool collides)
        { Id = id; Radius = radius; Height = height; Collides = collides; }

        public string Id { get; }
        public float Radius { get; }
        public float Height { get; }
        public bool Collides { get; }
    }

    public readonly struct InGameTrackProp
    {
        public InGameTrackProp(InGameTrackPropSpec spec, int routePiece, float u, float v, float yaw)
        { Spec = spec; RoutePiece = routePiece; U = u; V = v; Yaw = yaw; }

        public InGameTrackPropSpec Spec { get; }
        public int RoutePiece { get; }
        public float U { get; }
        // Continuous fraction of the road half-width, from -1 at left edge to +1 at right edge.
        public float V { get; }
        public float Yaw { get; }
    }

    /// <summary>Small allowlisted prop kit. Themes recolor props but never change collision hulls.</summary>
    public static class InGameTrackPropKit
    {
        private static readonly InGameTrackPropSpec[] Items =
        {
            new("foam-cone", .52f, 1.1f, true),
            new("cardboard-crate", .9f, 1.55f, true),
            new("paint-tin", .62f, 1.1f, true),
            new("club-flag", .35f, 1.7f, false)
        };
        private static readonly IReadOnlyList<InGameTrackPropSpec> ReadOnlyItems = Array.AsReadOnly(Items);
        private static readonly Dictionary<string, InGameTrackPropSpec> ById = BuildIndex();

        public static IReadOnlyList<InGameTrackPropSpec> All => ReadOnlyItems;
        public static bool TryGet(string id, out InGameTrackPropSpec spec) =>
            ById.TryGetValue(id ?? string.Empty, out spec);

        private static Dictionary<string, InGameTrackPropSpec> BuildIndex()
        {
            var result = new Dictionary<string, InGameTrackPropSpec>(Items.Length, StringComparer.Ordinal);
            foreach (InGameTrackPropSpec item in Items) result.Add(item.Id, item);
            return result;
        }
    }
}
