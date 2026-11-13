using System;
using System.Collections.Generic;
using UnityEngine;

namespace Presentation
{
    /// <summary>Conservative geometry gate before a draft can become a race track.</summary>
    public static class InGameTrackRouteValidator
    {
        private const int SamplesPerPiece = 10;
        public const float MinimumDeckClearance = 3f;

        public readonly struct RoadOverlap
        {
            // Zero-based route indices; the editor presents these as piece numbers + 1.
            public readonly int First;
            public readonly int Second;

            public RoadOverlap(int first, int second) { First = first; Second = second; }
        }

        public static IReadOnlyList<RoadOverlap> FindRoadOverlaps(InGameTrackLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            IReadOnlyList<TrackPiecePlacement> route = layout.Route;
            var overlaps = new List<RoadOverlap>();
            for (int first = 0; first < route.Count; first++)
                for (int second = first + 2; second < route.Count; second++)
                {
                    if (layout.IsClosed && first == 0 && second == route.Count - 1) continue;
                    if (TooClose(route[first], route[second]))
                        overlaps.Add(new RoadOverlap(first, second));
                    if (overlaps.Count >= 8) return overlaps;
                }
            return overlaps;
        }

        public static IReadOnlyList<string> Validate(InGameTrackLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var errors = new List<string>(layout.ValidateTopology());
            if (errors.Count != 0) return errors;
            IReadOnlyList<TrackPiecePlacement> route = layout.Route;
            // Road slabs overlap at joined sockets by design. Every other pair must be
            // separated in plan or by enough vertical space for the toy car and camera.
            foreach (RoadOverlap overlap in FindRoadOverlaps(layout))
            {
                errors.Add($"Road pieces {overlap.First + 1} and {overlap.Second + 1} overlap without safe height clearance (at least {MinimumDeckClearance:0.0} m).");
                if (errors.Count >= 8) return errors;
            }
            for (int bridge = 0; bridge < route.Count; bridge++)
            {
                TrackPiecePlacement piece = route[bridge];
                if (!piece.Spec.Id.StartsWith("bridge-", StringComparison.Ordinal)) continue;
                Vector3 middle = InGameTrackRuntimeBuilder.Sample(piece, .5f);
                Vector3 tangent = InGameTrackRuntimeBuilder.Tangent(piece, .5f).normalized;
                Vector3 right = new(tangent.z, 0f, -tangent.x);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 support = middle + right * (InGameTrackRuntimeBuilder.RoadWidth * .5f + .35f) * sign;
                    for (int road = 0; road < route.Count; road++)
                    {
                        if (road == bridge || road == (bridge + 1) % route.Count ||
                            road == (bridge - 1 + route.Count) % route.Count) continue;
                        if (SupportBlocksRoad(support, route[road]))
                        {
                            errors.Add($"Bridge piece {bridge + 1} has a support in road piece {road + 1}.");
                            return errors;
                        }
                    }
                }
            }
            return errors;
        }

        public static IReadOnlyList<string> Validate(InGameTrackDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            var errors = new List<string>(Validate(draft.Layout));
            if (errors.Count != 0) return errors;
            IReadOnlyList<TrackPiecePlacement> route = draft.Layout.Route;
            foreach (InGameTrackProp prop in draft.Props)
            {
                if (prop.RoutePiece < 0 || prop.RoutePiece >= route.Count)
                { errors.Add("A prop is anchored to a removed road piece."); return errors; }
                if (!prop.Spec.Collides) continue;
                float edge = Mathf.Abs(prop.V) * InGameTrackRuntimeBuilder.RoadWidth * .5f + prop.Spec.Radius;
                if (edge > InGameTrackRuntimeBuilder.RoadWidth * .5f)
                { errors.Add("A collidable prop extends beyond its road surface."); return errors; }
                if (prop.RoutePiece == 0 || prop.U < .11f || prop.U > .89f)
                { errors.Add("Keep the start grid and checkpoint approaches clear of obstacles."); return errors; }
                for (int pickup = 0; pickup < 4; pickup++)
                {
                    int padPiece = Mathf.Min(route.Count - 1,
                        (pickup * route.Count + route.Count / 2) / 4);
                    if (prop.RoutePiece == padPiece && Mathf.Abs(prop.U - .5f) < .15f &&
                        Mathf.Abs(prop.V) < .45f)
                    { errors.Add("Move the obstacle clear of a pickup pad and its approach."); return errors; }
                }
            }
            if (!HasPassableLaneLoop(draft))
                errors.Add("Obstacles block every continuous car-width lane around the loop.");
            return errors;
        }

        private static bool HasPassableLaneLoop(InGameTrackDraft draft)
        {
            IReadOnlyList<TrackPiecePlacement> route = draft.Layout.Route;
            var masks = new List<int>(route.Count * (SamplesPerPiece + 1));
            for (int pieceIndex = 0; pieceIndex < route.Count; pieceIndex++)
            {
                TrackPiecePlacement piece = route[pieceIndex];
                for (int sample = 0; sample <= SamplesPerPiece; sample++)
                {
                    float u = sample / (float)SamplesPerPiece;
                    Vector3 center = InGameTrackRuntimeBuilder.Sample(piece, u);
                    Vector3 tangent = InGameTrackRuntimeBuilder.Tangent(piece, u).normalized;
                    Vector3 right = new(tangent.z, 0f, -tangent.x);
                    int free = 0;
                    for (int lane = 0; lane < 3; lane++)
                    {
                        Vector3 point = center + right * ((lane - 1) * 2.1f);
                        bool blocked = false;
                        foreach (InGameTrackProp prop in draft.Props)
                        {
                            if (!prop.Spec.Collides || prop.RoutePiece != pieceIndex) continue;
                            Vector3 obstacle = PropPosition(piece, prop);
                            Vector3 delta = point - obstacle;
                            float reach = prop.Spec.Radius + 1.05f;
                            if (delta.x * delta.x + delta.z * delta.z < reach * reach &&
                                Mathf.Abs(delta.y) < prop.Spec.Height + 1f)
                            { blocked = true; break; }
                        }
                        if (!blocked) free |= 1 << lane;
                    }
                    masks.Add(free);
                    if (free == 0) return false;
                }
            }
            for (int startLane = 0; startLane < 3; startLane++)
            {
                int reachable = (1 << startLane) & masks[0];
                for (int index = 1; index < masks.Count && reachable != 0; index++)
                {
                    int transitions = reachable | (reachable << 1) | (reachable >> 1);
                    reachable = transitions & masks[index] & 7;
                }
                if (((reachable | reachable << 1 | reachable >> 1) & (1 << startLane)) != 0)
                    return true;
            }
            return false;
        }

        internal static Vector3 PropPosition(TrackPiecePlacement piece, InGameTrackProp prop)
        {
            Vector3 center = InGameTrackRuntimeBuilder.Sample(piece, prop.U);
            Vector3 tangent = InGameTrackRuntimeBuilder.Tangent(piece, prop.U).normalized;
            Vector3 right = new(tangent.z, 0f, -tangent.x);
            return center + right * (prop.V * InGameTrackRuntimeBuilder.RoadWidth * .5f);
        }

        private static bool TooClose(TrackPiecePlacement a, TrackPiecePlacement b)
        {
            float minimumPlanar = InGameTrackRuntimeBuilder.RoadWidth * .85f;
            float limitSquared = minimumPlanar * minimumPlanar;
            for (int i = 0; i <= SamplesPerPiece; i++)
            {
                Vector3 p = InGameTrackRuntimeBuilder.Sample(a, i / (float)SamplesPerPiece);
                for (int j = 0; j <= SamplesPerPiece; j++)
                {
                    Vector3 q = InGameTrackRuntimeBuilder.Sample(b, j / (float)SamplesPerPiece);
                    Vector3 delta = p - q;
                    if (delta.x * delta.x + delta.z * delta.z < limitSquared &&
                        Mathf.Abs(delta.y) < MinimumDeckClearance)
                        return true;
                }
            }
            return false;
        }

        private static bool SupportBlocksRoad(Vector3 support, TrackPiecePlacement road)
        {
            float halfWidth = InGameTrackRuntimeBuilder.RoadWidth * .5f + .7f;
            for (int i = 0; i <= SamplesPerPiece; i++)
            {
                Vector3 point = InGameTrackRuntimeBuilder.Sample(road, i / (float)SamplesPerPiece);
                if (point.y >= support.y - .3f) continue;
                Vector3 delta = point - support;
                if (delta.x * delta.x + delta.z * delta.z < halfWidth * halfWidth)
                    return true;
            }
            return false;
        }
    }
}
