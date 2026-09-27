namespace Vehicle
{
    /// <summary>Neutral public identifiers used by the Stage 5.1 blind handling comparison.</summary>
    public enum HandlingModelId
    {
        A = 0,
        B = 1,
        C = 2,
        D = 3
    }

    public static class HandlingModelCatalog
    {
        public static bool IsDefined(HandlingModelId model)
        {
            int value = (int)model;
            return value >= (int)HandlingModelId.A && value <= (int)HandlingModelId.D;
        }

        // Candidate agents change only their own entry after their implementation is complete.
        public static bool IsImplemented(HandlingModelId model) =>
            model == HandlingModelId.A || model == HandlingModelId.B;

        public static HandlingModelId ResolveImplementedModel(HandlingModelId selected) =>
            IsDefined(selected) && IsImplemented(selected) ? selected : HandlingModelId.A;

        public static string GetPublicLabel(HandlingModelId model) => IsDefined(model) ? $"MODEL {model}" : "MODEL ?";
    }

    /// <summary>One runtime dispatch point; candidate implementations remain outside input, combat and presentation.</summary>
    internal interface IVehicleHandlingModelRuntime
    {
        HandlingModelId Id { get; }
        void FixedStep(VehicleController vehicle);
        void OnCombatModifiersChanged(VehicleController vehicle);
        void Reset(VehicleController vehicle);
    }

    internal static class VehicleHandlingModelFactory
    {
        public static IVehicleHandlingModelRuntime Create(HandlingModelId selected)
        {
            // C-D deliberately fall back to the accepted baseline until each bounded candidate is delivered.
            switch (HandlingModelCatalog.ResolveImplementedModel(selected))
            {
                case HandlingModelId.B:
                    return new DirectionAwareWheelColliderHandlingModelRuntime();
                default:
                    return BaselineHandlingModelRuntime.Instance;
            }
        }
    }

    internal sealed class BaselineHandlingModelRuntime : IVehicleHandlingModelRuntime
    {
        public static readonly BaselineHandlingModelRuntime Instance = new BaselineHandlingModelRuntime();

        private BaselineHandlingModelRuntime() { }

        public HandlingModelId Id => HandlingModelId.A;
        public void FixedStep(VehicleController vehicle) => vehicle.FixedStepBaselineHandling();
        public void OnCombatModifiersChanged(VehicleController vehicle) => vehicle.ApplyBaselineCombatGrip();
        public void Reset(VehicleController vehicle) { }
    }

    /// <summary>Allocation-free drive result for the direction-aware WheelCollider candidate.</summary>
    public readonly struct DirectionAwareDriveDecision
    {
        public DirectionAwareDriveDecision(float motorInput, float brakeFraction, bool isOpposingMotion)
        {
            MotorInput = motorInput;
            BrakeFraction = brakeFraction;
            IsOpposingMotion = isOpposingMotion;
        }

        /// <summary>Signed fraction of the vehicle's authored motor torque.</summary>
        public float MotorInput { get; }
        /// <summary>Fraction of the vehicle's authored maximum brake torque.</summary>
        public float BrakeFraction { get; }
        public bool IsOpposingMotion { get; }
    }

    /// <summary>
    /// Candidate B's complete tuning surface and deterministic decisions. It intentionally changes only
    /// WheelCollider torque, brake torque and front-wheel steering; it never rotates or yaws the chassis.
    /// </summary>
    public static class DirectionAwareWheelColliderModel
    {
        public const float ThrottleDeadZone = .1f;
        public const float DirectionSwitchSpeed = .75f;
        public const float MinimumOpposeMotionBrakeFraction = .65f;
        public const float HighSpeedSteeringMultiplier = .55f;
        public const float ReverseSteeringMultiplier = .72f;
        public const float SteeringResponsePerSecond = 7f;

        /// <summary>
        /// Opposite-direction input first produces symmetric braking and zero motor torque. Below the switch
        /// speed, requested torque takes over immediately so a WheelCollider cannot remain held at neutral.
        /// </summary>
        public static DirectionAwareDriveDecision EvaluateDrive(float throttleInput, float manualBrakeInput,
            float signedLongitudinalSpeed, float totalSpeed, float maximumSpeed)
        {
            float throttle = UnityEngine.Mathf.Clamp(throttleInput, -1f, 1f);
            float manualBrake = UnityEngine.Mathf.Clamp01(manualBrakeInput);
            LongitudinalDirection requested = HandlingMetrics.ClassifyDirection(throttle, ThrottleDeadZone);
            LongitudinalDirection travelling = HandlingMetrics.ClassifyDirection(signedLongitudinalSpeed,
                DirectionSwitchSpeed);
            bool opposingMotion = requested != LongitudinalDirection.Stationary &&
                                  travelling != LongitudinalDirection.Stationary && requested != travelling;

            if (opposingMotion)
            {
                float opposeBrake = UnityEngine.Mathf.Lerp(MinimumOpposeMotionBrakeFraction, 1f,
                    UnityEngine.Mathf.Abs(throttle));
                return new DirectionAwareDriveDecision(0f, UnityEngine.Mathf.Max(manualBrake, opposeBrake), true);
            }

            if (requested == LongitudinalDirection.Stationary)
                return new DirectionAwareDriveDecision(0f, manualBrake, false);

            float motorInput = totalSpeed < UnityEngine.Mathf.Max(0f, maximumSpeed) ? throttle : 0f;
            return new DirectionAwareDriveDecision(motorInput, manualBrake, false);
        }

        /// <summary>
        /// Keeps full low-speed forward steering, progressively trims it as speed rises, and applies an
        /// additional reverse cap. At equal speed reverse can never request more angle than forward.
        /// </summary>
        public static float CalculateTargetSteerAngle(float steerInput, float throttleInput,
            float signedLongitudinalSpeed, float maximumSpeed, float maximumSteerAngle,
            float steeringMultiplier)
        {
            float normalizedSpeed = UnityEngine.Mathf.Clamp01(UnityEngine.Mathf.Abs(signedLongitudinalSpeed) /
                UnityEngine.Mathf.Max(.01f, maximumSpeed));
            float smoothSpeed = normalizedSpeed * normalizedSpeed * (3f - 2f * normalizedSpeed);
            float speedMultiplier = UnityEngine.Mathf.Lerp(1f, HighSpeedSteeringMultiplier, smoothSpeed);
            bool reversing = signedLongitudinalSpeed < -DirectionSwitchSpeed ||
                             UnityEngine.Mathf.Abs(signedLongitudinalSpeed) <= DirectionSwitchSpeed &&
                             throttleInput < -ThrottleDeadZone;
            float directionMultiplier = reversing ? ReverseSteeringMultiplier : 1f;
            return UnityEngine.Mathf.Clamp(steerInput, -1f, 1f) * maximumSteerAngle *
                   UnityEngine.Mathf.Clamp01(steeringMultiplier) * speedMultiplier * directionMultiplier;
        }

        public static float MoveSteerTowards(float currentAngle, float targetAngle, float maximumSteerAngle,
            float deltaSeconds)
        {
            float maximumDelta = UnityEngine.Mathf.Max(0f, maximumSteerAngle) * SteeringResponsePerSecond *
                                 UnityEngine.Mathf.Max(0f, deltaSeconds);
            return UnityEngine.Mathf.MoveTowards(currentAngle, targetAngle, maximumDelta);
        }
    }

    internal sealed class DirectionAwareWheelColliderHandlingModelRuntime : IVehicleHandlingModelRuntime
    {
        private float currentSteerAngle;

        public HandlingModelId Id => HandlingModelId.B;

        public void FixedStep(VehicleController vehicle)
        {
            float signedLongitudinalSpeed = vehicle.SignedLongitudinalSpeed;
            DirectionAwareDriveDecision drive = DirectionAwareWheelColliderModel.EvaluateDrive(
                vehicle.ThrottleInput, vehicle.BrakeInput, signedLongitudinalSpeed, vehicle.SpeedMetersPerSecond,
                vehicle.MaximumSpeedMetersPerSecond);
            float targetSteerAngle = DirectionAwareWheelColliderModel.CalculateTargetSteerAngle(
                vehicle.SteerInput, vehicle.ThrottleInput, signedLongitudinalSpeed,
                vehicle.MaximumSpeedMetersPerSecond, vehicle.MaximumSteerAngle, vehicle.CombatSteeringMultiplier);
            currentSteerAngle = DirectionAwareWheelColliderModel.MoveSteerTowards(currentSteerAngle,
                targetSteerAngle, vehicle.MaximumSteerAngle, UnityEngine.Time.fixedDeltaTime);
            vehicle.FixedStepDirectionAwareWheelHandling(currentSteerAngle, drive.MotorInput, drive.BrakeFraction);
        }

        public void OnCombatModifiersChanged(VehicleController vehicle) => vehicle.ApplyBaselineCombatGrip();

        public void Reset(VehicleController vehicle)
        {
            currentSteerAngle = 0f;
            vehicle.ResetDirectionAwareWheelState();
        }
    }
}
