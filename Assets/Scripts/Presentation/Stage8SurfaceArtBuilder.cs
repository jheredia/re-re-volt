using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Presentation
{
    /// <summary>
    /// Owns Stage 8's authored surface integration and its collider-free decal presentation.
    /// Retained textures belong to Resources; the circuit owns the one runtime decal material.
    /// </summary>
    internal static class Stage8SurfaceArtBuilder
    {
        internal const string RoadTextureResource = "Art/Stage8/CuttingMatRoad";
        internal const string WorkbenchTextureResource = "Art/Stage8/WorkbenchSurface";
        internal const string DecalTextureResource = "Art/Stage8/SurfaceDecals";
        internal const string RootName = "Stage8_SurfaceDecals";
        internal const int RendererBudget = 8;
        internal const int MaterialBudget = 1;
        internal const float SurfaceClearance = .015f;

        internal static readonly Vector2 RoadTiling = new(4f, 4f);
        internal static readonly Vector2 WorkbenchTiling = new(8f, 8f);

        private const int AtlasColumns = 4;
        private const int AtlasRows = 2;
        private const int RuntimeAtlasWidth = 1024;
        private const int RuntimeAtlasHeight = 512;
        // Keep bilinear samples one runtime texel inside each single-mip atlas cell.
        private const float AtlasUInset = 1f / RuntimeAtlasWidth;
        private const float AtlasVInset = 1f / RuntimeAtlasHeight;

        private readonly struct DecalSpec
        {
            internal DecalSpec(string name, int atlasCell, Vector3 position, Vector2 size, float yaw)
            {
                Name = name;
                AtlasCell = atlasCell;
                Position = position;
                Size = size;
                Yaw = yaw;
            }

            internal string Name { get; }
            internal int AtlasCell { get; }
            internal Vector3 Position { get; }
            internal Vector2 Size { get; }
            internal float Yaw { get; }
        }

        // Racing-surface marks sit off the route centre lines. The two club motifs live beyond the outer barrier.
        private static readonly DecalSpec[] Decals =
        {
            new("SurfaceDecal_RepairTape", 0, new Vector3(-17.15f, .15f + SurfaceClearance, 5.5f),
                new Vector2(2.2f, 2f), 18f),
            new("SurfaceDecal_PainterTape", 1, new Vector3(17.1f, .15f + SurfaceClearance, 6f),
                new Vector2(2.4f, 1.8f), -14f),
            new("SurfaceDecal_PaintScuff", 2, new Vector3(-6f, .15f + SurfaceClearance, 17.1f),
                new Vector2(2.2f, 1.8f), 8f),
            new("SurfaceDecal_TireArcs", 3, new Vector3(6f, .15f + SurfaceClearance, -17.1f),
                new Vector2(2f, 2.25f), -20f),
            new("SurfaceDecal_ClubTriangles", 4, new Vector3(-21f, -.05f + SurfaceClearance, 20f),
                new Vector2(2.6f, 2.6f), -8f),
            new("SurfaceDecal_ClubDiamonds", 5, new Vector3(20f, -.05f + SurfaceClearance, 20f),
                new Vector2(2.6f, 2.6f), 12f),
            new("SurfaceDecal_LaneWear", 6, new Vector3(2f, .15f + SurfaceClearance, 12.65f),
                new Vector2(2.2f, 1.8f), 5f),
            new("SurfaceDecal_TwinSkid", 7, new Vector3(12.65f, .15f + SurfaceClearance, -6f),
                new Vector2(2f, 2.4f), -12f)
        };

        internal sealed class Result
        {
            private readonly Mesh[] meshes;

            internal Result(GameObject root, Renderer[] renderers, Mesh[] ownedMeshes, Material decalMaterial,
                Texture2D roadTexture, Texture2D workbenchTexture, Texture2D decalTexture)
            {
                Root = root;
                Renderers = renderers;
                meshes = ownedMeshes;
                DecalMaterial = decalMaterial;
                RoadTexture = roadTexture;
                WorkbenchTexture = workbenchTexture;
                DecalTexture = decalTexture;
            }

            internal GameObject Root { get; }
            internal Renderer[] Renderers { get; }
            internal Material DecalMaterial { get; }
            internal Texture2D RoadTexture { get; }
            internal Texture2D WorkbenchTexture { get; }
            internal Texture2D DecalTexture { get; }

            internal void DisposeMeshes()
            {
                for (int i = 0; i < meshes.Length; i++)
                {
                    if (meshes[i] == null) continue;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(meshes[i]);
                    else UnityEngine.Object.DestroyImmediate(meshes[i]);
                }
            }
        }

        internal static Result Build(Transform parent, Material roadMaterial, Material workbenchMaterial)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (roadMaterial == null) throw new ArgumentNullException(nameof(roadMaterial));
            if (workbenchMaterial == null) throw new ArgumentNullException(nameof(workbenchMaterial));

            Texture2D roadTexture = RequireTexture(RoadTextureResource);
            Texture2D workbenchTexture = RequireTexture(WorkbenchTextureResource);
            Texture2D decalTexture = RequireTexture(DecalTextureResource);
            ApplySurface(roadMaterial, roadTexture, RoadTiling);
            ApplySurface(workbenchMaterial, workbenchTexture, WorkbenchTiling);

            Material decalMaterial = CreateDecalMaterial(decalTexture);
            GameObject root = new(RootName);
            root.transform.SetParent(parent, false);

            List<Renderer> renderers = new(RendererBudget);
            List<Mesh> meshes = new(RendererBudget);
            for (int i = 0; i < Decals.Length; i++)
                CreateDecal(root.transform, Decals[i], decalMaterial, renderers, meshes);

            if (renderers.Count != RendererBudget)
                throw new InvalidOperationException(
                    $"Stage 8 surface art requires exactly {RendererBudget} renderers; built {renderers.Count}.");

            return new Result(root, renderers.ToArray(), meshes.ToArray(), decalMaterial,
                roadTexture, workbenchTexture, decalTexture);
        }

        private static Texture2D RequireTexture(string resourceKey)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourceKey);
            return texture != null
                ? texture
                : throw new InvalidOperationException($"Missing retained texture Resources/{resourceKey}.");
        }

        private static void ApplySurface(Material material, Texture texture, Vector2 tiling)
        {
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
            material.mainTextureOffset = Vector2.zero;
            material.color = Color.white;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", tiling);
                material.SetTextureOffset("_BaseMap", Vector2.zero);
            }
        }

        private static Material CreateDecalMaterial(Texture2D decalTexture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard");
            Material material = new(shader)
            {
                name = "Stage 8 Shared Surface Decals",
                color = Color.white,
                enableInstancing = true,
                renderQueue = (int)RenderQueue.Transparent
            };
            material.mainTexture = decalTexture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", decalTexture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha"))
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
            return material;
        }

        private static void CreateDecal(Transform parent, DecalSpec spec, Material material,
            List<Renderer> renderers, List<Mesh> meshes)
        {
            GameObject decal = new(spec.Name);
            decal.transform.SetParent(parent, false);
            decal.transform.SetLocalPositionAndRotation(spec.Position, Quaternion.Euler(0f, spec.Yaw, 0f));
            decal.transform.localScale = new Vector3(spec.Size.x, 1f, spec.Size.y);

            Mesh mesh = CreateQuadMesh(spec.Name + " Mesh", spec.AtlasCell);
            decal.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = decal.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            renderers.Add(renderer);
            meshes.Add(mesh);
        }

        private static Mesh CreateQuadMesh(string name, int atlasCell)
        {
            int column = atlasCell % AtlasColumns;
            int rowFromTop = atlasCell / AtlasColumns;
            float uMin = column / (float)AtlasColumns + AtlasUInset;
            float uMax = (column + 1) / (float)AtlasColumns - AtlasUInset;
            float vMin = (AtlasRows - rowFromTop - 1) / (float)AtlasRows + AtlasVInset;
            float vMax = (AtlasRows - rowFromTop) / (float)AtlasRows - AtlasVInset;

            Mesh mesh = new() { name = name, hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(-.5f, 0f, -.5f), new Vector3(-.5f, 0f, .5f),
                new Vector3(.5f, 0f, .5f), new Vector3(.5f, 0f, -.5f)
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[]
            {
                new Vector2(uMin, vMin), new Vector2(uMin, vMax),
                new Vector2(uMax, vMax), new Vector2(uMax, vMin)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
