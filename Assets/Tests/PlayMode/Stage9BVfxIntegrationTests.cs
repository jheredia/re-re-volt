using System.Collections;
using System.Reflection;
using AudioPresentation;
using Core;
using NUnit.Framework;
using Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Vehicle;
using Weapons;

namespace Tests.PlayMode
{
    /// <summary>Focused bounds, reuse, routing, surface, style, and cleanup source for Stage 9B pooled VFX.</summary>
    public sealed class Stage9BVfxIntegrationTests
    {
        private GameObject root;
        private VisualStyleController style;
        private RaceManager race;
        private VehicleManager roster;
        private WeaponManager weapons;
        private Stage9BVfxPresentation vfx;
        private GameObject surface;
        private Collider surfaceCollider;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Stage9BVfxTest");
            style = root.AddComponent<VisualStyleController>();
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, 1), Is.True);
            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, null, null, null, style), Is.True);
            Stage3RacePresentation hud = root.AddComponent<Stage3RacePresentation>();
            weapons = root.AddComponent<WeaponManager>();
            weapons.Configure(race, roster, hud, ~0, style);
            vfx = root.AddComponent<Stage9BVfxPresentation>();
            vfx.Configure(race, roster, null, weapons, style, VfxDensityLevel.Full);

            surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.AddComponent<TrackSurfaceMarker>().Configure(TrackSurfaceKind.CuttingMat);
            surfaceCollider = surface.GetComponent<Collider>();
        }

        [TearDown]
        public void TearDown()
        {
            if (surface != null) Object.DestroyImmediate(surface);
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void CollisionPoolDropsNewestAtHardCapAndReusesReleasedEntries()
        {
            for (int i = 0; i < Stage9BVfxPresentation.CollisionHardActiveCap; i++)
                Assert.That(vfx.TryPresentCollision(CollisionPayload(1, i)), Is.True);

            Assert.That(vfx.TryPresentCollision(CollisionPayload(1, 99)), Is.False);
            VfxPoolMetrics saturated = vfx.GetPoolMetrics(Stage9BVfxFamily.Collision);
            Assert.That(saturated.WarmCapacity, Is.EqualTo(Stage9BVfxPresentation.CollisionWarmCapacity));
            Assert.That(saturated.Active, Is.EqualTo(saturated.HardActiveCap));
            Assert.That(saturated.HighWater, Is.EqualTo(saturated.HardActiveCap));
            Assert.That(saturated.Drops, Is.EqualTo(1));
            Assert.That(saturated.Created, Is.EqualTo(saturated.WarmCapacity));

            vfx.ClearActiveEffects();
            Assert.That(vfx.GetPoolMetrics(Stage9BVfxFamily.Collision).Active, Is.Zero);
            Assert.That(vfx.TryPresentCollision(CollisionPayload(1, 100)), Is.True);
            Assert.That(vfx.GetPoolMetrics(Stage9BVfxFamily.Collision).Created,
                Is.EqualTo(saturated.Created), "A returned entry must be reused rather than recreated.");
        }

        [Test]
        public void DensityLevelsSuppressOrBoundElementsWithoutCreatingViewportContent()
        {
            vfx.SetDensity(VfxDensityLevel.Off);
            Assert.That(vfx.TryPresentCollision(CollisionPayload(2, 0)), Is.False);
            Assert.That(vfx.SuppressedRequestCount, Is.EqualTo(1));
            Assert.That(vfx.GetPoolMetrics(Stage9BVfxFamily.Collision).Active, Is.Zero);

            vfx.SetDensity(VfxDensityLevel.Reduced);
            Assert.That(vfx.TryPresentCollision(CollisionPayload(2, 1)), Is.True);
            Assert.That(vfx.GetActiveElementCount(Stage9BVfxFamily.Collision), Is.EqualTo(1));
            Assert.That(vfx.GetActiveRecipientCount(2), Is.EqualTo(1));

            vfx.ClearActiveEffects();
            vfx.SetDensity(VfxDensityLevel.Full);
            Assert.That(vfx.TryPresentCollision(CollisionPayload(2, 2)), Is.True);
            Assert.That(vfx.GetActiveElementCount(Stage9BVfxFamily.Collision),
                Is.EqualTo(Stage9BVfxPresentation.RenderersPerEntry));
            Assert.That(vfx.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(vfx.GetComponentsInChildren<Canvas>(true), Is.Empty);
        }

        [Test]
        public void ExplicitSurfaceMarkerAndPayloadsPreserveExactPresentationContext()
        {
            Assert.That(TrackSurfaceMarker.TryResolve(surfaceCollider, out TrackSurfaceKind kind), Is.True);
            Assert.That(kind, Is.EqualTo(TrackSurfaceKind.CuttingMat));

            GameObject unclassified = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Assert.That(vfx.TryPresentCollision(new VehicleImpactPayload(0, 5f, Vector3.one, Vector3.up,
                unclassified.GetComponent<Collider>(), unclassified.layer, Vector3.forward)), Is.False);
            Assert.That(vfx.SurfaceClassificationMissCount, Is.EqualTo(1));

            GameObject otherVehicle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            otherVehicle.AddComponent<VehicleController>();
            Assert.That(vfx.TryPresentCollision(new VehicleImpactPayload(0, 5f, Vector3.one, Vector3.up,
                otherVehicle.GetComponent<Collider>(), otherVehicle.layer, Vector3.forward)), Is.True,
                "Vehicle contacts use their explicit presentation category rather than requiring a track marker.");
            Assert.That(vfx.SurfaceClassificationMissCount, Is.EqualTo(1));

            WeaponResolvedPresentationPayload resolution = new(null, null, null, WeaponKind.Bolt,
                new Vector3(3f, 2f, 1f), Vector3.right, surfaceCollider, false);
            Assert.That(resolution.Point, Is.EqualTo(new Vector3(3f, 2f, 1f)));
            Assert.That(resolution.Direction, Is.EqualTo(Vector3.right));
            Assert.That(resolution.Collider, Is.SameAs(surfaceCollider));
            Assert.That(resolution.AcceptedHit, Is.False);
            Assert.That(vfx.TryPresentWeaponResolved(resolution), Is.True);

            PickupAcquiredPresentationPayload pickup = new(3, null, WeaponKind.Oil, new Vector3(8f, 1f, 2f));
            Assert.That(pickup.PickupIndex, Is.EqualTo(3));
            Assert.That(pickup.Point, Is.EqualTo(new Vector3(8f, 1f, 2f)));
            Assert.That(pickup.Kind, Is.EqualTo(WeaponKind.Oil));
            RecoveryPresentationPayload recovery = new(new Pose(new Vector3(4f, .7f, 5f),
                Quaternion.Euler(0f, 90f, 0f)));
            Assert.That(recovery.Destination.position, Is.EqualTo(new Vector3(4f, .7f, 5f)));
            Object.DestroyImmediate(otherVehicle);
            Object.DestroyImmediate(unclassified);
        }

        [Test]
        public void DisablingOwnerRestoresLegacyBurstsAndReenableReacquiresOwnership()
        {
            MethodInfo showBurst = typeof(WeaponManager).GetMethod("ShowBurst",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showBurst, Is.Not.Null);

            showBurst.Invoke(weapons, new object[] { Vector3.up, WeaponKind.Bolt });
            Assert.That(weapons.FeedbackCount, Is.Zero,
                "The active pooled owner suppresses only the replaced legacy transient burst.");

            vfx.enabled = false;
            showBurst.Invoke(weapons, new object[] { Vector3.up, WeaponKind.Bolt });
            Assert.That(weapons.FeedbackCount, Is.EqualTo(1),
                "A disabled pooled owner must hand transient presentation back to the legacy fallback.");

            vfx.enabled = true;
            Assert.That(weapons.FeedbackCount, Is.Zero,
                "Reacquiring pooled ownership clears the temporary legacy burst before subscribing.");
            showBurst.Invoke(weapons, new object[] { Vector3.up, WeaponKind.Oil });
            Assert.That(weapons.FeedbackCount, Is.Zero,
                "Legacy transient bursts remain suppressed after pooled ownership is reacquired.");
        }

        [UnityTest]
        public IEnumerator WarmRenderersRegisterOnceAndUnregisterOnFinalDisposal()
        {
            int expectedWarmEntries = Stage9BVfxPresentation.CollisionWarmCapacity +
                                      Stage9BVfxPresentation.SkidWarmCapacity +
                                      Stage9BVfxPresentation.BoltTravelWarmCapacity +
                                      Stage9BVfxPresentation.BoltImpactWarmCapacity +
                                      Stage9BVfxPresentation.OilImpactWarmCapacity +
                                      Stage9BVfxPresentation.CueWarmCapacity;
            Assert.That(vfx.RegisteredSourceRendererCount,
                Is.EqualTo(expectedWarmEntries * Stage9BVfxPresentation.RenderersPerEntry));
            int withVfx = style.RegisteredRendererCount;
            style.SetMode(VisualStyleMode.InkLite);
            style.SetMode(VisualStyleMode.FullInk);
            style.SetMode(VisualStyleMode.Clean3D);
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(withVfx));

            int registeredByVfx = vfx.RegisteredSourceRendererCount;
            Object.Destroy(vfx);
            yield return null;
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(withVfx - registeredByVfx));
        }

        [Test]
        public void CleanupClearsEveryFamilyAndRecipientReference()
        {
            Assert.That(vfx.TryPresentCollision(CollisionPayload(3, 0)), Is.True);
            Assert.That(vfx.TryPresentPickup(new PickupAcquiredPresentationPayload(0, null, WeaponKind.Oil,
                Vector3.zero)), Is.True);
            Assert.That(vfx.GetActiveRecipientCount(3), Is.EqualTo(1));

            vfx.ClearActiveEffects();

            foreach (Stage9BVfxFamily family in System.Enum.GetValues(typeof(Stage9BVfxFamily)))
                Assert.That(vfx.GetPoolMetrics(family).Active, Is.Zero);
            Assert.That(vfx.GetActiveRecipientCount(3), Is.Zero);
            Assert.That(vfx.GetActiveRecipientCount(-1), Is.Zero);
        }

        private VehicleImpactPayload CollisionPayload(int playerIndex, int offset) =>
            new(playerIndex, 6f, new Vector3(offset * .01f, 1f, 0f), Vector3.up, surfaceCollider,
                surface.layer, Vector3.forward);
    }
}
