using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

namespace Presentation
{
    /// <summary>Compiles a closed local layout into the same owned runtime contract as fixed tracks.</summary>
    public static class InGameTrackRuntimeBuilder
    {
        public const int DrivableLayer = PrototypeCircuitBuilder.DrivableLayer;
        public const float CellSize = 2f;
        public const float LevelHeight = 3.5f;
        public const float RoadWidth = 7f;
        private const float RoadThickness = .22f;

        private sealed class Runtime : ITrackRuntime
        {
            private readonly VisualStyleController style;
            private readonly Renderer[] renderers;
            private readonly Material[] materials;
            private bool disposed;

            internal Runtime(GameObject root, Transform[] grid, RaceCheckpoint[] checkpoints,
                Vector3[] pickups, VisualStyleController style, Renderer[] renderers, Material[] materials)
            {
                Root = root;
                Grid = grid;
                Checkpoints = checkpoints;
                PickupPositions = Array.AsReadOnly(pickups);
                this.style = style;
                this.renderers = renderers;
                this.materials = materials;
            }

            public GameObject Root { get; private set; }
            public Transform[] Grid { get; }
            public RaceCheckpoint[] Checkpoints { get; }
            public LayerMask DrivableMask => 1 << DrivableLayer;
            public IReadOnlyList<Vector3> PickupPositions { get; }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (style != null)
                    foreach (Renderer renderer in renderers)
                        if (renderer != null) style.UnregisterRenderer(renderer);
                foreach (Material material in materials) DestroyOwned(material);
                DestroyOwned(Root);
                Root = null;
            }
        }

        public static ITrackRuntime Build(InGameTrackDraft draft, RaceManager race,
            Transform parent = null, VisualStyleController style = null)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (race == null) throw new ArgumentNullException(nameof(race));
            IReadOnlyList<string> issues = InGameTrackRouteValidator.Validate(draft);
            if (issues.Count != 0)
                throw new InvalidOperationException("Track route is not closed: " + string.Join("; ", issues));
            IReadOnlyList<TrackPiecePlacement> route = draft.Layout.Route;

            Material template = Resources.Load<Material>("VehicleOpaqueMaterial");
            if (template == null)
                throw new InvalidOperationException("The player-safe opaque material template is missing.");
            GameObject root = new("LocalTrack_" + draft.LocalId);
            if (parent != null) root.transform.SetParent(parent, false);
            var renderers = new List<Renderer>();
            Material[] materials = CreatePalette(template, draft.ThemeId);
            try
            {
                BuildGround(root.transform, route, materials[0], renderers);
                for (int i = 0; i < route.Count; i++)
                    BuildPiece(root.transform, route[i], i, materials, renderers);
                BuildProps(root.transform, draft, materials, renderers);
                Transform[] grid = BuildGrid(root.transform, route[0], materials[3], renderers);
                RaceCheckpoint[] checkpoints = BuildCheckpoints(root.transform, route, race,
                    materials[3], renderers);
                Vector3[] pickups = BuildPickups(root.transform, route, materials[4], renderers);
                Renderer[] ownedRenderers = renderers.ToArray();
                if (style != null)
                    foreach (Renderer renderer in ownedRenderers)
                        style.RegisterRenderer(renderer, false);
                return new Runtime(root, grid, checkpoints, pickups, style, ownedRenderers, materials);
            }
            catch
            {
                if (style != null)
                    foreach (Renderer renderer in renderers)
                        if (renderer != null) style.UnregisterRenderer(renderer);
                foreach (Material material in materials) DestroyOwned(material);
                DestroyOwned(root);
                throw;
            }
        }

        private static void BuildGround(Transform parent, IReadOnlyList<TrackPiecePlacement> route,
            Material material, List<Renderer> renderers)
        {
            int minX = 0, maxX = 0, minZ = 0, maxZ = 0;
            foreach (TrackPiecePlacement piece in route)
            {
                minX = Mathf.Min(minX, Mathf.Min(piece.Entry.X, piece.Exit.X));
                maxX = Mathf.Max(maxX, Mathf.Max(piece.Entry.X, piece.Exit.X));
                minZ = Mathf.Min(minZ, Mathf.Min(piece.Entry.Z, piece.Exit.Z));
                maxZ = Mathf.Max(maxZ, Mathf.Max(piece.Entry.Z, piece.Exit.Z));
            }
            float width = (maxX - minX) * CellSize + 30f;
            float depth = (maxZ - minZ) * CellSize + 30f;
            var center = new Vector3((minX + maxX) * CellSize * .5f, -.6f,
                (minZ + maxZ) * CellSize * .5f);
            CreateBox(parent, "ClubGround", center, Quaternion.identity,
                new Vector3(width, .8f, depth), material, true, 0,
                TrackSurfaceKind.WorkbenchWood, renderers);
        }

        private static void BuildPiece(Transform parent, TrackPiecePlacement piece, int index,
            Material[] materials, List<Renderer> renderers)
        {
            int samples = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(World(piece.Entry),
                World(piece.Exit)) / 2.5f), 3, 16);
            Vector3 previous = Sample(piece, 0f);
            Material road = piece.Surface switch
            {
                TrackRoadSurface.Slick => materials[5],
                TrackRoadSurface.HighGrip => materials[6],
                _ => materials[1]
            };
            TrackSurfaceKind surfaceKind = piece.Surface switch
            {
                TrackRoadSurface.Slick => TrackSurfaceKind.Metal,
                TrackRoadSurface.HighGrip => TrackSurfaceKind.Rubber,
                _ => TrackSurfaceKind.CuttingMat
            };
            for (int segment = 1; segment <= samples; segment++)
            {
                Vector3 next = Sample(piece, segment / (float)samples);
                Vector3 delta = next - previous;
                if (delta.sqrMagnitude < .001f) continue;
                Vector3 middle = (previous + next) * .5f;
                Quaternion rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                CreateBox(parent, $"Road_{index:000}_{segment:00}", middle, rotation,
                    new Vector3(RoadWidth, RoadThickness, delta.magnitude + .9f), road, true,
                    DrivableLayer, surfaceKind, renderers);
                previous = next;
            }
            // Elevated deck supports sit outside the carriageway; the later physical
            // clearance validator must reject a support that blocks a lower road.
            if (piece.Entry.Level > 0 && piece.Spec.Id.StartsWith("bridge-", StringComparison.Ordinal))
            {
                Vector3 middle = Sample(piece, .5f);
                Vector3 travel = Tangent(piece, .5f).normalized;
                Vector3 side = new Vector3(travel.z, 0f, -travel.x).normalized;
                float height = Mathf.Max(1f, middle.y);
                for (int sign = -1; sign <= 1; sign += 2)
                    CreateBox(parent, $"BridgeSupport_{index:000}_{sign}",
                        new Vector3(middle.x + side.x * (RoadWidth * .5f + .35f) * sign,
                            height * .5f - .08f,
                            middle.z + side.z * (RoadWidth * .5f + .35f) * sign),
                        Quaternion.identity, new Vector3(.45f, height, .45f), materials[2],
                        true, 0, TrackSurfaceKind.Metal, renderers);
            }
        }

        private static Transform[] BuildGrid(Transform parent, TrackPiecePlacement start,
            Material marker, List<Renderer> renderers)
        {
            var grid = new Transform[4];
            Vector3 forward = Horizontal(Tangent(start, .7f)).normalized;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 right = rotation * Vector3.right;
            for (int i = 0; i < grid.Length; i++)
            {
                float t = i < 2 ? .72f : .42f;
                float lateral = i % 2 == 0 ? -1.35f : 1.35f;
                Vector3 roadPoint = Sample(start, t) + right * lateral;
                var anchor = new GameObject($"Grid_{i + 1}");
                anchor.transform.SetParent(parent, false);
                anchor.transform.SetLocalPositionAndRotation(roadPoint + Vector3.up * .72f, rotation);
                grid[i] = anchor.transform;
                CreateBox(parent, $"GridMark_{i + 1}", roadPoint + Vector3.up * .14f,
                    rotation, new Vector3(1.5f, .035f, 2f), marker, false, 0,
                    TrackSurfaceKind.Unclassified, renderers);
            }
            return grid;
        }

        private static RaceCheckpoint[] BuildCheckpoints(Transform parent,
            IReadOnlyList<TrackPiecePlacement> route, RaceManager race, Material marker,
            List<Renderer> renderers)
        {
            var checkpoints = new RaceCheckpoint[route.Count];
            for (int i = 0; i < route.Count; i++)
            {
                TrackPiecePlacement piece = route[i];
                Vector3 forward = Horizontal(Tangent(piece, 1f)).normalized;
                Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
                Vector3 at = World(piece.Exit);
                GameObject recovery = new($"Recovery_{i:000}");
                recovery.transform.SetParent(parent, false);
                recovery.transform.SetLocalPositionAndRotation(Sample(piece, .55f) + Vector3.up * .72f,
                    Quaternion.LookRotation(Horizontal(Tangent(piece, .55f)).normalized, Vector3.up));
                GameObject trigger = new($"Checkpoint_{i:000}");
                trigger.transform.SetParent(parent, false);
                trigger.transform.SetLocalPositionAndRotation(at + Vector3.up * 1.2f, rotation);
                BoxCollider collider = trigger.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(RoadWidth - .6f, 2.8f, 1f);
                RaceCheckpoint checkpoint = trigger.AddComponent<RaceCheckpoint>();
                checkpoint.Configure(race, i, recovery.transform);
                checkpoints[i] = checkpoint;
                CreateBox(parent, $"GateMark_{i:000}", at + Vector3.up * .15f, rotation,
                    new Vector3(RoadWidth - .6f, .035f, .24f), marker, false, 0,
                    TrackSurfaceKind.Unclassified, renderers);
            }
            return checkpoints;
        }

        private static Vector3[] BuildPickups(Transform parent, IReadOnlyList<TrackPiecePlacement> route,
            Material marker, List<Renderer> renderers)
        {
            var pickups = new Vector3[4];
            for (int i = 0; i < pickups.Length; i++)
            {
                int pieceIndex = Mathf.Min(route.Count - 1, (i * route.Count + route.Count / 2) / 4);
                Vector3 at = Sample(route[pieceIndex], .5f);
                pickups[i] = at + Vector3.up * .8f;
                CreateBox(parent, $"PickupPad_{i + 1}", at + Vector3.up * .14f,
                    Quaternion.identity, new Vector3(1.5f, .04f, 1.5f), marker, false, 0,
                    TrackSurfaceKind.Unclassified, renderers);
            }
            return pickups;
        }

        private static void BuildProps(Transform parent, InGameTrackDraft draft,
            Material[] materials, List<Renderer> renderers)
        {
            foreach (InGameTrackProp prop in draft.Props)
            {
                Vector3 road = InGameTrackRouteValidator.PropPosition(draft.Layout.Route[prop.RoutePiece], prop);
                Vector3 scale = prop.Spec.Id switch
                {
                    "cardboard-crate" => new Vector3(1.6f, prop.Spec.Height, 1.6f),
                    "club-flag" => new Vector3(.25f, prop.Spec.Height, .25f),
                    _ => new Vector3(prop.Spec.Radius * 1.5f, prop.Spec.Height,
                        prop.Spec.Radius * 1.5f)
                };
                CreateBox(parent, "Prop_" + prop.Spec.Id,
                    road + Vector3.up * (scale.y * .5f + RoadThickness * .5f),
                    Quaternion.Euler(0f, prop.Yaw, 0f), scale, materials[2], prop.Spec.Collides,
                    0, TrackSurfaceKind.Unclassified, renderers);
            }
        }

        internal static Vector3 Sample(TrackPiecePlacement piece, float t)
        {
            Vector3 start = World(piece.Entry);
            Vector3 end = World(piece.Exit);
            Vector3 beginTangent = Direction(piece.Entry.Heading);
            Vector3 endTangent = Direction(piece.Exit.Heading);
            float distance = Vector3.Distance(Horizontal(start), Horizontal(end));
            float control = Mathf.Max(1f, distance * .32f);
            Vector3 p1 = start + beginTangent * control;
            Vector3 p2 = end - endTangent * control;
            float reverse = 1f - t;
            Vector3 result = reverse * reverse * reverse * start +
                3f * reverse * reverse * t * p1 +
                3f * reverse * t * t * p2 + t * t * t * end;
            result.y = Mathf.Lerp(start.y, end.y, t);
            return result;
        }

        internal static Vector3 Tangent(TrackPiecePlacement piece, float t) =>
            Sample(piece, Mathf.Min(1f, t + .005f)) - Sample(piece, Mathf.Max(0f, t - .005f));

        private static Vector3 World(TrackSocket socket) =>
            new(socket.X * CellSize, .11f + socket.Level * LevelHeight, socket.Z * CellSize);

        private static Vector3 Direction(int heading) => heading switch
        {
            0 => Vector3.right, 1 => new Vector3(1f, 0f, -1f).normalized,
            2 => Vector3.back, 3 => new Vector3(-1f, 0f, -1f).normalized,
            4 => Vector3.left, 5 => new Vector3(-1f, 0f, 1f).normalized,
            6 => Vector3.forward, _ => new Vector3(1f, 0f, 1f).normalized
        };

        private static Vector3 Horizontal(Vector3 value) => new(value.x, 0f, value.z);

        private static Material[] CreatePalette(Material template, string themeId)
        {
            Color ground, road, trim;
            switch (themeId)
            {
                case "patio":
                    ground = new Color(.51f, .29f, .18f); road = new Color(.31f, .30f, .25f);
                    trim = new Color(.98f, .71f, .38f); break;
                case "market":
                    ground = new Color(.52f, .41f, .24f); road = new Color(.27f, .28f, .27f);
                    trim = new Color(.75f, .85f, .41f); break;
                default:
                    ground = new Color(.34f, .23f, .14f); road = new Color(.08f, .23f, .21f);
                    trim = new Color(.96f, .68f, .12f); break;
            }
            return new[]
            {
                CloneMaterial(template, "Editor Ground", ground),
                CloneMaterial(template, "Editor Standard Road", road),
                CloneMaterial(template, "Editor Bridge Support", trim),
                CloneMaterial(template, "Editor Road Mark", new Color(.9f, .88f, .77f)),
                CloneMaterial(template, "Editor Pickup Pad", trim),
                CloneMaterial(template, "Editor Slick Road", Color.Lerp(road, Color.gray, .5f)),
                CloneMaterial(template, "Editor High Grip Road", Color.Lerp(road, Color.black, .3f))
            };
        }

        private static Material CloneMaterial(Material template, string name, Color color)
        {
            Material result = new(template) { name = name, color = color, enableInstancing = true };
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            return result;
        }

        private static void CreateBox(Transform parent, string name, Vector3 position,
            Quaternion rotation, Vector3 size, Material material, bool collides, int layer,
            TrackSurfaceKind surface, List<Renderer> renderers)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.layer = layer;
            item.transform.SetParent(parent, false);
            item.transform.SetLocalPositionAndRotation(position, rotation);
            item.transform.localScale = size;
            Renderer renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderers.Add(renderer);
            Collider collider = item.GetComponent<Collider>();
            if (!collides)
            {
                collider.enabled = false;
                DestroyOwned(collider);
            }
            else if (surface != TrackSurfaceKind.Unclassified)
                item.AddComponent<TrackSurfaceMarker>().Configure(surface);
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
