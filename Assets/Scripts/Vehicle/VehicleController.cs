using UnityEngine;

namespace Vehicle
{
    
    /// <summary>
    /// Arcade RC-car driving controller using Unity WheelColliders.
    /// Handles engine torque, braking, steering, drifting and surface friction.
    /// Attach to a vehicle Rigidbody. Requries 4 WheelColliders as children, named "Wheel_FL", "Wheel_FR", "Wheel_RL" and "Wheel_RR".
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        #region Wheel References
        // Wheel Colliders
        [Header("Wheel Colliders")]
        [SerializeField] private WheelCollider wheelFL;
        [SerializeField] private WheelCollider wheelFR;
        [SerializeField] private WheelCollider wheelRL;
        [SerializeField] private WheelCollider wheelRR;

        // Wheel Meshes        
        [Header("Wheel Meshes")] 
        [SerializeField] private Transform meshFL;
        [SerializeField] private Transform meshFR;
        [SerializeField] private Transform meshRL;
        [SerializeField] private Transform meshRR;

        #endregion

        #region Engine
        [Header("Engine")]
        [SerializeField] private float maxMotorTorque = 800f; // Maximum torque the engine can apply to the wheels
        [SerializeField] private float maxBrakeTorque = 2000f; // Maximum torque the brakes can apply to the wheels
        [SerializeField] private float maxSpeed = 25f; // Maximum speed in m/s (90 km/h)

        #endregion
       
        #region Steering
        [Header("Steering")]
        [SerializeField] private float maxSteerAngle = 35f; // Maximum steering angle in degrees
        [SerializeField] private float steerSpeed = 5f; // Speed at which the wheels steer towards the target angle, the smoothing rate
        #endregion

        #region Drifting
        [Header("Drift")]
        [SerializeField] private float normalRearGrip = 1.5f; // Sideways stiffness
        [SerializeField] private float driftRearGrip = 0.4f; // Reduced when drifting
        [SerializeField] private float driftThreshold = 0.8f; // Brake input threshold to enter drift
        #endregion

        #region Downforce
        [Header("Stability")]
        [SerializeField] private float downforce = 50f; // Downforce applied to the car
        #endregion

        #region Centre of Mass
        [Header("Center of Mass")]
        [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0, -0.2f, 0); // Lowering the center of mass for better stability
        #endregion

        #region Runtime State
        private Rigidbody vehicleRigidBody;
        private float currentSteerAngle;
        private bool inputEnabled = true; // Can be used to disable input when the car is flipped or during cutscenes

        // Raw inputs (set by InputHandler or AIDriverController)
        [HideInInspector] public float ThrottleInput; // -1 to 1
        [HideInInspector] public float BrakeInput; // 0 to 1
        [HideInInspector] public float SteerInput; // -1 to 1

        // Computed
        public float CurrentSpeed => vehicleRigidBody != null ? vehicleRigidBody.linearVelocity.magnitude * 3.6f: 0f; // Current speed in m/s
        public bool IsDrifting { get; private set; } // True when the car is drifting, determined by brake input and speed
        public bool IsConfigured => wheelFL != null && wheelFR != null && wheelRL != null && wheelRR != null;
        #endregion


        private void Awake()
        {
            TryGetComponent<Rigidbody>(out vehicleRigidBody);
            if (vehicleRigidBody == null)            {
                Debug.LogError("VehicleController requires a Rigidbody component.");
                enabled = false;
                return;
            }

            // Set the center of mass for better stability
            vehicleRigidBody.centerOfMass = centerOfMassOffset;
        }

        void FixedUpdate()
        {
            if (!IsConfigured) return;
            if (!inputEnabled)
            {
                wheelRL.motorTorque = wheelRR.motorTorque = 0f;
                ApplyBrake(maxBrakeTorque); // Apply brakes to stop the car if input is disabled
                return;
            }

            HandleSteering();
            HandleMotor();
            HandleDrift();
            ApplyDownforce();
            UpdateWheelMeshes();
        }

        // Input Enable/Disable

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled)
            {
                ThrottleInput = 0f;
                SteerInput = 0f;
                BrakeInput = 0f;
                IsDrifting = false;
                if (IsConfigured)
                {
                    wheelRL.motorTorque = wheelRR.motorTorque = 0f;
                    ApplyBrake(maxBrakeTorque);
                }
            }
        }

        public void ConfigureWheels(WheelCollider frontLeft, WheelCollider frontRight, WheelCollider rearLeft, WheelCollider rearRight)
        {
            wheelFL = frontLeft;
            wheelFR = frontRight;
            wheelRL = rearLeft;
            wheelRR = rearRight;
        }

        #region Motor

        private void HandleMotor()
        {
            float currentSpeed = vehicleRigidBody.linearVelocity.magnitude;
            float torque = 0f;

            if (currentSpeed < maxSpeed)
            {
                torque = ThrottleInput * maxMotorTorque;
            }

            // Rear wheel drive
            wheelRL.motorTorque = torque;
            wheelRR.motorTorque = torque;

            // Braking
            float brake = BrakeInput * maxBrakeTorque;
            ApplyBrake(brake);
        }

        private void ApplyBrake(float brakeTorque)
        {
            wheelFL.brakeTorque = brakeTorque;
            wheelFR.brakeTorque = brakeTorque;
            wheelRL.brakeTorque = brakeTorque;
            wheelRR.brakeTorque = brakeTorque;
        }
        #endregion

        #region Steering
        private void HandleSteering()
        {
            float targetAngle = SteerInput * maxSteerAngle;
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, steerSpeed * Time.fixedDeltaTime);
        
            wheelFL.steerAngle = currentSteerAngle;
            wheelFR.steerAngle = currentSteerAngle;
        }

        #endregion

        #region Drifting
        private void HandleDrift()
        {
            IsDrifting = BrakeInput > driftThreshold && CurrentSpeed > 2f; // Simple condition to enter drift

            float rearGrip = IsDrifting ? driftRearGrip : normalRearGrip;

            SetWheelSidewaysStiffness(wheelRL, rearGrip);
            SetWheelSidewaysStiffness(wheelRR, rearGrip);
        }


        private void SetWheelSidewaysStiffness(WheelCollider wheel, float stiffness)
        {
            WheelFrictionCurve curve = wheel.sidewaysFriction;
            curve.stiffness = stiffness;
            wheel.sidewaysFriction = curve;
        }
        #endregion

        #region Downforce
        private void ApplyDownforce()
        {
            vehicleRigidBody.AddForce(-transform.up * downforce * vehicleRigidBody.linearVelocity.magnitude);
        }

        #endregion

        #region Wheel Meshes
        private void UpdateWheelMeshes()
        {
            SyncMesh(wheelFL, meshFL);
            SyncMesh(wheelFR, meshFR);
            SyncMesh(wheelRL, meshRL);
            SyncMesh(wheelRR, meshRR);
        }

        private void SyncMesh(WheelCollider collider, Transform mesh)
        {
            if (collider == null || mesh == null) return;
            collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            mesh.SetPositionAndRotation(pos, rot);
        }

        #endregion

        #region Vehicle Reset
        public void ResetVehicle()
        {
            if (TryGetComponent(out VehicleRecovery recovery)) recovery.TryRecover();
        }

        public void Teleport(Pose pose)
        {
            vehicleRigidBody.linearVelocity = Vector3.zero;
            vehicleRigidBody.angularVelocity = Vector3.zero;
            vehicleRigidBody.position = pose.position;
            vehicleRigidBody.rotation = pose.rotation;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            ThrottleInput = SteerInput = BrakeInput = currentSteerAngle = 0f;
            IsDrifting = false;
            if (IsConfigured)
            {
                wheelRL.motorTorque = wheelRR.motorTorque = 0f;
                wheelFL.steerAngle = wheelFR.steerAngle = 0f;
                ApplyBrake(0f);
            }
        }
        #endregion
    }
}
