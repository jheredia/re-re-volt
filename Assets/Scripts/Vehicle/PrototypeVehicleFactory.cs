using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vehicle
{
    /// <summary>Minimal usable car for code-built scenes; authored prefabs can replace it.</summary>
    public static class PrototypeVehicleFactory
    {
        private static readonly Vector3 ChassisSize = new(1.1f, .4f, 1.7f);
        private static readonly Color DarkDetailColor = new(.035f, .045f, .055f);
        private static readonly Color GlassColor = new(.06f, .14f, .18f);

        public static GameObject Create(Pose pose)
        {
            return Create(pose, VehicleFamilyCatalog.DefaultId);
        }

        /// <summary>
        /// Builds one of the four visual families without changing the shared first-pass physics or handling setup.
        /// </summary>
        public static GameObject Create(Pose pose, VehicleFamilyId familyId)
        {
            if (!VehicleFamilyCatalog.IsDefined(familyId))
                throw new ArgumentOutOfRangeException(nameof(familyId), familyId, "Unknown vehicle family.");

            GameObject car = new GameObject("PrototypeVehicle");
            car.transform.SetPositionAndRotation(pose.position, pose.rotation);
            Rigidbody body = car.AddComponent<Rigidbody>();
            body.mass = 150f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            BoxCollider chassis = car.AddComponent<BoxCollider>();
            chassis.size = ChassisSize;

            List<Renderer> colorableBodyRenderers = new(8);
            CreateSilhouette(car.transform, familyId, colorableBodyRenderers);

            WheelCollider fl = CreateWheel(car.transform, "Wheel_FL", -.6f, .55f);
            WheelCollider fr = CreateWheel(car.transform, "Wheel_FR", .6f, .55f);
            WheelCollider rl = CreateWheel(car.transform, "Wheel_RL", -.6f, -.55f);
            WheelCollider rr = CreateWheel(car.transform, "Wheel_RR", .6f, -.55f);
            car.AddComponent<VehicleController>().ConfigureWheels(fl, fr, rl, rr);
            car.AddComponent<VehicleFamilyVisual>().Initialize(familyId, colorableBodyRenderers);
            return car;
        }

        private static void CreateSilhouette(Transform parent, VehicleFamilyId familyId,
            List<Renderer> colorableBodyRenderers)
        {
            switch (familyId)
            {
                case VehicleFamilyId.Touring:
                    CreateTouring(parent, colorableBodyRenderers);
                    return;
                case VehicleFamilyId.Buggy:
                    CreateBuggy(parent, colorableBodyRenderers);
                    return;
                case VehicleFamilyId.RcTruck:
                    CreateTruck(parent, colorableBodyRenderers);
                    return;
                case VehicleFamilyId.Drift:
                    CreateDrift(parent, colorableBodyRenderers);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(familyId), familyId, "Unknown vehicle family.");
            }
        }

        private static void CreateTouring(Transform parent, List<Renderer> bodyRenderers)
        {
            CreateBodyPart(parent, "Body", new Vector3(0f, 0f, 0f), new Vector3(1.1f, .28f, 1.7f), bodyRenderers);
            CreateBodyPart(parent, "Body_TouringCabin", new Vector3(0f, .24f, -.08f),
                new Vector3(.82f, .3f, .72f), bodyRenderers);
            CreateBodyPart(parent, "Body_TouringSpoiler", new Vector3(0f, .27f, -.72f),
                new Vector3(1f, .05f, .16f), bodyRenderers);
            CreateDetail(parent, "Detail_TouringWindshield", PrimitiveType.Cube, new Vector3(0f, .26f, .285f),
                new Vector3(.7f, .2f, .035f), new Vector3(-8f, 0f, 0f), GlassColor);
        }

        private static void CreateBuggy(Transform parent, List<Renderer> bodyRenderers)
        {
            CreateBodyPart(parent, "Body", new Vector3(0f, -.015f, 0f), new Vector3(.78f, .24f, 1.4f), bodyRenderers);
            CreateBodyPart(parent, "Body_BuggyNose", new Vector3(0f, .105f, .48f),
                new Vector3(.62f, .16f, .5f), bodyRenderers);
            CreateBodyPart(parent, "Body_BuggyCockpit", new Vector3(0f, .205f, -.12f),
                new Vector3(.58f, .22f, .5f), bodyRenderers);
            CreateBodyPart(parent, "Body_BuggyFenderFL", new Vector3(-.48f, .06f, .52f),
                new Vector3(.2f, .11f, .36f), bodyRenderers);
            CreateBodyPart(parent, "Body_BuggyFenderFR", new Vector3(.48f, .06f, .52f),
                new Vector3(.2f, .11f, .36f), bodyRenderers);
            CreateBodyPart(parent, "Body_BuggyFenderRL", new Vector3(-.48f, .06f, -.52f),
                new Vector3(.2f, .11f, .36f), bodyRenderers);
            CreateBodyPart(parent, "Body_BuggyFenderRR", new Vector3(.48f, .06f, -.52f),
                new Vector3(.2f, .11f, .36f), bodyRenderers);
            CreateDetail(parent, "Detail_BuggyRollBarLeft", PrimitiveType.Cube, new Vector3(-.27f, .36f, -.12f),
                new Vector3(.055f, .34f, .055f), new Vector3(0f, 0f, -16f), DarkDetailColor);
            CreateDetail(parent, "Detail_BuggyRollBarRight", PrimitiveType.Cube, new Vector3(.27f, .36f, -.12f),
                new Vector3(.055f, .34f, .055f), new Vector3(0f, 0f, 16f), DarkDetailColor);
        }

        private static void CreateTruck(Transform parent, List<Renderer> bodyRenderers)
        {
            CreateBodyPart(parent, "Body", new Vector3(0f, 0f, 0f), new Vector3(1.08f, .3f, 1.62f), bodyRenderers);
            CreateBodyPart(parent, "Body_TruckCab", new Vector3(0f, .28f, .36f),
                new Vector3(.94f, .43f, .66f), bodyRenderers);
            CreateBodyPart(parent, "Body_TruckBedLeft", new Vector3(-.45f, .23f, -.43f),
                new Vector3(.12f, .24f, .68f), bodyRenderers);
            CreateBodyPart(parent, "Body_TruckBedRight", new Vector3(.45f, .23f, -.43f),
                new Vector3(.12f, .24f, .68f), bodyRenderers);
            CreateBodyPart(parent, "Body_TruckTailgate", new Vector3(0f, .23f, -.72f),
                new Vector3(.9f, .24f, .1f), bodyRenderers);
            CreateDetail(parent, "Detail_TruckWindshield", PrimitiveType.Cube, new Vector3(0f, .33f, .68f),
                new Vector3(.76f, .25f, .035f), new Vector3(-5f, 0f, 0f), GlassColor);
            CreateDetail(parent, "Detail_TruckBed", PrimitiveType.Cube, new Vector3(0f, .16f, -.43f),
                new Vector3(.78f, .055f, .58f), Vector3.zero, DarkDetailColor);
        }

        private static void CreateDrift(Transform parent, List<Renderer> bodyRenderers)
        {
            CreateBodyPart(parent, "Body", new Vector3(0f, -.03f, 0f), new Vector3(1.08f, .22f, 1.72f), bodyRenderers);
            CreateBodyPart(parent, "Body_DriftCabin", new Vector3(0f, .175f, -.13f),
                new Vector3(.78f, .24f, .66f), bodyRenderers);
            CreateBodyPart(parent, "Body_DriftHood", new Vector3(0f, .115f, .52f),
                new Vector3(.98f, .1f, .58f), bodyRenderers);
            CreateBodyPart(parent, "Body_DriftWing", new Vector3(0f, .36f, -.72f),
                new Vector3(1.08f, .055f, .17f), bodyRenderers);
            CreateDetail(parent, "Detail_DriftWingLeft", PrimitiveType.Cube, new Vector3(-.38f, .25f, -.72f),
                new Vector3(.055f, .23f, .055f), Vector3.zero, DarkDetailColor);
            CreateDetail(parent, "Detail_DriftWingRight", PrimitiveType.Cube, new Vector3(.38f, .25f, -.72f),
                new Vector3(.055f, .23f, .055f), Vector3.zero, DarkDetailColor);
            CreateDetail(parent, "Detail_DriftWindshield", PrimitiveType.Cube, new Vector3(0f, .205f, .185f),
                new Vector3(.66f, .16f, .035f), new Vector3(-10f, 0f, 0f), GlassColor);
        }

        private static Renderer CreateBodyPart(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, List<Renderer> bodyRenderers)
        {
            Renderer renderer = CreatePart(parent, name, PrimitiveType.Cube, localPosition, localScale,
                Vector3.zero, null);
            bodyRenderers.Add(renderer);
            return renderer;
        }

        private static Renderer CreateDetail(Transform parent, string name, PrimitiveType primitive,
            Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Color color)
        {
            return CreatePart(parent, name, primitive, localPosition, localScale, localEulerAngles, color);
        }

        private static Renderer CreatePart(Transform parent, string name, PrimitiveType primitive,
            Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Color? color)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEulerAngles);
            part.transform.localScale = localScale;
            DisableAndRemovePrimitiveCollider(part);

            Renderer renderer = part.GetComponent<Renderer>();
            if (color.HasValue)
            {
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", color.Value);
                properties.SetColor("_Color", color.Value);
                renderer.SetPropertyBlock(properties);
            }
            return renderer;
        }

        private static WheelCollider CreateWheel(Transform parent, string name, float x, float z)
        {
            GameObject wheel = new GameObject(name);
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = new Vector3(x, -.1f, z);
            WheelCollider collider = wheel.AddComponent<WheelCollider>();
            collider.mass = 5f;
            collider.radius = .25f;
            collider.suspensionDistance = .2f;
            JointSpring suspension = collider.suspensionSpring;
            suspension.spring = 9000f;
            suspension.damper = 1000f;
            suspension.targetPosition = .5f;
            collider.suspensionSpring = suspension;

            CreateDetail(wheel.transform, "WheelVisual", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(.5f, .09f, .5f), new Vector3(0f, 0f, 90f), DarkDetailColor);
            return collider;
        }

        private static void DisableAndRemovePrimitiveCollider(GameObject primitive)
        {
            Collider primitiveCollider = primitive.GetComponent<Collider>();
            if (primitiveCollider == null) return;
            // Runtime removal is deferred, so disable immediately to keep all family collision footprints identical.
            primitiveCollider.enabled = false;
            if (Application.isPlaying) UnityEngine.Object.Destroy(primitiveCollider);
            else UnityEngine.Object.DestroyImmediate(primitiveCollider);
        }
    }
}
