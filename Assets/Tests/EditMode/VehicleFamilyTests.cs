using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vehicle;

namespace Tests.EditMode
{
    public sealed class VehicleFamilyTests
    {
        private readonly List<GameObject> created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void CatalogExposesExactlyFourFamiliesInStablePublicOrder()
        {
            Assert.That(VehicleFamilyCatalog.Count, Is.EqualTo(4));
            Assert.That(VehicleFamilyCatalog.DefaultId, Is.EqualTo(VehicleFamilyId.Touring));
            Assert.That(VehicleFamilyCatalog.Definitions, Has.Count.EqualTo(4));
            Assert.That(VehicleFamilyCatalog.Definitions[0].Id, Is.EqualTo(VehicleFamilyId.Touring));
            Assert.That(VehicleFamilyCatalog.Definitions[0].PublicLabel, Is.EqualTo("TOURING"));
            Assert.That(VehicleFamilyCatalog.Definitions[1].Id, Is.EqualTo(VehicleFamilyId.Buggy));
            Assert.That(VehicleFamilyCatalog.Definitions[1].PublicLabel, Is.EqualTo("BUGGY"));
            Assert.That(VehicleFamilyCatalog.Definitions[2].Id, Is.EqualTo(VehicleFamilyId.RcTruck));
            Assert.That(VehicleFamilyCatalog.Definitions[2].PublicLabel, Is.EqualTo("RC TRUCK"));
            Assert.That(VehicleFamilyCatalog.Definitions[3].Id, Is.EqualTo(VehicleFamilyId.Drift));
            Assert.That(VehicleFamilyCatalog.Definitions[3].PublicLabel, Is.EqualTo("DRIFT"));
        }

        [Test]
        public void WrappingIsDeterministicForPositiveNegativeAndUnknownInputs()
        {
            Assert.That(VehicleFamilyCatalog.Wrap(VehicleFamilyId.Touring, -1), Is.EqualTo(VehicleFamilyId.Drift));
            Assert.That(VehicleFamilyCatalog.Wrap(VehicleFamilyId.Drift, 1), Is.EqualTo(VehicleFamilyId.Touring));
            Assert.That(VehicleFamilyCatalog.Wrap(VehicleFamilyId.Buggy, 9), Is.EqualTo(VehicleFamilyId.RcTruck));
            Assert.That(VehicleFamilyCatalog.Wrap(VehicleFamilyId.Buggy, -10), Is.EqualTo(VehicleFamilyId.Drift));
            Assert.That(VehicleFamilyCatalog.Wrap((VehicleFamilyId)99, 1), Is.EqualTo(VehicleFamilyId.Buggy));
            Assert.That(VehicleFamilyCatalog.Wrap(VehicleFamilyId.Touring, int.MinValue),
                Is.EqualTo(VehicleFamilyId.Touring));
        }

        [Test]
        public void DefaultFactoryEntryCreatesTouringFamilyAtRequestedPose()
        {
            Pose pose = new Pose(new Vector3(4f, 3f, 2f), Quaternion.Euler(0f, 37f, 0f));
            GameObject vehicle = Track(PrototypeVehicleFactory.Create(pose));

            Assert.That(vehicle.transform.position, Is.EqualTo(pose.position));
            Assert.That(vehicle.transform.rotation, Is.EqualTo(pose.rotation));
            Assert.That(vehicle.GetComponent<VehicleFamilyVisual>().FamilyId, Is.EqualTo(VehicleFamilyId.Touring));
        }

        [Test]
        public void EveryFamilyHasDistinctTaggedBodyVisualsAndIdenticalCompetitiveSetup()
        {
            GameObject baseline = Track(PrototypeVehicleFactory.Create(default, VehicleFamilyId.Touring));
            Rigidbody baselineBody = baseline.GetComponent<Rigidbody>();
            BoxCollider baselineChassis = baseline.GetComponent<BoxCollider>();
            WheelCollider[] baselineWheels = GetOrderedWheels(baseline);

            foreach (VehicleFamilyDefinition definition in VehicleFamilyCatalog.Definitions)
            {
                GameObject vehicle = definition.Id == VehicleFamilyId.Touring
                    ? baseline
                    : Track(PrototypeVehicleFactory.Create(default, definition.Id));
                VehicleFamilyVisual visual = vehicle.GetComponent<VehicleFamilyVisual>();
                Renderer[] allRenderers = vehicle.GetComponentsInChildren<Renderer>(true);
                Rigidbody rigidbody = vehicle.GetComponent<Rigidbody>();
                BoxCollider chassis = vehicle.GetComponent<BoxCollider>();
                WheelCollider[] wheels = GetOrderedWheels(vehicle);

                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.FamilyId, Is.EqualTo(definition.Id));
                Assert.That(visual.ColorableBodyRenderers.Count, Is.GreaterThanOrEqualTo(2));
                Assert.That(vehicle.transform.Find("Body"), Is.Not.Null);
                foreach (Renderer colorable in visual.ColorableBodyRenderers)
                {
                    Assert.That(colorable.name, Does.StartWith("Body"));
                    CollectionAssert.Contains(allRenderers, colorable);
                    Assert.That(colorable, Is.TypeOf<MeshRenderer>());
                }

                Assert.That(vehicle.GetComponent<VehicleController>().IsConfigured, Is.True);
                Assert.That(rigidbody.mass, Is.EqualTo(baselineBody.mass));
                Assert.That(rigidbody.interpolation, Is.EqualTo(baselineBody.interpolation));
                Assert.That(rigidbody.collisionDetectionMode, Is.EqualTo(baselineBody.collisionDetectionMode));
                Assert.That(chassis.size, Is.EqualTo(baselineChassis.size));
                Assert.That(wheels, Has.Length.EqualTo(4));
                for (int i = 0; i < wheels.Length; i++)
                {
                    Assert.That(wheels[i].transform.localPosition,
                        Is.EqualTo(baselineWheels[i].transform.localPosition));
                    Assert.That(wheels[i].radius, Is.EqualTo(baselineWheels[i].radius));
                    Assert.That(wheels[i].mass, Is.EqualTo(baselineWheels[i].mass));
                    Assert.That(wheels[i].suspensionDistance, Is.EqualTo(baselineWheels[i].suspensionDistance));
                    Assert.That(wheels[i].suspensionSpring.spring,
                        Is.EqualTo(baselineWheels[i].suspensionSpring.spring));
                    Assert.That(wheels[i].suspensionSpring.damper,
                        Is.EqualTo(baselineWheels[i].suspensionSpring.damper));
                }

                Collider[] colliders = vehicle.GetComponentsInChildren<Collider>(true);
                Assert.That(colliders, Has.Length.EqualTo(5),
                    "Generated visual primitives must not alter the shared collision footprint.");
                foreach (Renderer renderer in allRenderers)
                    Assert.That(renderer, Is.TypeOf<MeshRenderer>(),
                        "Every prototype renderer must remain eligible for current visual-style discovery.");
            }

            Assert.That(baseline.transform.Find("Body_TouringCabin"), Is.Not.Null);
            Assert.That(FindFamily(VehicleFamilyId.Buggy).transform.Find("Body_BuggyCockpit"), Is.Not.Null);
            Assert.That(FindFamily(VehicleFamilyId.RcTruck).transform.Find("Body_TruckCab"), Is.Not.Null);
            Assert.That(FindFamily(VehicleFamilyId.Drift).transform.Find("Body_DriftWing"), Is.Not.Null);
        }

        [Test]
        public void FactoryRejectsUnknownFamilyBeforeCreatingAPlayerObject()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                PrototypeVehicleFactory.Create(default, (VehicleFamilyId)99));
        }

        private GameObject Track(GameObject gameObject)
        {
            created.Add(gameObject);
            return gameObject;
        }

        private GameObject FindFamily(VehicleFamilyId familyId)
        {
            return created.Find(gameObject =>
                gameObject != null && gameObject.GetComponent<VehicleFamilyVisual>()?.FamilyId == familyId);
        }

        private static WheelCollider[] GetOrderedWheels(GameObject vehicle)
        {
            return new[]
            {
                vehicle.transform.Find("Wheel_FL").GetComponent<WheelCollider>(),
                vehicle.transform.Find("Wheel_FR").GetComponent<WheelCollider>(),
                vehicle.transform.Find("Wheel_RL").GetComponent<WheelCollider>(),
                vehicle.transform.Find("Wheel_RR").GetComponent<WheelCollider>()
            };
        }
    }
}
