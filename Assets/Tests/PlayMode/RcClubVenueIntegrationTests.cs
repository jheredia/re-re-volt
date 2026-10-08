using System.Collections;
using Core;
using NUnit.Framework;
using Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class RcClubVenueIntegrationTests
    {
        private GameObject root;
        private VisualStyleController style;
        private PrototypeCircuitBuilder.Result circuit;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Stage8VenueTest");
            style = root.AddComponent<VisualStyleController>();
            RaceManager race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, PrototypeCircuitBuilder.OrderedCheckpointCount), Is.True);
            circuit = PrototypeCircuitBuilder.Build(race, root.transform, style);
        }

        [TearDown]
        public void TearDown()
        {
            circuit?.Dispose();
            Object.DestroyImmediate(root);
        }

        [Test]
        public void VenueHasStableIdentityAndReusesTheCircuitMaterialBudget()
        {
            Assert.That(circuit.VenueRoot, Is.Not.Null);
            Assert.That(circuit.VenueRoot.name, Is.EqualTo("Stage8_RcClubVenue"));
            Assert.That(circuit.VenueRoot.transform.Find("Venue_PitCorner/Venue_PitTable_Top"), Is.Not.Null);
            Assert.That(circuit.VenueRoot.transform.Find("Venue_PitCorner/Venue_Thermos_Body"), Is.Not.Null);
            Assert.That(circuit.VenueRoot.transform.Find("Venue_PitCorner/Venue_Mate_Cup"), Is.Not.Null);
            Assert.That(circuit.VenueRoot.transform.Find("Venue_PaintedTireStacks/Venue_TireStack_1_1"), Is.Not.Null);
            Assert.That(circuit.VenueRoot.transform.Find("Venue_ClubBanner/Venue_ClubBanner_Backdrop"), Is.Not.Null);
            Assert.That(circuit.VenueRoot.transform.Find("Venue_Bunting/Venue_Bunting_Pennant_3"), Is.Not.Null);

            Assert.That(circuit.VenueRendererCount, Is.EqualTo(39));
            Assert.That(circuit.VenueRendererCount, Is.LessThanOrEqualTo(PrototypeCircuitBuilder.VenueRendererBudget));
            Assert.That(circuit.VenueOutlineEligibleRendererCount, Is.EqualTo(23));
            Assert.That(circuit.RuntimeMaterialCount, Is.EqualTo(10));
            Assert.That(style.InkMaterialCount, Is.EqualTo(circuit.RuntimeMaterialCount -
                PrototypeCircuitBuilder.SurfaceDecalMaterialBudget),
                "Shared venue colors and style-preserved decals must not expand the ink-material cache.");

            Renderer[] renderers = SourceRenderers(circuit.VenueRoot);
            Assert.That(renderers, Has.Length.EqualTo(circuit.VenueRendererCount));
            foreach (Renderer renderer in renderers)
            {
                Assert.That(renderer, Is.TypeOf<MeshRenderer>());
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(circuit.OwnsRuntimeMaterial(renderer.sharedMaterial), Is.True,
                    $"{renderer.name} must reference a material owned by the circuit palette.");
                Assert.That(renderer.gameObject.layer, Is.Not.EqualTo(PrototypeCircuitBuilder.DrivableLayer));
            }
        }

        [UnityTest]
        public IEnumerator VenueIsColliderFreeAndContainedBehindTheInnerBarrier()
        {
            // Primitive colliders are removed by the builder; wait one frame for deferred destruction in Play Mode.
            yield return null;
            Assert.That(circuit.VenueRoot.GetComponentsInChildren<Collider>(true), Is.Empty);

            Renderer[] renderers = SourceRenderers(circuit.VenueRoot);
            Collider[] circuitColliders = circuit.Root.GetComponentsInChildren<Collider>(true);
            foreach (Renderer renderer in renderers)
            {
                Bounds bounds = renderer.bounds;
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-PrototypeCircuitBuilder.VenueInfieldSafetyExtent),
                    renderer.name);
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(PrototypeCircuitBuilder.VenueInfieldSafetyExtent),
                    renderer.name);
                Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-PrototypeCircuitBuilder.VenueInfieldSafetyExtent),
                    renderer.name);
                Assert.That(bounds.max.z, Is.LessThanOrEqualTo(PrototypeCircuitBuilder.VenueInfieldSafetyExtent),
                    renderer.name);

                foreach (Collider collider in circuitColliders)
                {
                    if (collider == null || collider.transform.IsChildOf(circuit.VenueRoot.transform)) continue;
                    Assert.That(bounds.Intersects(collider.bounds), Is.False,
                        $"Venue renderer {renderer.name} intersects circuit collider {collider.name}.");
                }
            }

            for (int i = 0; i < PrototypeCircuitBuilder.GridCount; i++)
                Assert.That(Mathf.Abs(PrototypeCircuitBuilder.GetGridPose(i).position.x),
                    Is.GreaterThan(PrototypeCircuitBuilder.VenueInfieldSafetyExtent));
            for (int i = 0; i < PrototypeCircuitBuilder.OrderedCheckpointCount; i++)
            {
                Vector3 checkpoint = PrototypeCircuitBuilder.GetCheckpointPose(i).position;
                Assert.That(Mathf.Max(Mathf.Abs(checkpoint.x), Mathf.Abs(checkpoint.z)),
                    Is.GreaterThan(PrototypeCircuitBuilder.VenueInfieldSafetyExtent));
            }
        }

        [UnityTest]
        public IEnumerator VenueRegistrationIsBoundedAndCircuitDisposalUnregistersEverySource()
        {
            yield return null;
            int sourceRendererCount = SourceRenderers(circuit.Root).Length;
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(sourceRendererCount));

            style.SetMode(VisualStyleMode.FullInk);
            Assert.That(style.OutlineObjectCount, Is.GreaterThanOrEqualTo(circuit.VenueOutlineEligibleRendererCount));
            Assert.That(circuit.VenueRoot.transform
                .Find("Venue_PitCorner/Venue_PitTable_Top/Venue_PitTable_Top_InkOutline"), Is.Not.Null);
            Assert.That(circuit.VenueRoot.transform
                .Find("Venue_ClubBanner/Venue_ClubBanner_MotifBlue/Venue_ClubBanner_MotifBlue_InkOutline"), Is.Null,
                "Flat painted motifs must remain unoutlined.");

            GameObject circuitRoot = circuit.Root;
            circuit.Dispose();
            circuit = null;
            Assert.That(style.RegisteredRendererCount, Is.Zero);
            Assert.That(style.OutlineObjectCount, Is.Zero);
            yield return null;
            foreach (Transform descendant in circuitRoot.GetComponentsInChildren<Transform>(true))
                Assert.That(descendant.name.EndsWith("_InkOutline"), Is.False,
                    $"Disposed circuit retained outline object {descendant.name}.");
        }

        private static Renderer[] SourceRenderers(GameObject owner)
        {
            Renderer[] all = owner.GetComponentsInChildren<Renderer>(true);
            int sourceCount = 0;
            for (int i = 0; i < all.Length; i++)
                if (!all[i].gameObject.name.EndsWith("_InkOutline")) sourceCount++;

            Renderer[] sources = new Renderer[sourceCount];
            int destination = 0;
            for (int i = 0; i < all.Length; i++)
                if (!all[i].gameObject.name.EndsWith("_InkOutline")) sources[destination++] = all[i];
            return sources;
        }
    }
}
