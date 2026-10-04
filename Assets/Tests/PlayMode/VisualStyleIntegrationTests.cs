using System.Collections;
using NUnit.Framework;
using Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class VisualStyleIntegrationTests
    {
        private GameObject root;
        private VisualStyleController controller;
        private Material sourceA;
        private Material sourceB;
        private Mesh multiMaterialMesh;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("VisualStyleIntegrationTest");
            controller = root.AddComponent<VisualStyleController>();
            Shader sourceShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            sourceA = new Material(sourceShader) { name = "Clean Source A", color = Color.cyan };
            sourceB = new Material(sourceShader) { name = "Clean Source B", color = Color.magenta };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sourceA);
            Object.DestroyImmediate(sourceB);
            if (multiMaterialMesh != null) Object.DestroyImmediate(multiMaterialMesh);
        }

        [Test]
        public void RetainedMaterialsResolveToExpectedShaders()
        {
            Material ink = Resources.Load<Material>("VisualStyles/InkLit");
            Material outline = Resources.Load<Material>("VisualStyles/InkOutline");
            Assert.That(ink, Is.Not.Null);
            Assert.That(outline, Is.Not.Null);
            Assert.That(ink.shader.name, Is.EqualTo("ReReVolt/InkLit"));
            Assert.That(outline.shader.name, Is.EqualTo("ReReVolt/InkOutline"));
            Assert.That(ink.shader.isSupported, Is.True);
            Assert.That(outline.shader.isSupported, Is.True);
            Assert.That(ink.FindPass("ForwardLit"), Is.GreaterThanOrEqualTo(0));
            Assert.That(ink.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
            Assert.That(ink.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
            Assert.That(ink.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
            Assert.That(outline.FindPass("InkOutline"), Is.GreaterThanOrEqualTo(0));
            Assert.That(outline.FindPass("ShadowCaster"), Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator ModesRestoreExactSourcesPreservePropertyBlocksAndReuseOwnedResources()
        {
            Renderer first = CreateRenderer("First", sourceA);
            Renderer second = CreateRenderer("Second", sourceA);
            Renderer unoutlined = CreateRenderer("Unoutlined", sourceA);
            MaterialPropertyBlock playerColor = new MaterialPropertyBlock();
            playerColor.SetColor("_BaseColor", Color.green);
            first.SetPropertyBlock(playerColor);

            Assert.That(controller.RegisterRenderer(first, true), Is.True);
            Assert.That(controller.RegisterRenderer(second, true), Is.True);
            Assert.That(controller.RegisterRenderer(unoutlined, false), Is.True);
            Assert.That(controller.RegisterRenderer(first, true), Is.False);
            Assert.That(controller.OutlineObjectCount, Is.EqualTo(2));

            controller.SetMode(VisualStyleMode.InkLite);
            Assert.That(first.sharedMaterial, Is.Not.SameAs(sourceA));
            Assert.That(first.sharedMaterial, Is.SameAs(second.sharedMaterial));
            Assert.That(controller.InkMaterialCount, Is.EqualTo(1));
            Assert.That(ActiveOutlineCount(), Is.Zero);
            AssertPropertyColor(first, Color.green);

            Material cachedInk = first.sharedMaterial;
            for (int i = 0; i < 4; i++)
            {
                controller.SetMode(VisualStyleMode.FullInk);
                Assert.That(ActiveOutlineCount(), Is.EqualTo(2));
                controller.SetMode(VisualStyleMode.Clean3D);
                Assert.That(first.sharedMaterial, Is.SameAs(sourceA));
                Assert.That(second.sharedMaterial, Is.SameAs(sourceA));
                Assert.That(unoutlined.sharedMaterial, Is.SameAs(sourceA));
                controller.SetMode(VisualStyleMode.InkLite);
                Assert.That(first.sharedMaterial, Is.SameAs(cachedInk));
            }

            Assert.That(controller.InkMaterialCount, Is.EqualTo(1));
            Assert.That(controller.OutlineObjectCount, Is.EqualTo(2));
            AssertPropertyColor(first, Color.green);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SourceUpdateAppliesCurrentModeAndControllerCleanupRestoresLatestSource()
        {
            Renderer renderer = CreateRenderer("Mutable", sourceA);
            controller.RegisterRenderer(renderer, true);
            controller.SetMode(VisualStyleMode.FullInk);
            Material firstInk = renderer.sharedMaterial;

            controller.UpdateSourceMaterial(renderer, sourceB);
            Assert.That(renderer.sharedMaterial, Is.Not.SameAs(firstInk));
            Assert.That(renderer.sharedMaterial, Is.Not.SameAs(sourceB));
            Assert.That(controller.InkMaterialCount, Is.EqualTo(2));
            Assert.That(ActiveOutlineCount(), Is.EqualTo(1));

            Object.Destroy(controller);
            yield return null;
            Assert.That(renderer.sharedMaterial, Is.SameAs(sourceB));
            Assert.That(renderer.transform.Find("Mutable_InkOutline"), Is.Null);
        }

        [Test]
        public void IdenticalUpdatesAreNoOpAndMultiMaterialOrderRemainsExact()
        {
            Renderer renderer = CreateMultiMaterialRenderer("Multi", sourceA, sourceB);
            Assert.That(controller.RegisterRenderer(renderer, true), Is.True);
            controller.SetMode(VisualStyleMode.InkLite);
            Material[] initialInk = renderer.sharedMaterials;
            Renderer outline = renderer.transform.Find("Multi_InkOutline").GetComponent<Renderer>();
            Material[] initialOutlines = outline.sharedMaterials;
            Material[] unchangedSources = { sourceA, sourceB };

            for (int i = 0; i < 32; i++)
            {
                Assert.That(controller.UpdateSourceMaterials(renderer, unchangedSources), Is.False);
                Material[] unchangedInk = renderer.sharedMaterials;
                Assert.That(unchangedInk[0], Is.SameAs(initialInk[0]));
                Assert.That(unchangedInk[1], Is.SameAs(initialInk[1]));
                Material[] unchangedOutlines = outline.sharedMaterials;
                Assert.That(unchangedOutlines[0], Is.SameAs(initialOutlines[0]));
                Assert.That(unchangedOutlines[1], Is.SameAs(initialOutlines[1]));
            }

            Assert.That(controller.InkMaterialCount, Is.EqualTo(2));
            Assert.That(controller.OutlineObjectCount, Is.EqualTo(1));
            Assert.That(controller.UpdateSourceMaterials(renderer, new[] { sourceB, sourceA }), Is.True);
            controller.SetMode(VisualStyleMode.Clean3D);
            Material[] reordered = renderer.sharedMaterials;
            Assert.That(reordered[0], Is.SameAs(sourceB));
            Assert.That(reordered[1], Is.SameAs(sourceA));

            Renderer single = CreateRenderer("SingleNoOp", sourceA);
            controller.RegisterRenderer(single, false);
            controller.SetMode(VisualStyleMode.InkLite);
            Material singleInk = single.sharedMaterial;
            for (int i = 0; i < 32; i++)
                Assert.That(controller.UpdateSourceMaterial(single, sourceA), Is.False);
            Assert.That(single.sharedMaterial, Is.SameAs(singleInk));
            Assert.That(controller.InkMaterialCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DestroyedRenderersArePrunedExplicitlyAndBeforeNoOpModeChanges()
        {
            controller.SetMode(VisualStyleMode.InkLite);
            for (int i = 0; i < 6; i++)
            {
                Renderer renderer = CreateRenderer("Transient_" + i, sourceA);
                controller.RegisterRenderer(renderer, true);
                Assert.That(controller.RegisteredRendererCount, Is.EqualTo(1));
                GameObject sourceObject = renderer.gameObject;
                Assert.That(sourceObject.transform.Find(sourceObject.name + "_InkOutline"), Is.Not.Null);
                Object.Destroy(renderer);
                yield return null;

                if (i == 0)
                    Assert.That(controller.PruneDestroyedRenderers(), Is.EqualTo(1));
                else
                    controller.SetMode(controller.Mode);

                Assert.That(controller.RegisteredRendererCount, Is.Zero);
                Assert.That(controller.OutlineObjectCount, Is.Zero);
                yield return null;
                Assert.That(sourceObject.transform.Find(sourceObject.name + "_InkOutline"), Is.Null);
                Object.Destroy(sourceObject);
                yield return null;
            }
            Assert.That(controller.InkMaterialCount, Is.EqualTo(1));
        }

        [Test]
        public void SkinnedRenderersCanBeStyledWithoutOutlineButOutlinedRegistrationIsDeferred()
        {
            GameObject skinnedObject = new GameObject("Skinned");
            skinnedObject.transform.SetParent(root.transform, false);
            SkinnedMeshRenderer skinned = skinnedObject.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMaterial = sourceA;

            Assert.That(controller.RegisterRenderer(skinned, false), Is.True);
            controller.SetMode(VisualStyleMode.InkLite);
            Assert.That(skinned.sharedMaterial, Is.Not.SameAs(sourceA));
            System.NotSupportedException exception = Assert.Throws<System.NotSupportedException>(
                () => controller.RegisterRenderer(skinned, true));
            StringAssert.Contains("deferred", exception.Message);
            Assert.That(controller.RegisteredRendererCount, Is.EqualTo(1));
            Assert.That(controller.OutlineObjectCount, Is.Zero);
            controller.SetMode(VisualStyleMode.Clean3D);
            Assert.That(skinned.sharedMaterial, Is.SameAs(sourceA));
        }

        private Renderer CreateRenderer(string name, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = name;
            primitive.transform.SetParent(root.transform, false);
            Renderer renderer = primitive.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private Renderer CreateMultiMaterialRenderer(string name, Material first, Material second)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(root.transform, false);
            Mesh mesh = new Mesh { name = name + " Mesh", subMeshCount = 2 };
            multiMaterialMesh = mesh;
            mesh.vertices = new[]
            {
                new Vector3(-1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f)
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            mesh.RecalculateNormals();
            MeshFilter filter = item.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { first, second };
            return renderer;
        }

        private int ActiveOutlineCount()
        {
            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.EndsWith("_InkOutline") && child.gameObject.activeSelf) count++;
            }
            return count;
        }

        private static void AssertPropertyColor(Renderer renderer, Color expected)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(expected));
        }
    }
}
