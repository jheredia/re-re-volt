using System;
using Core;
using UnityEngine;

namespace Presentation
{
    /// <summary>Runtime-built original craft-room circuit used by the vertical slice.</summary>
    public static class PrototypeCircuitBuilder
    {
        public const int OrderedCheckpointCount = 8;
        public const int GridCount = 4;
        public const int DrivableLayer = 8;

        private const float RoadHalfExtent = 15f;
        private const float RoadWidth = 7f;
        private static readonly Vector3[] GridPositions =
        {
            new(-16.4f, .76f, -7f),
            new(-13.6f, .76f, -7f),
            new(-16.4f, .76f, -10f),
            new(-13.6f, .76f, -10f)
        };
        private static readonly Pose[] CheckpointPoses =
        {
            new(new Vector3(-15f, .2f, 0f), Quaternion.identity),
            new(new Vector3(-15f, .2f, 10f), Quaternion.identity),
            new(new Vector3(-8f, .2f, 15f), Quaternion.Euler(0f, 90f, 0f)),
            new(new Vector3(8f, .2f, 15f), Quaternion.Euler(0f, 90f, 0f)),
            new(new Vector3(15f, .2f, 9f), Quaternion.Euler(0f, 180f, 0f)),
            new(new Vector3(15f, .2f, -9f), Quaternion.Euler(0f, 180f, 0f)),
            new(new Vector3(8f, .2f, -15f), Quaternion.Euler(0f, 270f, 0f)),
            new(new Vector3(-8f, .2f, -15f), Quaternion.Euler(0f, 270f, 0f))
        };

        public sealed class Result : IDisposable
        {
            private readonly VisualStyleController visualStyle;
            private readonly Renderer[] renderers;

            internal Result(GameObject root, Transform[] grid, RaceCheckpoint[] checkpoints, Material[] materials,
                VisualStyleController styleController, Renderer[] registeredRenderers)
            {
                Root = root;
                Grid = grid;
                Checkpoints = checkpoints;
                RuntimeMaterials = materials;
                visualStyle = styleController;
                renderers = registeredRenderers;
            }

            public GameObject Root { get; }
            public Transform[] Grid { get; }
            public RaceCheckpoint[] Checkpoints { get; }
            public LayerMask DrivableMask => 1 << DrivableLayer;
            internal Material[] RuntimeMaterials { get; }

            public void Dispose()
            {
                if (visualStyle != null && renderers != null)
                    for (int i = 0; i < renderers.Length; i++)
                        visualStyle.UnregisterRenderer(renderers[i]);
                for (int i = 0; i < RuntimeMaterials.Length; i++)
                {
                    if (RuntimeMaterials[i] == null) continue;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(RuntimeMaterials[i]);
                    else UnityEngine.Object.DestroyImmediate(RuntimeMaterials[i]);
                }
            }
        }

        public static Result Build(RaceManager raceManager, Transform parent = null,
            VisualStyleController visualStyle = null)
        {
            if (raceManager == null) throw new ArgumentNullException(nameof(raceManager));

            GameObject root = new GameObject("Stage3_CraftRoomCircuit");
            if (parent != null) root.transform.SetParent(parent, false);

            Material road = CreateMaterial("Cutting Mat Road", new Color(0.075f, 0.22f, 0.20f));
            Material floor = CreateMaterial("Workbench", new Color(0.36f, 0.22f, 0.13f));
            Material barrier = CreateMaterial("Foam Barrier", new Color(0.96f, 0.64f, 0.08f));
            Material stripe = CreateMaterial("Workshop White", new Color(0.92f, 0.91f, 0.82f));
            Material red = CreateMaterial("Tape Red", new Color(0.76f, 0.12f, 0.10f));
            Material blue = CreateMaterial("Painter Tape", new Color(0.12f, 0.46f, 0.77f));
            Material cardboard = CreateMaterial("Cardboard", new Color(0.58f, 0.36f, 0.18f));
            Material metal = CreateMaterial("Paint Tin", new Color(0.55f, 0.59f, 0.60f), .65f);
            Material graphite = CreateMaterial("Graphite", new Color(0.12f, 0.12f, 0.13f));

            CreateBox(root.transform, "Workbench Surface", new Vector3(0f, -.45f, 0f),
                new Vector3(48f, .8f, 48f), floor, true, 0);
            CreateRoad(root.transform, road);
            CreateBarriers(root.transform, barrier, red);
            CreateRouteMarks(root.transform, stripe, barrier);
            CreateWorkshopProps(root.transform, cardboard, metal, blue, barrier, graphite, stripe, red);

            Transform[] grid = CreateGrid(root.transform, stripe);
            RaceCheckpoint[] checkpoints = CreateCheckpoints(root.transform, raceManager, stripe, barrier);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (visualStyle != null)
                for (int i = 0; i < renderers.Length; i++)
                    visualStyle.RegisterRenderer(renderers[i], IsOutlineEligible(renderers[i].gameObject.name));
            return new Result(root, grid, checkpoints,
                new[] { road, floor, barrier, stripe, red, blue, cardboard, metal, graphite }, visualStyle, renderers);
        }

        // Builder-owned categories make outline eligibility explicit without coupling the style controller to names.
        private static bool IsOutlineEligible(string itemName) =>
            itemName.StartsWith("Outer_", StringComparison.Ordinal) ||
            itemName.StartsWith("Inner_", StringComparison.Ordinal) ||
            itemName.StartsWith("StartArch_", StringComparison.Ordinal) ||
            itemName.StartsWith("Cardboard", StringComparison.Ordinal) ||
            itemName.StartsWith("PaintCan_", StringComparison.Ordinal) ||
            itemName == "TapeRoll" ||
            itemName == "Ruler" ||
            itemName.StartsWith("Pencil_", StringComparison.Ordinal) ||
            itemName.StartsWith("Cable_", StringComparison.Ordinal);

        public static Pose GetGridPose(int index)
        {
            if (index < 0 || index >= GridPositions.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return new Pose(GridPositions[index], Quaternion.identity);
        }

        public static Pose GetCheckpointPose(int index)
        {
            if (index < 0 || index >= CheckpointPoses.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return CheckpointPoses[index];
        }

        private static void CreateRoad(Transform parent, Material road)
        {
            CreateBox(parent, "Road_West", new Vector3(-RoadHalfExtent, .05f, 0f),
                new Vector3(RoadWidth, .2f, 30f), road, true, DrivableLayer);
            CreateBox(parent, "Road_North", new Vector3(0f, .05f, RoadHalfExtent),
                new Vector3(30f, .2f, RoadWidth), road, true, DrivableLayer);
            CreateBox(parent, "Road_East", new Vector3(RoadHalfExtent, .05f, 0f),
                new Vector3(RoadWidth, .2f, 30f), road, true, DrivableLayer);
            CreateBox(parent, "Road_South", new Vector3(0f, .05f, -RoadHalfExtent),
                new Vector3(30f, .2f, RoadWidth), road, true, DrivableLayer);

            // Overlapping corner pads keep the route forgiving for compact RC cars.
            CreateBox(parent, "Road_NW", new Vector3(-RoadHalfExtent, .05f, RoadHalfExtent),
                new Vector3(RoadWidth, .2f, RoadWidth), road, true, DrivableLayer);
            CreateBox(parent, "Road_NE", new Vector3(RoadHalfExtent, .05f, RoadHalfExtent),
                new Vector3(RoadWidth, .2f, RoadWidth), road, true, DrivableLayer);
            CreateBox(parent, "Road_SE", new Vector3(RoadHalfExtent, .05f, -RoadHalfExtent),
                new Vector3(RoadWidth, .2f, RoadWidth), road, true, DrivableLayer);
            CreateBox(parent, "Road_SW", new Vector3(-RoadHalfExtent, .05f, -RoadHalfExtent),
                new Vector3(RoadWidth, .2f, RoadWidth), road, true, DrivableLayer);
        }

        private static void CreateBarriers(Transform parent, Material yellow, Material red)
        {
            const float outer = 19f;
            const float inner = 11f;
            CreateBarrierLoop(parent, "Outer", outer, 38f, yellow);
            CreateBarrierLoop(parent, "Inner", inner, 22f, red);
        }

        private static void CreateBarrierLoop(Transform parent, string prefix, float extent, float span, Material material)
        {
            CreateBox(parent, $"{prefix}_West", new Vector3(-extent, .65f, 0f), new Vector3(.45f, 1.2f, span), material, true, 0);
            CreateBox(parent, $"{prefix}_East", new Vector3(extent, .65f, 0f), new Vector3(.45f, 1.2f, span), material, true, 0);
            CreateBox(parent, $"{prefix}_North", new Vector3(0f, .65f, extent), new Vector3(span, 1.2f, .45f), material, true, 0);
            CreateBox(parent, $"{prefix}_South", new Vector3(0f, .65f, -extent), new Vector3(span, 1.2f, .45f), material, true, 0);
        }

        private static Transform[] CreateGrid(Transform parent, Material marker)
        {
            Transform[] grid = new Transform[GridCount];
            for (int i = 0; i < grid.Length; i++)
            {
                Pose pose = GetGridPose(i);
                GameObject anchor = new GameObject($"Grid_{i + 1}");
                anchor.transform.SetParent(parent, false);
                anchor.transform.SetLocalPositionAndRotation(pose.position, pose.rotation);
                grid[i] = anchor.transform;
                CreateBox(parent, $"GridMark_{i + 1}", new Vector3(pose.position.x, .19f, pose.position.z),
                    new Vector3(1.5f, .04f, 2.2f), marker, false, 0);
            }
            return grid;
        }

        private static RaceCheckpoint[] CreateCheckpoints(
            Transform parent, RaceManager raceManager, Material stripe, Material yellow)
        {
            RaceCheckpoint[] checkpoints = new RaceCheckpoint[CheckpointPoses.Length];
            for (int i = 0; i < CheckpointPoses.Length; i++)
            {
                Pose pose = GetCheckpointPose(i);
                GameObject anchor = new GameObject($"RecoveryAnchor_{i:00}");
                anchor.transform.SetParent(parent, false);
                anchor.transform.SetLocalPositionAndRotation(pose.position, pose.rotation);

                GameObject trigger = new GameObject($"Checkpoint_{i:00}");
                trigger.transform.SetParent(parent, false);
                trigger.transform.SetLocalPositionAndRotation(pose.position + Vector3.up, pose.rotation);
                BoxCollider collider = trigger.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(RoadWidth - .8f, 2.5f, .65f);
                RaceCheckpoint checkpoint = trigger.AddComponent<RaceCheckpoint>();
                checkpoint.Configure(raceManager, i, anchor.transform);
                checkpoints[i] = checkpoint;

                CreateBox(parent, $"CheckpointMark_{i:00}", pose.position + Vector3.up * .02f,
                    new Vector3(RoadWidth - .8f, .035f, .28f), i == 0 ? yellow : stripe, false, 0,
                    pose.rotation);
            }

            // Start arch turns the first timing line into a readable landmark.
            CreateBox(parent, "StartArch_Left", new Vector3(-18.05f, 1.65f, 0f), new Vector3(.35f, 3f, .45f), yellow, true, 0);
            CreateBox(parent, "StartArch_Right", new Vector3(-11.95f, 1.65f, 0f), new Vector3(.35f, 3f, .45f), yellow, true, 0);
            CreateBox(parent, "StartArch_Top", new Vector3(-15f, 3.05f, 0f), new Vector3(6.45f, .35f, .45f), yellow, true, 0);
            return checkpoints;
        }

        private static void CreateRouteMarks(Transform parent, Material white, Material yellow)
        {
            for (int z = -12; z <= 12; z += 4)
                CreateBox(parent, $"WestDash_{z}", new Vector3(-15f, .18f, z), new Vector3(.12f, .025f, 1.7f), white, false, 0);
            for (int x = -12; x <= 12; x += 4)
            {
                CreateBox(parent, $"NorthDash_{x}", new Vector3(x, .18f, 15f), new Vector3(1.7f, .025f, .12f), white, false, 0);
                CreateBox(parent, $"SouthDash_{x}", new Vector3(x, .18f, -15f), new Vector3(1.7f, .025f, .12f), white, false, 0);
            }
            for (int z = -12; z <= 12; z += 4)
                CreateBox(parent, $"EastDash_{z}", new Vector3(15f, .18f, z), new Vector3(.12f, .025f, 1.7f), white, false, 0);

            // Direction chevrons on the large central cutting-mat infield.
            CreateBox(parent, "InfieldLabel", new Vector3(0f, .12f, 0f), new Vector3(12f, .08f, 5f), yellow, false, 0);
            CreateBox(parent, "InfieldLabelInner", new Vector3(0f, .17f, 0f), new Vector3(10.8f, .04f, 3.8f), white, false, 0);
        }

        private static void CreateWorkshopProps(Transform parent, Material cardboard, Material metal,
            Material blue, Material yellow, Material graphite, Material white, Material red)
        {
            // Cardboard storage and a small improvised ramp sit outside the racing line.
            CreateBox(parent, "CardboardBox_A", new Vector3(-22f, 1.8f, 14f), new Vector3(5f, 3.6f, 6f), cardboard, true, 0);
            CreateBox(parent, "CardboardBox_B", new Vector3(22f, 1.3f, -13f), new Vector3(5f, 2.6f, 7f), cardboard, true, 0);
            CreateBox(parent, "CardboardRamp", new Vector3(1f, 1f, 5.5f), new Vector3(7f, .35f, 4f), cardboard, true, 0,
                Quaternion.Euler(12f, 0f, 0f));

            CreateCylinder(parent, "PaintCan_Blue", new Vector3(-5f, 1.6f, -4f), new Vector3(2.4f, 1.6f, 2.4f), blue, true);
            CreateCylinder(parent, "PaintCan_Metal", new Vector3(5f, 1.25f, -5f), new Vector3(2f, 1.25f, 2f), metal, true);
            CreateCylinder(parent, "TapeRoll", new Vector3(0f, .8f, -5f), new Vector3(2.8f, .8f, 2.8f), yellow, true);
            CreateCylinder(parent, "TapeHole", new Vector3(0f, 1.63f, -5f), new Vector3(1.35f, .05f, 1.35f), graphite, false);

            // Oversized ruler and pencils sell the toy scale without depending on authored meshes.
            CreateBox(parent, "Ruler", new Vector3(0f, .75f, 9f), new Vector3(15f, .3f, 1.8f), yellow, true, 0,
                Quaternion.Euler(0f, -8f, 0f));
            for (int i = -6; i <= 6; i++)
                CreateBox(parent, $"RulerTick_{i}", new Vector3(i, .94f, 9f - i * .14f), new Vector3(.08f, .04f, i % 2 == 0 ? .9f : .55f), graphite, false, 0,
                    Quaternion.Euler(0f, -8f, 0f));

            CreatePencil(parent, "Pencil_Red", new Vector3(-22f, 1f, -4f), red, graphite, white);
            CreatePencil(parent, "Pencil_Blue", new Vector3(22f, 1f, 5f), blue, graphite, white);

            // A loose cable snakes around one edge using a few broad dark segments.
            for (int i = 0; i < 5; i++)
                CreateBox(parent, $"Cable_{i}", new Vector3(-23f + i * 2.2f, .25f, -20.5f + Mathf.Sin(i) * .8f),
                    new Vector3(2.8f, .28f, .28f), graphite, true, 0, Quaternion.Euler(0f, Mathf.Sin(i) * 22f, 0f));
        }

        private static void CreatePencil(Transform parent, string name, Vector3 position, Material paint,
            Material graphite, Material wood)
        {
            GameObject body = CreateCylinder(parent, name, position, new Vector3(.42f, 6f, .42f), paint, true);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject tip = CreateCylinder(parent, name + "_Wood", position + Vector3.forward * 3.2f,
                new Vector3(.38f, .7f, .38f), wood, false);
            tip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreateCylinder(parent, name + "_Graphite", position + Vector3.forward * 3.65f,
                new Vector3(.2f, .25f, .2f), graphite, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, bool collider, int layer, Quaternion rotation = default)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.layer = layer;
            item.transform.SetParent(parent, false);
            item.transform.SetLocalPositionAndRotation(position, rotation == default ? Quaternion.identity : rotation);
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.Destroy(item.GetComponent<Collider>());
            return item;
        }

        private static GameObject CreateCylinder(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, bool collider)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.Destroy(item.GetComponent<Collider>());
            return item;
        }

        private static Material CreateMaterial(string name, Color color, float metallic = 0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = name, color = color, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .25f);
            return material;
        }
    }
}
