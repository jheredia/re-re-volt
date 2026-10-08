using System.Collections;
using System.Collections.Generic;
using Core;
using NUnit.Framework;
using Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class Stage8SurfaceArtIntegrationTests
    {
        private const int BaselineDrivableLayer = 8;
        private const int BaselineDrivableMask = 1 << BaselineDrivableLayer;
        private const int BaselineDrivableColliderCount = 8;

        // Copied from the accepted pre-8D circuit contract so coordinated production changes cannot bless themselves.
        private static readonly Pose[] BaselineGridPoses =
        {
            new(new Vector3(-16.4f, .76f, -7f), Quaternion.identity),
            new(new Vector3(-13.6f, .76f, -7f), Quaternion.identity),
            new(new Vector3(-16.4f, .76f, -10f), Quaternion.identity),
            new(new Vector3(-13.6f, .76f, -10f), Quaternion.identity)
        };

        private static readonly Pose[] BaselineCheckpointPoses =
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

        private GameObject root;
        private VisualStyleController style;
        private PrototypeCircuitBuilder.Result circuit;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Stage8SurfaceArtTest");
            style = root.AddComponent<VisualStyleController>();
            RaceManager race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, 8), Is.True);
            circuit = PrototypeCircuitBuilder.Build(race, root.transform, style);
        }

        [TearDown]
        public void TearDown()
        {
            circuit?.Dispose();
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RetainedSurfaceResourcesLoadWithExpectedRuntimeDimensionsAndWrapModes()
        {
            Texture2D road = Resources.Load<Texture2D>(PrototypeCircuitBuilder.RoadTextureResource);
            Texture2D workbench = Resources.Load<Texture2D>(PrototypeCircuitBuilder.WorkbenchTextureResource);
            Texture2D decals = Resources.Load<Texture2D>(PrototypeCircuitBuilder.SurfaceDecalTextureResource);

            Assert.That(road, Is.Not.Null);
            Assert.That(workbench, Is.Not.Null);
            Assert.That(decals, Is.Not.Null);
            Assert.That(road, Is.SameAs(circuit.RoadTexture));
            Assert.That(workbench, Is.SameAs(circuit.WorkbenchTexture));
            Assert.That(decals, Is.SameAs(circuit.SurfaceDecalTexture));
            Assert.That(road.width, Is.EqualTo(1024));
            Assert.That(road.height, Is.EqualTo(1024));
            Assert.That(workbench.width, Is.EqualTo(1024));
            Assert.That(workbench.height, Is.EqualTo(1024));
            Assert.That(decals.width, Is.EqualTo(1024));
            Assert.That(decals.height, Is.EqualTo(512));
            Assert.That(road.wrapModeU, Is.EqualTo(TextureWrapMode.Mirror));
            Assert.That(road.wrapModeV, Is.EqualTo(TextureWrapMode.Mirror));
            Assert.That(road.wrapModeW, Is.EqualTo(TextureWrapMode.Mirror));
            Assert.That(workbench.wrapModeU, Is.EqualTo(TextureWrapMode.Mirror));
            Assert.That(workbench.wrapModeV, Is.EqualTo(TextureWrapMode.Mirror));
            Assert.That(workbench.wrapModeW, Is.EqualTo(TextureWrapMode.Mirror));
            Assert.That(decals.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(road.mipmapCount, Is.GreaterThan(1));
            Assert.That(workbench.mipmapCount, Is.GreaterThan(1));
            Assert.That(decals.mipmapCount, Is.EqualTo(1));
        }

        [Test]
        public void SurfaceTexturesAndAllDecalsUseSharedOwnedReferencesWithinExactBudgets()
        {
            Renderer road = FindRenderer("Road_West");
            Renderer workbench = FindRenderer("Workbench Surface");
            Assert.That(road.sharedMaterial.mainTexture, Is.SameAs(circuit.RoadTexture));
            Assert.That(workbench.sharedMaterial.mainTexture, Is.SameAs(circuit.WorkbenchTexture));
            Assert.That(road.sharedMaterial.mainTextureScale, Is.EqualTo(new Vector2(4f, 4f)));
            Assert.That(workbench.sharedMaterial.mainTextureScale, Is.EqualTo(new Vector2(8f, 8f)));
            int sharedRoadRendererCount = 0;
            foreach (Renderer candidate in circuit.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (!candidate.gameObject.name.StartsWith("Road_")) continue;
                sharedRoadRendererCount++;
                Assert.That(candidate.sharedMaterial, Is.SameAs(road.sharedMaterial), candidate.name);
                Assert.That(candidate.sharedMaterial.mainTexture, Is.SameAs(circuit.RoadTexture), candidate.name);
            }
            Assert.That(sharedRoadRendererCount, Is.EqualTo(8));

            Assert.That(circuit.SurfaceDecalRoot, Is.Not.Null);
            Assert.That(circuit.SurfaceDecalRoot.name, Is.EqualTo("Stage8_SurfaceDecals"));
            Assert.That(circuit.SurfaceDecalRoot.transform.parent, Is.SameAs(circuit.Root.transform));
            Renderer[] decals = circuit.SurfaceDecalRoot.GetComponentsInChildren<Renderer>(true);
            Assert.That(decals, Has.Length.EqualTo(PrototypeCircuitBuilder.SurfaceDecalRendererBudget));
            Assert.That(circuit.SurfaceDecalRendererCount,
                Is.EqualTo(PrototypeCircuitBuilder.SurfaceDecalRendererBudget));
            Assert.That(PrototypeCircuitBuilder.SurfaceDecalMaterialBudget, Is.EqualTo(1));
            Assert.That(circuit.OwnsRuntimeMaterial(circuit.SurfaceDecalMaterial), Is.True);
            Material decalMaterial = circuit.SurfaceDecalMaterial;
            Assert.That(decalMaterial.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            Assert.That(decalMaterial.HasProperty("_Surface"), Is.True);
            Assert.That(decalMaterial.GetFloat("_Surface"), Is.EqualTo(1f));
            Assert.That(decalMaterial.HasProperty("_ZWrite"), Is.True);
            Assert.That(decalMaterial.GetFloat("_ZWrite"), Is.EqualTo(0f));
            Assert.That(decalMaterial.HasProperty("_SrcBlend"), Is.True);
            Assert.That(decalMaterial.GetFloat("_SrcBlend"), Is.EqualTo((float)BlendMode.SrcAlpha));
            Assert.That(decalMaterial.HasProperty("_DstBlend"), Is.True);
            Assert.That(decalMaterial.GetFloat("_DstBlend"),
                Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));
            Assert.That(decalMaterial.HasProperty("_SrcBlendAlpha"), Is.True);
            Assert.That(decalMaterial.GetFloat("_SrcBlendAlpha"), Is.EqualTo((float)BlendMode.One));
            Assert.That(decalMaterial.HasProperty("_DstBlendAlpha"), Is.True);
            Assert.That(decalMaterial.GetFloat("_DstBlendAlpha"),
                Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));
            Assert.That(decalMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
            Assert.That(decalMaterial.GetTag("RenderType", false), Is.EqualTo("Transparent"));
            Assert.That(decalMaterial.GetShaderPassEnabled("ShadowCaster"), Is.False);
            Assert.That(decalMaterial.GetShaderPassEnabled("DepthOnly"), Is.False);

            HashSet<Material> materials = new();
            HashSet<Mesh> meshes = new();
            foreach (Renderer decal in decals)
            {
                materials.Add(decal.sharedMaterial);
                meshes.Add(decal.GetComponent<MeshFilter>().sharedMesh);
                Assert.That(decal.sharedMaterial, Is.SameAs(circuit.SurfaceDecalMaterial), decal.name);
                Assert.That(decal.sharedMaterial.mainTexture, Is.SameAs(circuit.SurfaceDecalTexture), decal.name);
                Assert.That(decal.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off), decal.name);
                Assert.That(decal.receiveShadows, Is.False, decal.name);
            }
            Assert.That(materials, Has.Count.EqualTo(PrototypeCircuitBuilder.SurfaceDecalMaterialBudget));
            Assert.That(meshes, Has.Count.EqualTo(PrototypeCircuitBuilder.SurfaceDecalRendererBudget),
                "Each atlas cell is owned by one bounded quad mesh.");
            AssertDecalAtlasUvInsets();
        }

        [Test]
        public void DecalLayerHasNoCollidersStaysInWorkbenchBoundsAndCannotBecomeDrivable()
        {
            Assert.That(circuit.SurfaceDecalRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(circuit.DrivableMask.value, Is.EqualTo(BaselineDrivableMask));

            Renderer[] decals = circuit.SurfaceDecalRoot.GetComponentsInChildren<Renderer>(true);
            List<Renderer> protectedMarks = new();
            foreach (Renderer candidate in circuit.Root.GetComponentsInChildren<Renderer>(true))
            {
                string name = candidate.gameObject.name;
                if (name.Contains("Dash") || name.StartsWith("GridMark_") || name.StartsWith("CheckpointMark_"))
                    protectedMarks.Add(candidate);
            }
            foreach (Renderer decal in decals)
            {
                Bounds bounds = decal.bounds;
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-24f), decal.name);
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(24f), decal.name);
                Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-24f), decal.name);
                Assert.That(bounds.max.z, Is.LessThanOrEqualTo(24f), decal.name);
                Assert.That(decal.gameObject.layer, Is.Not.EqualTo(BaselineDrivableLayer), decal.name);

                float y = decal.transform.localPosition.y;
                bool clearsRoad = Mathf.Abs(y - (.15f + PrototypeCircuitBuilder.SurfaceDecalClearance)) < .0001f;
                bool clearsWorkbench = Mathf.Abs(y - (-.05f + PrototypeCircuitBuilder.SurfaceDecalClearance)) < .0001f;
                Assert.That(clearsRoad || clearsWorkbench, Is.True,
                    $"{decal.name} must retain the authored anti-z-fighting clearance.");
                foreach (Renderer mark in protectedMarks)
                    Assert.That(IntersectsXZ(bounds, mark.bounds), Is.False,
                        $"{decal.name} overlaps protected route marking {mark.name} in the racing plane.");
            }
        }

        [Test]
        public void SurfaceLayerPreservesGridCheckpointAndDrivableAuthority()
        {
            Assert.That(PrototypeCircuitBuilder.DrivableLayer, Is.EqualTo(BaselineDrivableLayer));
            Assert.That(circuit.DrivableMask.value, Is.EqualTo(BaselineDrivableMask));
            Assert.That(circuit.Grid, Has.Length.EqualTo(4));
            for (int i = 0; i < circuit.Grid.Length; i++)
            {
                Pose expected = BaselineGridPoses[i];
                Assert.That(Vector3.Distance(circuit.Grid[i].localPosition, expected.position), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(circuit.Grid[i].localRotation, expected.rotation), Is.LessThan(.001f));
            }

            Assert.That(circuit.Checkpoints, Has.Length.EqualTo(8));
            for (int i = 0; i < circuit.Checkpoints.Length; i++)
            {
                Pose expected = BaselineCheckpointPoses[i];
                Transform checkpoint = circuit.Checkpoints[i].transform;
                Transform recoveryAnchor = circuit.Root.transform.Find($"RecoveryAnchor_{i:00}");
                Assert.That(recoveryAnchor, Is.Not.Null);
                Assert.That(Vector3.Distance(recoveryAnchor.localPosition, expected.position), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(recoveryAnchor.localRotation, expected.rotation), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(checkpoint.localPosition, expected.position + Vector3.up),
                    Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(checkpoint.localRotation, expected.rotation), Is.LessThan(.001f));
            }

            Collider[] colliders = circuit.Root.GetComponentsInChildren<Collider>(true);
            int drivableColliderCount = 0;
            foreach (Collider collider in colliders)
                if (collider.gameObject.layer == BaselineDrivableLayer) drivableColliderCount++;
            Assert.That(drivableColliderCount, Is.EqualTo(BaselineDrivableColliderCount),
                "Stage 8 surface presentation must not add to the eight original road colliders.");
        }

        [UnityTest]
        public IEnumerator SurfaceTilingAndTransparentDecalOwnershipSurviveEveryVisualModeAndCleanup()
        {
            Renderer road = FindRenderer("Road_West");
            Renderer workbench = FindRenderer("Workbench Surface");
            Renderer[] decals = circuit.SurfaceDecalRoot.GetComponentsInChildren<Renderer>(true);
            int sourceRendererCount = SourceRenderers(circuit.Root).Length;
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(sourceRendererCount));

            VisualStyleMode[] modes =
            {
                VisualStyleMode.Clean3D, VisualStyleMode.InkLite, VisualStyleMode.FullInk
            };
            foreach (VisualStyleMode mode in modes)
            {
                style.SetMode(mode);
                AssertSurface(road, circuit.RoadTexture, new Vector2(4f, 4f));
                AssertSurface(workbench, circuit.WorkbenchTexture, new Vector2(8f, 8f));
                foreach (Renderer decal in decals)
                {
                    Assert.That(decal.sharedMaterial, Is.SameAs(circuit.SurfaceDecalMaterial), decal.name);
                    Assert.That(decal.transform.Find(decal.name + "_InkOutline"), Is.Null, decal.name);
                }
            }

            MeshFilter[] filters = circuit.SurfaceDecalRoot.GetComponentsInChildren<MeshFilter>(true);
            Material ownedDecalMaterial = circuit.SurfaceDecalMaterial;
            circuit.Dispose();
            circuit = null;
            Assert.That(style.RegisteredRendererCount, Is.Zero);
            Assert.That(style.OutlineObjectCount, Is.Zero);
            yield return null;
            foreach (MeshFilter filter in filters)
                Assert.That(filter.sharedMesh, Is.Null, $"Owned decal mesh survived disposal on {filter.name}.");
            Assert.That(ownedDecalMaterial == null, Is.True,
                "The circuit-owned transparent decal material survived circuit disposal.");
        }

        private Renderer FindRenderer(string objectName)
        {
            Transform found = circuit.Root.transform.Find(objectName);
            Assert.That(found, Is.Not.Null, objectName);
            return found.GetComponent<Renderer>();
        }

        private static void AssertSurface(Renderer renderer, Texture expectedTexture, Vector2 expectedTiling)
        {
            Material material = renderer.sharedMaterial;
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(expectedTexture), renderer.name);
            Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(expectedTiling), renderer.name);
            Assert.That(material.GetTextureOffset("_BaseMap"), Is.EqualTo(Vector2.zero), renderer.name);
        }

        private void AssertDecalAtlasUvInsets()
        {
            const int columns = 4;
            const int rows = 2;
            const float runtimeWidth = 1024f;
            const float runtimeHeight = 512f;
            const float epsilon = .000001f;
            for (int cell = 0; cell < PrototypeCircuitBuilder.SurfaceDecalRendererBudget; cell++)
            {
                int column = cell % columns;
                int rowFromTop = cell / columns;
                float cellUMin = column / (float)columns;
                float cellUMax = (column + 1) / (float)columns;
                float cellVMin = (rows - rowFromTop - 1) / (float)rows;
                float cellVMax = (rows - rowFromTop) / (float)rows;
                Vector2[] uv = circuit.SurfaceDecalRoot.transform.GetChild(cell)
                    .GetComponent<MeshFilter>().sharedMesh.uv;

                float uMin = Mathf.Min(uv[0].x, uv[1].x, uv[2].x, uv[3].x);
                float uMax = Mathf.Max(uv[0].x, uv[1].x, uv[2].x, uv[3].x);
                float vMin = Mathf.Min(uv[0].y, uv[1].y, uv[2].y, uv[3].y);
                float vMax = Mathf.Max(uv[0].y, uv[1].y, uv[2].y, uv[3].y);
                Assert.That(uMin, Is.GreaterThan(cellUMin), $"Cell {cell} samples its left boundary.");
                Assert.That(uMax, Is.LessThan(cellUMax), $"Cell {cell} samples its right boundary.");
                Assert.That(vMin, Is.GreaterThan(cellVMin), $"Cell {cell} samples its bottom boundary.");
                Assert.That(vMax, Is.LessThan(cellVMax), $"Cell {cell} samples its top boundary.");
                Assert.That(Mathf.Abs(uMin - (cellUMin + 1f / runtimeWidth)), Is.LessThan(epsilon));
                Assert.That(Mathf.Abs(uMax - (cellUMax - 1f / runtimeWidth)), Is.LessThan(epsilon));
                Assert.That(Mathf.Abs(vMin - (cellVMin + 1f / runtimeHeight)), Is.LessThan(epsilon));
                Assert.That(Mathf.Abs(vMax - (cellVMax - 1f / runtimeHeight)), Is.LessThan(epsilon));
            }
        }

        private static bool IntersectsXZ(Bounds first, Bounds second) =>
            first.min.x <= second.max.x && first.max.x >= second.min.x &&
            first.min.z <= second.max.z && first.max.z >= second.min.z;

        private static Renderer[] SourceRenderers(GameObject owner)
        {
            Renderer[] all = owner.GetComponentsInChildren<Renderer>(true);
            List<Renderer> sources = new(all.Length);
            for (int i = 0; i < all.Length; i++)
                if (!all[i].gameObject.name.EndsWith("_InkOutline")) sources.Add(all[i]);
            return sources.ToArray();
        }
    }
}
