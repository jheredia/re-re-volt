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
            model == HandlingModelId.A || model == HandlingModelId.B || model == HandlingModelId.C ||
            model == HandlingModelId.D;

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
            switch (HandlingModelCatalog.ResolveImplementedModel(selected))
            {
                case HandlingModelId.B:
                    return new DirectionAwareWheelColliderHandlingModelRuntime();
                case HandlingModelId.C:
                    return new BalancedArcadeYawHandlingModelRuntime();
                case HandlingModelId.D:
                    return new DeliberateDriftHandlingModelRuntime();
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

    /// <summary>Allocation-free yaw-assist result for the balanced arcade candidate.</summary>
    public readonly struct BalancedArcadeYawDecision
    {
        public BalancedArcadeYawDecision(float targetYawRateDegreesPerSecond,
            float yawAccelerationDegreesPerSecondSquared, bool shouldApply)
        {
            TargetYawRateDegreesPerSecond = targetYawRateDegreesPerSecond;
            YawAccelerationDegreesPerSecondSquared = yawAccelerationDegreesPerSecondSquared;
            ShouldApply = shouldApply;
        }

        public float TargetYawRateDegreesPerSecond { get; }
        public float YawAccelerationDegreesPerSecondSquared { get; }
        public bool ShouldApply { get; }
    }

    /// <summary>
    /// Candidate C's complete tuning surface and deterministic target-yaw calculation. WheelColliders remain
    /// authoritative for suspension, grip and collisions; this model adds only grounded, bounded yaw torque.
    /// </summary>
    public static class BalancedArcadeYawModel
    {
        public const int MinimumGroundedWheels = 2;
        public const float SteeringDeadZone = .05f;
        public const float MinimumAssistSpeed = 1.5f;
        public const float FullAssistSpeed = 6f;
        public const float HighSpeedAttenuationStart = .55f;
        public const float HighSpeedYawMultiplier = .58f;
        public const float MaximumTargetYawRateDegreesPerSecond = 120f;
        public const float ReverseYawMultiplier = .45f;
        public const float DriftYawMultiplier = .75f;
        public const float YawErrorGainPerSecond = 7f;
        public const float MaximumYawAccelerationDegreesPerSecondSquared = 320f;

        /// <summary>
        /// Model C shares Model B's symmetric oppose-motion braking. Its difference is the bounded yaw-rate
        /// assistance, not a second acceleration model.
        /// </summary>
        public static DirectionAwareDriveDecision EvaluateDrive(float throttleInput, float manualBrakeInput,
            float signedLongitudinalSpeed, float totalSpeed, float maximumSpeed) =>
            DirectionAwareWheelColliderModel.EvaluateDrive(throttleInput, manualBrakeInput,
                signedLongitudinalSpeed, totalSpeed, maximumSpeed);

        /// <summary>
        /// Produces a speed-aware desired yaw rate and a bounded acceleration toward it. Reverse authority is
        /// intentionally below forward authority, high-speed authority attenuates smoothly, and fewer than two
        /// grounded wheels disables the assist so jumps and rolls remain governed by Rigidbody physics.
        /// </summary>
        public static BalancedArcadeYawDecision EvaluateYawAssist(float steerInput,
            float signedLongitudinalSpeed, float maximumSpeed, float currentYawRateDegreesPerSecond,
            int groundedWheelCount, bool isDrifting, float steeringMultiplier)
        {
            float steering = UnityEngine.Mathf.Clamp(steerInput, -1f, 1f);
            float effectiveSteeringMultiplier = UnityEngine.Mathf.Clamp01(steeringMultiplier);
            float absoluteSpeed = UnityEngine.Mathf.Abs(signedLongitudinalSpeed);
            if (groundedWheelCount < MinimumGroundedWheels ||
                UnityEngine.Mathf.Abs(steering) <= SteeringDeadZone ||
                effectiveSteeringMultiplier <= 0f || absoluteSpeed < MinimumAssistSpeed || maximumSpeed <= 0f)
                return default;

            float activation = UnityEngine.Mathf.InverseLerp(MinimumAssistSpeed, FullAssistSpeed, absoluteSpeed);
            activation = SmoothStep01(activation);

            float normalizedSpeed = UnityEngine.Mathf.Clamp01(absoluteSpeed / maximumSpeed);
            float highSpeedBlend = UnityEngine.Mathf.InverseLerp(HighSpeedAttenuationStart, 1f, normalizedSpeed);
            highSpeedBlend = SmoothStep01(highSpeedBlend);
            float speedMultiplier = UnityEngine.Mathf.Lerp(1f, HighSpeedYawMultiplier, highSpeedBlend);

            bool reversing = signedLongitudinalSpeed < -MinimumAssistSpeed;
            float directionSign = reversing ? -1f : 1f;
            float directionMultiplier = reversing ? ReverseYawMultiplier : 1f;
            float driftMultiplier = isDrifting ? DriftYawMultiplier : 1f;
            float targetYawRate = steering * MaximumTargetYawRateDegreesPerSecond * activation *
                                  speedMultiplier * directionSign * directionMultiplier * driftMultiplier *
                                  effectiveSteeringMultiplier;

            float yawError = targetYawRate - currentYawRateDegreesPerSecond;
            float yawAcceleration = UnityEngine.Mathf.Clamp(yawError * YawErrorGainPerSecond,
                -MaximumYawAccelerationDegreesPerSecondSquared,
                MaximumYawAccelerationDegreesPerSecondSquared);
            return new BalancedArcadeYawDecision(targetYawRate, yawAcceleration, true);
        }

        private static float SmoothStep01(float value) => value * value * (3f - 2f * value);
    }

    internal sealed class BalancedArcadeYawHandlingModelRuntime : IVehicleHandlingModelRuntime
    {
        private float currentSteerAngle;

        public HandlingModelId Id => HandlingModelId.C;

        public void FixedStep(VehicleController vehicle)
        {
            float signedLongitudinalSpeed = vehicle.SignedLongitudinalSpeed;
            DirectionAwareDriveDecision drive = BalancedArcadeYawModel.EvaluateDrive(
                vehicle.ThrottleInput, vehicle.BrakeInput, signedLongitudinalSpeed,
                vehicle.SpeedMetersPerSecond, vehicle.MaximumSpeedMetersPerSecond);
            float targetSteerAngle = DirectionAwareWheelColliderModel.CalculateTargetSteerAngle(
                vehicle.SteerInput, vehicle.ThrottleInput, signedLongitudinalSpeed,
                vehicle.MaximumSpeedMetersPerSecond, vehicle.MaximumSteerAngle,
                vehicle.CombatSteeringMultiplier);
            currentSteerAngle = DirectionAwareWheelColliderModel.MoveSteerTowards(currentSteerAngle,
                targetSteerAngle, vehicle.MaximumSteerAngle, UnityEngine.Time.fixedDeltaTime);
            vehicle.FixedStepBalancedArcadeYawHandling(currentSteerAngle, drive.MotorInput,
                drive.BrakeFraction);
        }

        public void OnCombatModifiersChanged(VehicleController vehicle) => vehicle.ApplyBaselineCombatGrip();

        public void Reset(VehicleController vehicle)
        {
            currentSteerAngle = 0f;
            vehicle.ResetDirectionAwareWheelState();
        }
    }

    public enum DeliberateDriftPhase
    {
        Grip = 0,
        Drift = 1,
        Recovery = 2
    }

    /// <summary>Small, allocation-free state carried by each Model D runtime.</summary>
    public readonly struct DeliberateDriftState
    {
        public DeliberateDriftState(DeliberateDriftPhase phase, float recoverySeconds)
        {
            Phase = phase;
            RecoverySeconds = UnityEngine.Mathf.Max(0f, recoverySeconds);
        }

        public DeliberateDriftPhase Phase { get; }
        public float RecoverySeconds { get; }
        public bool IsDrifting => Phase == DeliberateDriftPhase.Drift;
    }

    /// <summary>Bounded Rigidbody and WheelCollider requests produced by Model D for one physics step.</summary>
    public readonly struct DeliberateDriftDecision
    {
        public DeliberateDriftDecision(DeliberateDriftState state, float rearGripMultiplier,
            float wheelBrakeMultiplier, float targetYawRateDegreesPerSecond,
            float yawAccelerationDegreesPerSecondSquared, float lateralAccelerationMetersPerSecondSquared,
            bool shouldApplyAssistance)
        {
            State = state;
            RearGripMultiplier = rearGripMultiplier;
            WheelBrakeMultiplier = wheelBrakeMultiplier;
            TargetYawRateDegreesPerSecond = targetYawRateDegreesPerSecond;
            YawAccelerationDegreesPerSecondSquared = yawAccelerationDegreesPerSecondSquared;
            LateralAccelerationMetersPerSecondSquared = lateralAccelerationMetersPerSecondSquared;
            ShouldApplyAssistance = shouldApplyAssistance;
        }

        public DeliberateDriftState State { get; }
        public bool IsDrifting => State.IsDrifting;
        public float RearGripMultiplier { get; }
        public float WheelBrakeMultiplier { get; }
        public float TargetYawRateDegreesPerSecond { get; }
        public float YawAccelerationDegreesPerSecondSquared { get; }
        public float LateralAccelerationMetersPerSecondSquared { get; }
        public bool ShouldApplyAssistance { get; }
    }

    /// <summary>
    /// Candidate D's complete tuning surface and deterministic grip/drift/recovery state machine. A drift can
    /// only start from forward, grounded motion with deliberate brake and steering input. Lower hold thresholds
    /// provide hysteresis; losing them enters a timed velocity-alignment recovery rather than abruptly restoring
    /// full grip. Reverse and airborne motion never receive expressive drift assistance.
    /// </summary>
    public static class DeliberateDriftModel
    {
        public const int MinimumGroundedWheels = 2;
        public const float EntryBrakeInput = .72f;
        public const float HoldBrakeInput = .35f;
        public const float EntrySteeringInput = .32f;
        public const float HoldSteeringInput = .12f;
        public const float EntryForwardSpeed = 5f;
        public const float HoldForwardSpeed = 3f;
        public const float MinimumRecoverySeconds = .25f;
        public const float MaximumRecoverySeconds = .8f;
        public const float RecoveryLateralSpeed = .5f;
        public const float DriftRearGripMultiplier = .45f;
        public const float RecoveryRearGripMultiplier = .78f;
        public const float DriftWheelBrakeMultiplier = .2f;
        public const float MaximumTargetYawRateDegreesPerSecond = 105f;
        public const float YawErrorGainPerSecond = 6f;
        public const float MaximumYawAccelerationDegreesPerSecondSquared = 260f;
        public const float DriftLateralVelocityRatio = .28f;
        public const float DriftAlignmentGainPerSecond = 3.5f;
        public const float RecoveryAlignmentGainPerSecond = 5.5f;
        public const float MaximumDriftLateralAcceleration = 7f;
        public const float MaximumRecoveryLateralAcceleration = 12f;

        public static DeliberateDriftState InitialState =>
            new DeliberateDriftState(DeliberateDriftPhase.Grip, 0f);

        /// <summary>Model D deliberately shares Model B's prompt, symmetric direction-change drive rules.</summary>
        public static DirectionAwareDriveDecision EvaluateDrive(float throttleInput, float manualBrakeInput,
            float signedLongitudinalSpeed, float totalSpeed, float maximumSpeed) =>
            DirectionAwareWheelColliderModel.EvaluateDrive(throttleInput, manualBrakeInput,
                signedLongitudinalSpeed, totalSpeed, maximumSpeed);

        public static DeliberateDriftDecision Evaluate(DeliberateDriftState previousState, float brakeInput,
            float steerInput, float signedLongitudinalSpeed, float lateralSpeed,
            float currentYawRateDegreesPerSecond, int groundedWheelCount, float steeringMultiplier,
            float gripMultiplier, float deltaSeconds)
        {
            float brake = UnityEngine.Mathf.Clamp01(brakeInput);
            float steering = UnityEngine.Mathf.Clamp(steerInput, -1f, 1f);
            float absoluteSteering = UnityEngine.Mathf.Abs(steering);
            float stepSeconds = UnityEngine.Mathf.Max(0f, deltaSeconds);
            bool grounded = groundedWheelCount >= MinimumGroundedWheels;

            DeliberateDriftState nextState = previousState;
            switch (previousState.Phase)
            {
                case DeliberateDriftPhase.Drift:
                    bool canHold = grounded && signedLongitudinalSpeed >= HoldForwardSpeed &&
                                   brake >= HoldBrakeInput && absoluteSteering >= HoldSteeringInput;
                    nextState = canHold
                        ? new DeliberateDriftState(DeliberateDriftPhase.Drift, 0f)
                        : new DeliberateDriftState(DeliberateDriftPhase.Recovery, 0f);
                    break;
                case DeliberateDriftPhase.Recovery:
                    float recoverySeconds = UnityEngine.Mathf.Min(MaximumRecoverySeconds,
                        previousState.RecoverySeconds + stepSeconds);
                    bool recovered = recoverySeconds >= MaximumRecoverySeconds ||
                                     recoverySeconds >= MinimumRecoverySeconds &&
                                     UnityEngine.Mathf.Abs(lateralSpeed) <= RecoveryLateralSpeed;
                    nextState = recovered
                        ? InitialState
                        : new DeliberateDriftState(DeliberateDriftPhase.Recovery, recoverySeconds);
                    break;
                default:
                    bool canEnter = grounded && signedLongitudinalSpeed >= EntryForwardSpeed &&
                                    brake >= EntryBrakeInput && absoluteSteering >= EntrySteeringInput;
                    nextState = canEnter
                        ? new DeliberateDriftState(DeliberateDriftPhase.Drift, 0f)
                        : InitialState;
                    break;
            }

            float rearGripMultiplier = 1f;
            float wheelBrakeMultiplier = 1f;
            if (nextState.Phase == DeliberateDriftPhase.Drift)
            {
                rearGripMultiplier = DriftRearGripMultiplier;
                wheelBrakeMultiplier = DriftWheelBrakeMultiplier;
            }
            else if (nextState.Phase == DeliberateDriftPhase.Recovery)
            {
                float recoveryBlend = UnityEngine.Mathf.InverseLerp(0f, MaximumRecoverySeconds,
                    nextState.RecoverySeconds);
                rearGripMultiplier = UnityEngine.Mathf.Lerp(RecoveryRearGripMultiplier, 1f, recoveryBlend);
            }

            bool forward = signedLongitudinalSpeed > HandlingMetrics.DefaultSpeedDeadZone;
            bool shouldApply = grounded && forward && nextState.Phase != DeliberateDriftPhase.Grip;
            if (!shouldApply)
                return new DeliberateDriftDecision(nextState, rearGripMultiplier, wheelBrakeMultiplier,
                    0f, 0f, 0f, false);

            float effectiveSteering = UnityEngine.Mathf.Clamp01(steeringMultiplier);
            float effectiveGrip = UnityEngine.Mathf.Clamp01(gripMultiplier);
            float targetYawRate = 0f;
            float lateralAcceleration;
            float maximumLateralAcceleration;
            if (nextState.IsDrifting)
            {
                float speedBlend = UnityEngine.Mathf.InverseLerp(HoldForwardSpeed,
                    EntryForwardSpeed * 2.5f, signedLongitudinalSpeed);
                float yawSpeedMultiplier = UnityEngine.Mathf.Lerp(.65f, 1f, speedBlend);
                targetYawRate = steering * MaximumTargetYawRateDegreesPerSecond * yawSpeedMultiplier *
                                effectiveSteering;
                float targetLateralSpeed = steering * signedLongitudinalSpeed * DriftLateralVelocityRatio;
                lateralAcceleration = (targetLateralSpeed - lateralSpeed) * DriftAlignmentGainPerSecond *
                                      effectiveGrip;
                maximumLateralAcceleration = MaximumDriftLateralAcceleration * effectiveGrip;
            }
            else
            {
                lateralAcceleration = -lateralSpeed * RecoveryAlignmentGainPerSecond * effectiveGrip;
                maximumLateralAcceleration = MaximumRecoveryLateralAcceleration * effectiveGrip;
            }

            float yawAcceleration = UnityEngine.Mathf.Clamp(
                (targetYawRate - currentYawRateDegreesPerSecond) * YawErrorGainPerSecond,
                -MaximumYawAccelerationDegreesPerSecondSquared * effectiveSteering,
                MaximumYawAccelerationDegreesPerSecondSquared * effectiveSteering);
            lateralAcceleration = UnityEngine.Mathf.Clamp(lateralAcceleration,
                -maximumLateralAcceleration, maximumLateralAcceleration);
            return new DeliberateDriftDecision(nextState, rearGripMultiplier, wheelBrakeMultiplier,
                targetYawRate, yawAcceleration, lateralAcceleration, true);
        }
    }

    internal sealed class DeliberateDriftHandlingModelRuntime : IVehicleHandlingModelRuntime
    {
        private float currentSteerAngle;
        private DeliberateDriftState driftState = DeliberateDriftModel.InitialState;
        private float rearGripMultiplier = 1f;

        public HandlingModelId Id => HandlingModelId.D;

        public void FixedStep(VehicleController vehicle)
        {
            float signedLongitudinalSpeed = vehicle.SignedLongitudinalSpeed;
            DirectionAwareDriveDecision drive = DeliberateDriftModel.EvaluateDrive(vehicle.ThrottleInput,
                vehicle.BrakeInput, signedLongitudinalSpeed, vehicle.SpeedMetersPerSecond,
                vehicle.MaximumSpeedMetersPerSecond);
            float targetSteerAngle = DirectionAwareWheelColliderModel.CalculateTargetSteerAngle(
                vehicle.SteerInput, vehicle.ThrottleInput, signedLongitudinalSpeed,
                vehicle.MaximumSpeedMetersPerSecond, vehicle.MaximumSteerAngle,
                vehicle.CombatSteeringMultiplier);
            currentSteerAngle = DirectionAwareWheelColliderModel.MoveSteerTowards(currentSteerAngle,
                targetSteerAngle, vehicle.MaximumSteerAngle, UnityEngine.Time.fixedDeltaTime);

            DeliberateDriftDecision drift = DeliberateDriftModel.Evaluate(driftState, vehicle.BrakeInput,
                vehicle.SteerInput, signedLongitudinalSpeed, vehicle.LateralSpeed,
                vehicle.CurrentYawRateDegreesPerSecond, vehicle.GroundedWheelCount,
                vehicle.CombatSteeringMultiplier, vehicle.CombatGripMultiplier,
                UnityEngine.Time.fixedDeltaTime);
            driftState = drift.State;
            rearGripMultiplier = drift.RearGripMultiplier;
            float brakeFraction = drive.IsOpposingMotion
                ? drive.BrakeFraction
                : drive.BrakeFraction * drift.WheelBrakeMultiplier;
            vehicle.FixedStepDeliberateDriftHandling(currentSteerAngle, drive.MotorInput,
                brakeFraction, drift);
        }

        public void OnCombatModifiersChanged(VehicleController vehicle) =>
            vehicle.ApplyDeliberateDriftGrip(rearGripMultiplier);

        public void Reset(VehicleController vehicle)
        {
            currentSteerAngle = 0f;
            driftState = DeliberateDriftModel.InitialState;
            rearGripMultiplier = 1f;
            vehicle.ResetDeliberateDriftState();
        }
    }
}
