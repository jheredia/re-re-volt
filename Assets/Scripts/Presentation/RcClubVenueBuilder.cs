using System;
using System.Collections.Generic;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Builds the lightweight neighborhood RC-club dressing inside the circuit's protected infield.
    /// It borrows the circuit palette and owns no materials, colliders, or gameplay state.
    /// </summary>
    internal static class RcClubVenueBuilder
    {
        internal const int RendererBudget = 40;
        internal const float InfieldSafetyExtent = 10f;
        private const string RootName = "Stage8_RcClubVenue";

        internal readonly struct Palette
        {
            public Palette(Material yellow, Material white, Material red, Material blue,
                Material wood, Material metal, Material graphite)
            {
                Yellow = Require(yellow, nameof(yellow));
                White = Require(white, nameof(white));
                Red = Require(red, nameof(red));
                Blue = Require(blue, nameof(blue));
                Wood = Require(wood, nameof(wood));
                Metal = Require(metal, nameof(metal));
                Graphite = Require(graphite, nameof(graphite));
            }

            public Material Yellow { get; }
            public Material White { get; }
            public Material Red { get; }
            public Material Blue { get; }
            public Material Wood { get; }
            public Material Metal { get; }
            public Material Graphite { get; }

            private static Material Require(Material material, string parameterName) =>
                material != null ? material : throw new ArgumentNullException(parameterName);
        }

        internal sealed class Result
        {
            private readonly HashSet<Renderer> outlineEligible;

            internal Result(GameObject root, Renderer[] renderers, HashSet<Renderer> eligible)
            {
                Root = root;
                Renderers = renderers;
                outlineEligible = eligible;
            }

            internal GameObject Root { get; }
            internal Renderer[] Renderers { get; }
            internal int OutlineEligibleRendererCount => outlineEligible.Count;
            internal bool IsOutlineEligible(Renderer renderer) => outlineEligible.Contains(renderer);
        }

        internal static Result Build(Transform parent, Palette palette)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            GameObject root = new(RootName);
            root.transform.SetParent(parent, false);

            List<Renderer> renderers = new(RendererBudget);
            HashSet<Renderer> outlineEligible = new();
            CreatePitCorner(root.transform, palette, renderers, outlineEligible);
            CreateTireStacks(root.transform, palette, renderers, outlineEligible);
            CreateClubBanner(root.transform, palette, renderers, outlineEligible);
            CreateBunting(root.transform, palette, renderers, outlineEligible);

            if (renderers.Count > RendererBudget)
                throw new InvalidOperationException(
                    $"RC-club venue exceeded its {RendererBudget}-renderer budget with {renderers.Count} renderers.");

            return new Result(root, renderers.ToArray(), outlineEligible);
        }

        private static void CreatePitCorner(Transform parent, Palette palette, List<Renderer> renderers,
            HashSet<Renderer> outlines)
        {
            Transform group = CreateGroup(parent, "Venue_PitCorner");

            // An improvised trestle table gives the club a practical, lived-in paddock focal point.
            CreateBox(group, "Venue_PitTable_Top", new Vector3(-6.8f, 1.65f, 3.6f),
                new Vector3(5.2f, .28f, 1.8f), palette.Wood, true, renderers, outlines);
            CreateBox(group, "Venue_PitTable_TrestleLeft", new Vector3(-8.45f, .8f, 3.6f),
                new Vector3(.42f, 1.6f, 1.25f), palette.Metal, true, renderers, outlines);
            CreateBox(group, "Venue_PitTable_TrestleRight", new Vector3(-5.15f, .8f, 3.6f),
                new Vector3(.42f, 1.6f, 1.25f), palette.Metal, true, renderers, outlines);

            CreateBox(group, "Venue_Toolbox_Base", new Vector3(-8.2f, 2.03f, 3.62f),
                new Vector3(1.45f, .48f, .72f), palette.Red, true, renderers, outlines);
            CreateBox(group, "Venue_Toolbox_Lid", new Vector3(-8.2f, 2.34f, 3.62f),
                new Vector3(1.5f, .16f, .76f), palette.Graphite, true, renderers, outlines);
            CreateBox(group, "Venue_Toolbox_Handle", new Vector3(-8.2f, 2.55f, 3.62f),
                new Vector3(.65f, .13f, .12f), palette.Metal, false, renderers, outlines);

            CreateCylinder(group, "Venue_Thermos_Body", new Vector3(-6.9f, 2.27f, 3.55f),
                new Vector3(.28f, .47f, .28f), palette.Metal, true, renderers, outlines);
            CreateCylinder(group, "Venue_Thermos_Cap", new Vector3(-6.9f, 2.82f, 3.55f),
                new Vector3(.19f, .08f, .19f), palette.Blue, false, renderers, outlines);
            CreateBox(group, "Venue_Thermos_Handle", new Vector3(-6.55f, 2.3f, 3.55f),
                new Vector3(.12f, .52f, .12f), palette.Blue, false, renderers, outlines);

            CreateCylinder(group, "Venue_Mate_Cup", new Vector3(-6.08f, 2.05f, 3.55f),
                new Vector3(.3f, .25f, .3f), palette.Wood, true, renderers, outlines);
            CreateBox(group, "Venue_Mate_Straw", new Vector3(-5.98f, 2.55f, 3.55f),
                new Vector3(.06f, 1.05f, .06f), palette.Metal, false, renderers, outlines,
                Quaternion.Euler(0f, 0f, -12f));

            CreateBox(group, "Venue_Radio_Body", new Vector3(-5.05f, 2.13f, 3.6f),
                new Vector3(1.12f, .66f, .62f), palette.Blue, true, renderers, outlines);
            CreateCylinder(group, "Venue_Radio_Speaker", new Vector3(-5.05f, 2.13f, 3.27f),
                new Vector3(.24f, .05f, .24f), palette.Graphite, false, renderers, outlines,
                Quaternion.Euler(90f, 0f, 0f));

            CreateCylinder(group, "Venue_Trophy_Base", new Vector3(-7.52f, 1.9f, 3.08f),
                new Vector3(.25f, .12f, .25f), palette.Graphite, true, renderers, outlines);
            CreateCylinder(group, "Venue_Trophy_Stem", new Vector3(-7.52f, 2.2f, 3.08f),
                new Vector3(.09f, .25f, .09f), palette.Yellow, false, renderers, outlines);
            CreateCylinder(group, "Venue_Trophy_Cup", new Vector3(-7.52f, 2.55f, 3.08f),
                new Vector3(.34f, .22f, .34f), palette.Yellow, true, renderers, outlines);

            CreateStool(group, "Venue_Stool_A", new Vector3(-8.4f, 0f, 1.65f), palette, renderers, outlines);
            CreateStool(group, "Venue_Stool_B", new Vector3(-5.5f, 0f, 1.55f), palette, renderers, outlines);
        }

        private static void CreateStool(Transform parent, string name, Vector3 position, Palette palette,
            List<Renderer> renderers, HashSet<Renderer> outlines)
        {
            CreateCylinder(parent, name + "_Pedestal", position + new Vector3(0f, .47f, 0f),
                new Vector3(.15f, .47f, .15f), palette.Metal, true, renderers, outlines);
            CreateCylinder(parent, name + "_Seat", position + new Vector3(0f, 1.02f, 0f),
                new Vector3(.62f, .12f, .62f), palette.Red, true, renderers, outlines);
        }

        private static void CreateTireStacks(Transform parent, Palette palette, List<Renderer> renderers,
            HashSet<Renderer> outlines)
        {
            Transform group = CreateGroup(parent, "Venue_PaintedTireStacks");
            Material[] colors = { palette.Red, palette.Yellow, palette.Blue };
            Vector3[] bases = { new(7.45f, .2f, 4.5f), new(7.45f, .2f, 1.75f) };
            for (int stack = 0; stack < bases.Length; stack++)
                for (int tire = 0; tire < colors.Length; tire++)
                    CreateCylinder(group, $"Venue_TireStack_{stack + 1}_{tire + 1}",
                        bases[stack] + Vector3.up * (.42f * tire), new Vector3(.82f, .2f, .82f),
                        colors[(stack + tire) % colors.Length], true, renderers, outlines);
        }

        private static void CreateClubBanner(Transform parent, Palette palette, List<Renderer> renderers,
            HashSet<Renderer> outlines)
        {
            Transform group = CreateGroup(parent, "Venue_ClubBanner");
            CreateBox(group, "Venue_ClubBanner_LeftPost", new Vector3(-3f, 2f, -9f),
                new Vector3(.25f, 4f, .25f), palette.Graphite, true, renderers, outlines);
            CreateBox(group, "Venue_ClubBanner_RightPost", new Vector3(3f, 2f, -9f),
                new Vector3(.25f, 4f, .25f), palette.Graphite, true, renderers, outlines);
            CreateBox(group, "Venue_ClubBanner_Backdrop", new Vector3(0f, 2.7f, -8.96f),
                new Vector3(5.5f, 2.15f, .16f), palette.Wood, true, renderers, outlines);

            // Layered workshop colors form an original hand-painted geometric club mark, not a flag or logo.
            CreateBox(group, "Venue_ClubBanner_MotifRed", new Vector3(-1.65f, 2.72f, -8.86f),
                new Vector3(1.25f, 1.25f, .045f), palette.Red, false, renderers, outlines,
                Quaternion.Euler(0f, 0f, 45f));
            CreateBox(group, "Venue_ClubBanner_MotifBlue", new Vector3(0f, 2.72f, -8.86f),
                new Vector3(1.25f, 1.25f, .045f), palette.Blue, false, renderers, outlines,
                Quaternion.Euler(0f, 0f, 45f));
            CreateBox(group, "Venue_ClubBanner_MotifYellow", new Vector3(1.65f, 2.72f, -8.86f),
                new Vector3(1.25f, 1.25f, .045f), palette.Yellow, false, renderers, outlines,
                Quaternion.Euler(0f, 0f, 45f));
            CreateBox(group, "Venue_ClubBanner_MotifLine", new Vector3(0f, 1.95f, -8.83f),
                new Vector3(4.35f, .13f, .04f), palette.White, false, renderers, outlines);
        }

        private static void CreateBunting(Transform parent, Palette palette, List<Renderer> renderers,
            HashSet<Renderer> outlines)
        {
            Transform group = CreateGroup(parent, "Venue_Bunting");
            CreateBox(group, "Venue_Bunting_String", new Vector3(0f, 4.3f, -7.6f),
                new Vector3(16f, .055f, .055f), palette.Graphite, false, renderers, outlines);
            Material[] colors = { palette.Red, palette.Yellow, palette.Blue, palette.White, palette.Red };
            for (int i = 0; i < colors.Length; i++)
                CreateBox(group, $"Venue_Bunting_Pennant_{i + 1}", new Vector3(-6f + i * 3f, 4.05f, -7.6f),
                    new Vector3(.5f, .5f, .06f), colors[i], false, renderers, outlines,
                    Quaternion.Euler(0f, 0f, 45f));
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject group = new(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void CreateBox(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, bool outline, List<Renderer> renderers, HashSet<Renderer> outlines,
            Quaternion rotation = default)
        {
            CreatePrimitive(PrimitiveType.Cube, parent, name, position, scale, material, outline,
                renderers, outlines, rotation);
        }

        private static void CreateCylinder(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, bool outline, List<Renderer> renderers, HashSet<Renderer> outlines,
            Quaternion rotation = default)
        {
            CreatePrimitive(PrimitiveType.Cylinder, parent, name, position, scale, material, outline,
                renderers, outlines, rotation);
        }

        private static void CreatePrimitive(PrimitiveType type, Transform parent, string name, Vector3 position,
            Vector3 scale, Material material, bool outline, List<Renderer> renderers,
            HashSet<Renderer> outlines, Quaternion rotation)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.SetLocalPositionAndRotation(position, rotation == default ? Quaternion.identity : rotation);
            item.transform.localScale = scale;
            Renderer renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderers.Add(renderer);
            if (outline) outlines.Add(renderer);

            Collider collider = item.GetComponent<Collider>();
            if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
            else UnityEngine.Object.DestroyImmediate(collider);
        }
    }
}
