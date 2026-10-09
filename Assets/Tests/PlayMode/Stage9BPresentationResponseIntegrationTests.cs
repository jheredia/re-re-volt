using System.Collections;
using AudioPresentation;
using Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Vehicle;

namespace Tests.PlayMode
{
    /// <summary>Focused lifecycle and ownership coverage for the local-only Stage 9B response owner.</summary>
    public sealed class Stage9BPresentationResponseIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private RaceManager race;
        private VehicleManager roster;

        public override void Setup()
        {
            base.Setup();
            root = new GameObject("Stage9BPresentationTest");
            race = root.AddComponent<RaceManager>();
            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, null), Is.True);
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(root);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator OneToFourHumansReceiveExactlyOneOwnedResponseAndReleaseCleanly()
        {
            for (int i = 0; i < 4; i++)
            {
                Gamepad pad = InputSystem.AddDevice<Gamepad>();
                Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer player), Is.True);
                Assert.That(player.PresentationResponse, Is.Not.Null);
                Assert.That(player.PresentationResponse.PlayerIndex, Is.EqualTo(player.PlayerIndex));
                Assert.That(player.PresentationResponse.gameObject, Is.SameAs(player.Camera.gameObject));
                Assert.That(player.Vehicle.GetComponents<VehiclePresentationResponseOwner>(), Is.Empty);
            }

            Assert.That(roster.Players.Count, Is.EqualTo(4));
            Assert.That(roster.TryLeavePlayer(3), Is.True);
            yield return null;
            Assert.That(roster.Players.Count, Is.EqualTo(3));
        }

        [Test]
        public void GeneratedFamiliesExposeOnlyChildVisualPivotsAndKeepPhysicsRootUntouched()
        {
            foreach (VehicleFamilyId family in new[] { VehicleFamilyId.Touring, VehicleFamilyId.Buggy,
                         VehicleFamilyId.RcTruck, VehicleFamilyId.Drift })
            {
                GameObject car = PrototypeVehicleFactory.Create(new Pose(Vector3.zero, Quaternion.identity), family);
                VehicleFamilyVisual visual = car.GetComponent<VehicleFamilyVisual>();
                Assert.That(visual.PresentationPivot, Is.Not.Null);
                Assert.That(visual.PresentationPivot.parent, Is.SameAs(car.transform));
                Assert.That(car.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(car.GetComponentsInChildren<WheelCollider>().Length, Is.EqualTo(4));
                foreach (Renderer renderer in visual.SourceRenderers)
                    Assert.That(renderer.transform.IsChildOf(visual.PresentationPivot), Is.True);
                Object.DestroyImmediate(car);
            }
        }

        [Test]
        public void FamilyReplacementRetainsTheSameVisualPivotAndDoesNotTouchWheelAuthority()
        {
            GameObject car = PrototypeVehicleFactory.Create(new Pose(Vector3.zero, Quaternion.identity), VehicleFamilyId.Touring);
            VehicleFamilyVisual visual = car.GetComponent<VehicleFamilyVisual>();
            Transform pivot = visual.PresentationPivot;
            WheelCollider[] wheels = car.GetComponentsInChildren<WheelCollider>();
            VehicleFamilyVisualReplacement replacement = PrototypeVehicleFactory.PrepareFamilyVisualReplacement(car,
                VehicleFamilyId.Drift);
            replacement.Commit();

            Assert.That(visual.PresentationPivot, Is.SameAs(pivot));
            Assert.That(car.GetComponentsInChildren<WheelCollider>(), Is.EqualTo(wheels));
            foreach (Renderer renderer in visual.SourceRenderers)
                Assert.That(renderer.transform.IsChildOf(pivot), Is.True);
            Object.DestroyImmediate(car);
        }

        [Test]
        public void ImpactPayloadPreservesDirectionalAndColliderContextWithoutPhysicsWrites()
        {
            GameObject source = new GameObject("ImpactSource");
            GameObject other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Rigidbody body = source.AddComponent<Rigidbody>();
            Collider collider = other.GetComponent<Collider>();
            VehicleImpactPayload payload = new(2, 9f, Vector3.one, Vector3.left, collider, other.layer, Vector3.forward);
            Assert.That(payload.PlayerIndex, Is.EqualTo(2));
            Assert.That(payload.Impulse, Is.EqualTo(9f));
            Assert.That(payload.Normal, Is.EqualTo(Vector3.left));
            Assert.That(payload.OtherCollider, Is.SameAs(collider));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(other);
        }

        [UnityTest]
        public IEnumerator PauseAndGridResetClearCameraCompositionWithoutWritingVehicleAuthority()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer player), Is.True);
            VehicleController vehicle = player.Vehicle;
            float throttleBefore = vehicle.ThrottleInput;
            player.ChaseCamera.SetPresentationResponse(Vector3.one, Vector3.up, 80f);
            roster.SetAllVehiclesEnabled(false);
            Assert.That(player.Camera.fieldOfView, Is.EqualTo(player.ChaseCamera.BaselineFieldOfView));
            Assert.That(vehicle.ThrottleInput, Is.EqualTo(throttleBefore));

            player.ChaseCamera.SetPresentationResponse(Vector3.one, Vector3.up, 80f);
            roster.ResetPlayersToGrid();
            yield return null;
            Assert.That(player.Camera.fieldOfView, Is.EqualTo(player.ChaseCamera.BaselineFieldOfView));
            Assert.That(vehicle.transform.position, Is.Not.EqualTo(Vector3.one), "Reset owns the grid pose, not presentation.");
        }
    }
}
