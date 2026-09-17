using UnityEngine;

namespace Vehicle
{
    /// <summary>Minimal usable car for code-built scenes; authored prefabs can replace it.</summary>
    public static class PrototypeVehicleFactory
    {
        public static GameObject Create(Pose pose)
        {
            GameObject car = new GameObject("PrototypeVehicle");
            car.transform.SetPositionAndRotation(pose.position, pose.rotation);
            Rigidbody body = car.AddComponent<Rigidbody>();
            body.mass = 150f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            BoxCollider chassis = car.AddComponent<BoxCollider>();
            chassis.size = new Vector3(1.1f, .4f, 1.7f);
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shell.name = "Body";
            shell.transform.SetParent(car.transform, false);
            shell.transform.localScale = chassis.size;
            Object.Destroy(shell.GetComponent<Collider>());

            WheelCollider fl = CreateWheel(car.transform, "Wheel_FL", -.6f, .55f);
            WheelCollider fr = CreateWheel(car.transform, "Wheel_FR", .6f, .55f);
            WheelCollider rl = CreateWheel(car.transform, "Wheel_RL", -.6f, -.55f);
            WheelCollider rr = CreateWheel(car.transform, "Wheel_RR", .6f, -.55f);
            car.AddComponent<VehicleController>().ConfigureWheels(fl, fr, rl, rr);
            return car;
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
            return collider;
        }
    }
}
