using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Shared construction utility for the three Stage 9C fixed circuits. Layout data remains explicit in this
    /// file: the utility only owns repetitive road, checkpoint, style-registration, and disposal mechanics.
    /// </summary>
    public static class Stage9CAuthoredTrackBuilder
    {
        public const int DrivableLayer = PrototypeCircuitBuilder.DrivableLayer;
        public const int GridCount = 4;
        public const int PickupCount = 4;

        private sealed class Layout
        {
            internal string RootName;
            internal Vector3[] Route;
            internal Vector3[] Pickups;
            internal float RoadWidth;
            internal TrackSurfaceKind RoadSurface;
            internal Color GroundColor;
            internal Color RoadColor;
            internal Color BarrierColor;
            internal Color AccentColor;
            internal Color SecondaryColor;
            internal bool OpenRecoveryLanes;
            internal Action<BuildContext> Decorate;
        }

        private sealed class BuildContext
        {
            internal Transform Root;
            internal Material Accent;
            internal Material Secondary;
            internal Material Neutral;
            internal readonly List<Renderer> Renderers = new();
            internal readonly List<Renderer> OutlineRenderers = new();
        }

        private sealed class Runtime : ITrackRuntime
        {
            private readonly VisualStyleController visualStyle;
            private readonly Renderer[] renderers;
            private readonly Material[] materials;
            private bool disposed;

            internal Runtime(GameObject root, Transform[] grid, RaceCheckpoint[] checkpoints,
                IReadOnlyList<Vector3> pickups, VisualStyleController style, Renderer[] ownedRenderers,
                Material[] ownedMaterials)
            {
                Root = root;
                Grid = grid;
                Checkpoints = checkpoints;
                PickupPositions = pickups;
                visualStyle = style;
                renderers = ownedRenderers;
                materials = ownedMaterials;
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
                if (visualStyle != null)
                    for (int i = 0; i < renderers.Length; i++)
                        if (renderers[i] != null) visualStyle.UnregisterRenderer(renderers[i]);
                for (int i = 0; i < materials.Length; i++) DestroyOwned(materials[i]);
                DestroyOwned(Root);
                Root = null;
            }
        }

        public static ITrackRuntime BuildPatioSweep(RaceManager race, Transform parent,
            VisualStyleController visualStyle) => Build(race, parent, visualStyle, PatioSweep());

        public static ITrackRuntime BuildMarketAisles(RaceManager race, Transform parent,
            VisualStyleController visualStyle) => Build(race, parent, visualStyle, MarketAisles());

        public static ITrackRuntime BuildBridgeExhibition(RaceManager race, Transform parent,
            VisualStyleController visualStyle) => Build(race, parent, visualStyle, BridgeExhibition());

        private static ITrackRuntime Build(RaceManager race, Transform parent, VisualStyleController visualStyle,
            Layout layout)
        {
            if (race == null) throw new ArgumentNullException(nameof(race));

            GameObject root = new(layout.RootName);
            if (parent != null) root.transform.SetParent(parent, false);

            Material ground = CreateMaterial(layout.RootName + " Ground", layout.GroundColor);
            Material road = CreateMaterial(layout.RootName + " Road", layout.RoadColor);
            Material barrier = CreateMaterial(layout.RootName + " Barrier", layout.BarrierColor);
            Material accent = CreateMaterial(layout.RootName + " Accent", layout.AccentColor);
            Material secondary = CreateMaterial(layout.RootName + " Secondary", layout.SecondaryColor);
            Material neutral = CreateMaterial(layout.RootName + " Neutral", new Color(.82f, .78f, .67f));
            Material[] materials = { ground, road, barrier, accent, secondary, neutral };
            BuildContext context = new()
            {
                Root = root.transform,
                Accent = accent,
                Secondary = secondary,
                Neutral = neutral
            };

            Vector3 boundsCenter = RouteCenter(layout.Route);
            boundsCenter.y = -.48f;
            CreatePrimitive(context, PrimitiveType.Cube, "VenueGround", boundsCenter,
                Quaternion.identity, new Vector3(62f, .8f, 62f), ground, true, 0,
                TrackSurfaceKind.WorkbenchWood, false);
            BuildRoadAndBarriers(context, layout, road, barrier);
            Transform[] grid = BuildGrid(context, layout, neutral);
            RaceCheckpoint[] checkpoints = BuildCheckpoints(context, layout, race, neutral, accent);
            layout.Decorate(context);

            Renderer[] renderers = context.Renderers.ToArray();
            if (visualStyle != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                    visualStyle.RegisterRenderer(renderers[i], context.OutlineRenderers.Contains(renderers[i]));
            }
            return new Runtime(root, grid, checkpoints, Array.AsReadOnly((Vector3[])layout.Pickups.Clone()),
                visualStyle, renderers, materials);
        }

        private static void BuildRoadAndBarriers(BuildContext context, Layout layout, Material road,
            Material barrier)
        {
            for (int i = 0; i < layout.Route.Length; i++)
            {
                Vector3 from = layout.Route[i];
                Vector3 to = layout.Route[(i + 1) % layout.Route.Length];
                Vector3 delta = to - from;
                float length = delta.magnitude;
                Quaternion rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                Vector3 midpoint = (from + to) * .5f;
                CreatePrimitive(context, PrimitiveType.Cube, $"Road_{i:00}", midpoint, rotation,
                    new Vector3(layout.RoadWidth, .22f, length + layout.RoadWidth * .35f), road, true,
                    DrivableLayer, layout.RoadSurface, false);

                // Keep corner mouths open so vehicle and AI lines cannot snag on intersecting barrier ends.
                float barrierLength = Mathf.Max(1f, length - layout.RoadWidth * .82f);
                Vector3 right = rotation * Vector3.right;
                float edge = layout.RoadWidth * .5f + .28f;
                CreatePrimitive(context, PrimitiveType.Cube, $"Barrier_{i:00}_L", midpoint - right * edge,
                    rotation, new Vector3(.45f, 1.05f, barrierLength), barrier, true, 0,
                    TrackSurfaceKind.Foam, true);
                CreatePrimitive(context, PrimitiveType.Cube, $"Barrier_{i:00}_R", midpoint + right * edge,
                    rotation, new Vector3(.45f, 1.05f, barrierLength), barrier, true, 0,
                    TrackSurfaceKind.Foam, true);
            }

            for (int i = 0; i < layout.Route.Length; i++)
            {
                Vector3 point = layout.Route[i];
                // The overlapping road slabs own collision at every route node. A primitive cylinder collider
                // adds a vertical rim there, which catches this small chassis one car-length before the checkpoint.
                CreatePrimitive(context, PrimitiveType.Cylinder, $"CornerPad_{i:00}", point,
                    Quaternion.identity, new Vector3(layout.RoadWidth, .11f, layout.RoadWidth), road,
                    !layout.OpenRecoveryLanes,
                    DrivableLayer, layout.RoadSurface, false);
            }
        }

        private static Transform[] BuildGrid(BuildContext context, Layout layout, Material marker)
        {
            Vector3 start = layout.Route[0];
            Vector3 forward = Flatten(layout.Route[1] - start).normalized;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 right = rotation * Vector3.right;
            Transform[] grid = new Transform[GridCount];
            for (int i = 0; i < grid.Length; i++)
            {
                float lateral = i % 2 == 0 ? -1.35f : 1.35f;
                float backward = 3.2f + (i / 2) * 3.2f;
                Vector3 roadPoint = start - forward * backward + right * lateral;
                roadPoint.y = SampleRouteHeight(layout.Route, 0, backward / Vector3.Distance(start, layout.Route[1]));
                GameObject anchor = new($"Grid_{i + 1}");
                anchor.transform.SetParent(context.Root, false);
                anchor.transform.SetLocalPositionAndRotation(roadPoint + Vector3.up * .72f, rotation);
                grid[i] = anchor.transform;
                CreatePrimitive(context, PrimitiveType.Cube, $"GridMark_{i + 1}", roadPoint + Vector3.up * .14f,
                    rotation, new Vector3(1.7f, .035f, 2.35f), marker, false, 0,
                    TrackSurfaceKind.Unclassified, false);
            }
            return grid;
        }

        private static RaceCheckpoint[] BuildCheckpoints(BuildContext context, Layout layout, RaceManager race,
            Material stripe, Material start)
        {
            RaceCheckpoint[] checkpoints = new RaceCheckpoint[layout.Route.Length];
            for (int i = 0; i < layout.Route.Length; i++)
            {
                Vector3 point = layout.Route[i];
                Vector3 forward = Flatten(layout.Route[(i + 1) % layout.Route.Length] - point).normalized;
                Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
                GameObject anchor = new($"RecoveryAnchor_{i:00}");
                anchor.transform.SetParent(context.Root, false);
                anchor.transform.SetLocalPositionAndRotation(point + Vector3.up * .2f, rotation);

                GameObject trigger = new($"Checkpoint_{i:00}");
                trigger.transform.SetParent(context.Root, false);
                trigger.transform.SetLocalPositionAndRotation(point + Vector3.up * 1.2f, rotation);
                BoxCollider collider = trigger.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(layout.RoadWidth - .7f, 2.8f, .75f);
                RaceCheckpoint checkpoint = trigger.AddComponent<RaceCheckpoint>();
                checkpoint.Configure(race, i, anchor.transform);
                checkpoints[i] = checkpoint;

                CreatePrimitive(context, PrimitiveType.Cube, $"CheckpointMark_{i:00}",
                    point + Vector3.up * .14f, rotation,
                    new Vector3(layout.RoadWidth - .75f, .04f, .3f), i == 0 ? start : stripe,
                    false, 0, TrackSurfaceKind.Unclassified, false);
            }
            return checkpoints;
        }

        private static Layout PatioSweep() => new()
        {
            RootName = "Stage9C_SunsetPatioSweep",
            RoadWidth = 8.4f,
            RoadSurface = TrackSurfaceKind.CuttingMat,
            GroundColor = new Color(.56f, .34f, .23f),
            RoadColor = new Color(.18f, .31f, .27f),
            BarrierColor = new Color(.94f, .66f, .16f),
            AccentColor = new Color(.94f, .24f, .14f),
            SecondaryColor = new Color(.14f, .55f, .72f),
            OpenRecoveryLanes = true,
            Route = new[]
            {
                P(-21f, -5f), P(-20f, 9f), P(-13f, 18f), P(2f, 21f), P(17f, 17f),
                P(24f, 7f), P(23f, -8f), P(14f, -18f), P(0f, -21f), P(-14f, -17f), P(-21f, -15f)
            },
            Pickups = new[] { P(-20f, 5f, .95f), P(8f, 19f, .95f), P(22f, -3f, .95f), P(-5f, -20f, .95f) },
            Decorate = context =>
            {
                CreateDecor(context, "PatioUmbrella", new Vector3(0f, 2.6f, 2f),
                    new Vector3(7f, .35f, 7f), context.Accent, PrimitiveType.Cylinder);
                CreateDecor(context, "Cooler", new Vector3(-5f, 1.3f, 7f),
                    new Vector3(4f, 2.6f, 3f), context.Secondary);
                CreateDecor(context, "Planter", new Vector3(7f, 1.1f, -7f),
                    new Vector3(3f, 2.2f, 3f), context.Neutral, PrimitiveType.Cylinder);
            }
        };

        private static Layout MarketAisles() => new()
        {
            RootName = "Stage9C_MarketAisleDash",
            RoadWidth = 7.2f,
            RoadSurface = TrackSurfaceKind.Cardboard,
            GroundColor = new Color(.41f, .39f, .34f),
            RoadColor = new Color(.47f, .31f, .17f),
            BarrierColor = new Color(.19f, .53f, .67f),
            AccentColor = new Color(.92f, .27f, .18f),
            SecondaryColor = new Color(.95f, .74f, .12f),
            OpenRecoveryLanes = true,
            Route = new[]
            {
                P(-20f, -12f), P(-20f, 12f), P(-12f, 12f), P(2f, 12f), P(2f, 6f),
                P(15f, 6f), P(15f, 0f), P(15f, -6f), P(2f, -6f), P(2f, -14f), P(-20f, -20f)
            },
            Pickups = new[] { P(-20f, 2f, .95f), P(-5f, 12f, .95f), P(15f, 2f, .95f), P(-5f, -16f, .95f) },
            Decorate = context =>
            {
                for (int i = 0; i < 3; i++)
                    CreateDecor(context, $"ProduceCrate_{i + 1}", new Vector3(-9f + i * 7f, 1.2f, 3f),
                        new Vector3(4.5f, 2.4f, 3.2f), i % 2 == 0 ? context.Accent : context.Secondary);
                CreateDecor(context, "Awning", new Vector3(-8f, 3.6f, -5f),
                    new Vector3(9f, .35f, 4f), context.Secondary);
            }
        };

        private static Layout BridgeExhibition() => new()
        {
            RootName = "Stage9C_ClubBridgeExhibition",
            RoadWidth = 8f,
            RoadSurface = TrackSurfaceKind.Metal,
            GroundColor = new Color(.16f, .18f, .22f),
            RoadColor = new Color(.34f, .37f, .42f),
            BarrierColor = new Color(.73f, .14f, .22f),
            AccentColor = new Color(.14f, .67f, .79f),
            SecondaryColor = new Color(.93f, .76f, .18f),
            OpenRecoveryLanes = true,
            Route = new[]
            {
                P(-20f, -8f), P(-20f, 9f), P(-11f, 18f), P(3f, 18f, 1f),
                P(16f, 18f, 1f), P(16f, 7f, .8f), P(16f, 0f), P(22f, -10f),
                P(14f, -18f), P(0f, -18f), P(-11f, -18f), P(-20f, -15f)
            },
            Pickups = new[] { P(-20f, 4f, .95f), P(8f, 18f, 1.95f), P(19f, -5f, .95f), P(1f, -18f, .95f) },
            Decorate = context =>
            {
                CreateDecor(context, "ExhibitionPodium", new Vector3(-1f, 1.2f, 2f),
                    new Vector3(8f, 2.4f, 5f), context.Secondary);
                CreateDecor(context, "ClubSpeaker_Left", new Vector3(-8f, 2.5f, 7f),
                    new Vector3(3f, 5f, 3f), context.Accent);
                CreateDecor(context, "ClubSpeaker_Right", new Vector3(8f, 2.5f, -7f),
                    new Vector3(3f, 5f, 3f), context.Accent);
            }
        };

        private static Renderer CreatePrimitive(BuildContext context, PrimitiveType primitiveType, string name,
            Vector3 position, Quaternion rotation, Vector3 scale, Material material, bool collider, int layer,
            TrackSurfaceKind surfaceKind, bool outline)
        {
            GameObject item = GameObject.CreatePrimitive(primitiveType);
            item.name = name;
            item.layer = layer;
            item.transform.SetParent(context.Root, false);
            item.transform.SetLocalPositionAndRotation(position, rotation);
            item.transform.localScale = scale;
            Renderer renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            context.Renderers.Add(renderer);
            if (outline) context.OutlineRenderers.Add(renderer);
            Collider primitiveCollider = item.GetComponent<Collider>();
            if (!collider)
            {
                primitiveCollider.enabled = false;
                DestroyOwned(primitiveCollider);
            }
            else if (surfaceKind != TrackSurfaceKind.Unclassified)
            {
                item.AddComponent<TrackSurfaceMarker>().Configure(surfaceKind);
            }
            return renderer;
        }

        private static void CreateDecor(BuildContext context, string name, Vector3 position, Vector3 scale,
            Material material, PrimitiveType primitive = PrimitiveType.Cube)
        {
            Renderer renderer = CreatePrimitive(context, primitive, name, position, Quaternion.identity, scale,
                material, false, 0, TrackSurfaceKind.Unclassified, true);
            renderer.gameObject.AddComponent<NearCameraPresentationOccludable>()
                .Configure(NearCameraOcclusionKind.Decoration);
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new(shader) { name = name, color = color, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .22f);
            return material;
        }

        private static Vector3 RouteCenter(IReadOnlyList<Vector3> route)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < route.Count; i++) sum += route[i];
            return sum / route.Count;
        }

        private static Vector3 Flatten(Vector3 value) => new(value.x, 0f, value.z);

        private static float SampleRouteHeight(IReadOnlyList<Vector3> route, int segment, float backwardRatio)
        {
            int previous = (segment - 1 + route.Count) % route.Count;
            return Mathf.Lerp(route[segment].y, route[previous].y, Mathf.Clamp01(backwardRatio));
        }

        private static Vector3 P(float x, float z, float y = .11f) => new(x, y, z);

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
