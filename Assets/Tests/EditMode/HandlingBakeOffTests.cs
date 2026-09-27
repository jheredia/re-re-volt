using System;
using NUnit.Framework;
using UnityEngine;
using Vehicle;

namespace Tests.EditMode
{
    public sealed class HandlingBakeOffTests
    {
        [Test]
        public void CatalogExposesNeutralIdsAndAllImplementedModels()
        {
            Assert.That(HandlingModelCatalog.GetPublicLabel(HandlingModelId.A), Is.EqualTo("MODEL A"));
            Assert.That(HandlingModelCatalog.GetPublicLabel(HandlingModelId.D), Is.EqualTo("MODEL D"));
            Assert.That(HandlingModelCatalog.IsImplemented(HandlingModelId.A), Is.True);
            Assert.That(HandlingModelCatalog.IsImplemented(HandlingModelId.B), Is.True);
            Assert.That(HandlingModelCatalog.IsImplemented(HandlingModelId.C), Is.True);
            Assert.That(HandlingModelCatalog.IsImplemented(HandlingModelId.D), Is.True);
            Assert.That(HandlingModelCatalog.ResolveImplementedModel(HandlingModelId.A), Is.EqualTo(HandlingModelId.A));
            Assert.That(HandlingModelCatalog.ResolveImplementedModel(HandlingModelId.B), Is.EqualTo(HandlingModelId.B));
            Assert.That(HandlingModelCatalog.ResolveImplementedModel(HandlingModelId.C), Is.EqualTo(HandlingModelId.C));
            Assert.That(HandlingModelCatalog.ResolveImplementedModel(HandlingModelId.D), Is.EqualTo(HandlingModelId.D));
            Assert.That(HandlingModelCatalog.ResolveImplementedModel((HandlingModelId)99), Is.EqualTo(HandlingModelId.A));
        }

        [Test]
        public void VehicleSelectionActivatesEveryImplementedCandidate()
        {
            GameObject gameObject = new GameObject("HandlingModelBSelectionTest");
            try
            {
                gameObject.AddComponent<Rigidbody>();
                VehicleController vehicle = gameObject.AddComponent<VehicleController>();

                Assert.That(vehicle.TrySelectHandlingModel(HandlingModelId.B), Is.True);
                Assert.That(vehicle.SelectedHandlingModel, Is.EqualTo(HandlingModelId.B));
                Assert.That(vehicle.ActiveHandlingModel, Is.EqualTo(HandlingModelId.B));
                Assert.That(vehicle.HasHandlingModelFallback, Is.False);

                Assert.That(vehicle.TrySelectHandlingModel(HandlingModelId.C), Is.True);
                Assert.That(vehicle.SelectedHandlingModel, Is.EqualTo(HandlingModelId.C));
                Assert.That(vehicle.ActiveHandlingModel, Is.EqualTo(HandlingModelId.C));
                Assert.That(vehicle.HasHandlingModelFallback, Is.False);

                Assert.That(vehicle.TrySelectHandlingModel(HandlingModelId.D), Is.True);
                Assert.That(vehicle.SelectedHandlingModel, Is.EqualTo(HandlingModelId.D));
                Assert.That(vehicle.ActiveHandlingModel, Is.EqualTo(HandlingModelId.D));
                Assert.That(vehicle.HasHandlingModelFallback, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DirectionAwareDriveBrakesOpposingMotionSymmetricallyWithNeutralTorque()
        {
            DirectionAwareDriveDecision forwardToReverse = DirectionAwareWheelColliderModel.EvaluateDrive(
                -.8f, 0f, 5f, 5f, 25f);
            DirectionAwareDriveDecision reverseToForward = DirectionAwareWheelColliderModel.EvaluateDrive(
                .8f, 0f, -5f, 5f, 25f);

            Assert.That(forwardToReverse.IsOpposingMotion, Is.True);
            Assert.That(reverseToForward.IsOpposingMotion, Is.True);
            Assert.That(forwardToReverse.MotorInput, Is.Zero);
            Assert.That(reverseToForward.MotorInput, Is.Zero);
            Assert.That(reverseToForward.BrakeFraction, Is.EqualTo(forwardToReverse.BrakeFraction).Within(.0001f));
            Assert.That(reverseToForward.BrakeFraction, Is.GreaterThanOrEqualTo(
                DirectionAwareWheelColliderModel.MinimumOpposeMotionBrakeFraction));
        }

        [Test]
        public void DirectionAwareDriveSwitchesPromptlyBelowThresholdAndHonorsNeutralAndSpeedCap()
        {
            DirectionAwareDriveDecision switching = DirectionAwareWheelColliderModel.EvaluateDrive(
                1f, 0f, -.5f, .5f, 25f);
            Assert.That(switching.IsOpposingMotion, Is.False);
            Assert.That(switching.MotorInput, Is.EqualTo(1f));
            Assert.That(switching.BrakeFraction, Is.Zero);

            DirectionAwareDriveDecision neutral = DirectionAwareWheelColliderModel.EvaluateDrive(
                .05f, .25f, 4f, 4f, 25f);
            Assert.That(neutral.MotorInput, Is.Zero);
            Assert.That(neutral.BrakeFraction, Is.EqualTo(.25f));

            DirectionAwareDriveDecision capped = DirectionAwareWheelColliderModel.EvaluateDrive(
                1f, 0f, 25f, 25f, 25f);
            Assert.That(capped.MotorInput, Is.Zero);
        }

        [Test]
        public void DirectionAwareSteeringIsSpeedSensitiveAndReverseIsCapped()
        {
            float lowForward = DirectionAwareWheelColliderModel.CalculateTargetSteerAngle(1f, 1f, 0f,
                25f, 35f, 1f);
            float highForward = DirectionAwareWheelColliderModel.CalculateTargetSteerAngle(1f, 1f, 25f,
                25f, 35f, 1f);
            float highReverse = DirectionAwareWheelColliderModel.CalculateTargetSteerAngle(1f, -1f, -25f,
                25f, 35f, 1f);
            float startingReverse = DirectionAwareWheelColliderModel.CalculateTargetSteerAngle(1f, -1f, 0f,
                25f, 35f, 1f);

            Assert.That(lowForward, Is.EqualTo(35f).Within(.0001f));
            Assert.That(highForward, Is.LessThan(lowForward));
            Assert.That(highReverse, Is.EqualTo(highForward *
                DirectionAwareWheelColliderModel.ReverseSteeringMultiplier).Within(.0001f));
            Assert.That(startingReverse, Is.EqualTo(lowForward *
                DirectionAwareWheelColliderModel.ReverseSteeringMultiplier).Within(.0001f));
        }

        [Test]
        public void DirectionAwareSteeringResponseIsBoundedAndSymmetric()
        {
            float positive = DirectionAwareWheelColliderModel.MoveSteerTowards(0f, 35f, 35f, .02f);
            float negative = DirectionAwareWheelColliderModel.MoveSteerTowards(0f, -35f, 35f, .02f);

            Assert.That(positive, Is.GreaterThan(0f).And.LessThan(35f));
            Assert.That(negative, Is.EqualTo(-positive).Within(.0001f));
        }

        [Test]
        public void BalancedArcadeYawBuildsAtMediumSpeedAndAttenuatesAtHighSpeed()
        {
            BalancedArcadeYawDecision low = BalancedArcadeYawModel.EvaluateYawAssist(
                1f, 2f, 25f, 0f, 4, false, 1f);
            BalancedArcadeYawDecision medium = BalancedArcadeYawModel.EvaluateYawAssist(
                1f, 8f, 25f, 0f, 4, false, 1f);
            BalancedArcadeYawDecision high = BalancedArcadeYawModel.EvaluateYawAssist(
                1f, 25f, 25f, 0f, 4, false, 1f);

            Assert.That(low.ShouldApply, Is.True);
            Assert.That(low.TargetYawRateDegreesPerSecond, Is.GreaterThan(0f));
            Assert.That(medium.TargetYawRateDegreesPerSecond, Is.GreaterThan(low.TargetYawRateDegreesPerSecond));
            Assert.That(high.TargetYawRateDegreesPerSecond, Is.LessThan(medium.TargetYawRateDegreesPerSecond));
        }

        [Test]
        public void BalancedArcadeYawCapsAndReversesReverseAuthority()
        {
            BalancedArcadeYawDecision forward = BalancedArcadeYawModel.EvaluateYawAssist(
                .8f, 8f, 25f, 0f, 4, false, 1f);
            BalancedArcadeYawDecision reverse = BalancedArcadeYawModel.EvaluateYawAssist(
                .8f, -8f, 25f, 0f, 4, false, 1f);

            Assert.That(forward.TargetYawRateDegreesPerSecond, Is.GreaterThan(0f));
            Assert.That(reverse.TargetYawRateDegreesPerSecond, Is.LessThan(0f));
            Assert.That(Mathf.Abs(reverse.TargetYawRateDegreesPerSecond),
                Is.EqualTo(Mathf.Abs(forward.TargetYawRateDegreesPerSecond) *
                    BalancedArcadeYawModel.ReverseYawMultiplier).Within(.0001f));
        }

        [Test]
        public void BalancedArcadeYawRequiresSteeringSpeedAndGroundContact()
        {
            BalancedArcadeYawDecision airborne = BalancedArcadeYawModel.EvaluateYawAssist(
                1f, 8f, 25f, 0f, 0, false, 1f);
            BalancedArcadeYawDecision slow = BalancedArcadeYawModel.EvaluateYawAssist(
                1f, 1f, 25f, 0f, 4, false, 1f);
            BalancedArcadeYawDecision straight = BalancedArcadeYawModel.EvaluateYawAssist(
                0f, 8f, 25f, 0f, 4, false, 1f);

            Assert.That(airborne.ShouldApply, Is.False);
            Assert.That(slow.ShouldApply, Is.False);
            Assert.That(straight.ShouldApply, Is.False);
            Assert.That(airborne.YawAccelerationDegreesPerSecondSquared, Is.Zero);
        }

        [Test]
        public void BalancedArcadeYawAccelerationIsBoundedSymmetricAndHonorsModifiers()
        {
            BalancedArcadeYawDecision positive = BalancedArcadeYawModel.EvaluateYawAssist(
                1f, 8f, 25f, 0f, 4, false, 1f);
            BalancedArcadeYawDecision negative = BalancedArcadeYawModel.EvaluateYawAssist(
                -1f, 8f, 25f, 0f, 4, false, 1f);
            BalancedArcadeYawDecision reduced = BalancedArcadeYawModel.EvaluateYawAssist(
                1f, 8f, 25f, 0f, 4, true, .5f);

            Assert.That(positive.YawAccelerationDegreesPerSecondSquared,
                Is.EqualTo(BalancedArcadeYawModel.MaximumYawAccelerationDegreesPerSecondSquared));
            Assert.That(negative.YawAccelerationDegreesPerSecondSquared,
                Is.EqualTo(-positive.YawAccelerationDegreesPerSecondSquared).Within(.0001f));
            Assert.That(reduced.TargetYawRateDegreesPerSecond, Is.EqualTo(
                BalancedArcadeYawModel.MaximumTargetYawRateDegreesPerSecond *
                BalancedArcadeYawModel.DriftYawMultiplier * .5f).Within(.0001f));
        }

        [Test]
        public void BalancedArcadeDriveChangesDirectionPromptlyAndSymmetrically()
        {
            DirectionAwareDriveDecision forward = BalancedArcadeYawModel.EvaluateDrive(
                1f, 0f, -.5f, .5f, 25f);
            DirectionAwareDriveDecision reverse = BalancedArcadeYawModel.EvaluateDrive(
                -1f, 0f, .5f, .5f, 25f);

            Assert.That(forward.IsOpposingMotion, Is.False);
            Assert.That(reverse.IsOpposingMotion, Is.False);
            Assert.That(forward.MotorInput, Is.EqualTo(1f));
            Assert.That(reverse.MotorInput, Is.EqualTo(-1f));
            Assert.That(forward.BrakeFraction, Is.EqualTo(reverse.BrakeFraction));
        }

        [Test]
        public void BalancedArcadeSelectionSurvivesControlDisableAndTeleportReset()
        {
            GameObject gameObject = new GameObject("BalancedArcadeLifecycleTest");
            try
            {
                Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
                VehicleController vehicle = gameObject.AddComponent<VehicleController>();
                typeof(VehicleController).GetMethod("Awake", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)?.Invoke(vehicle, null);
                vehicle.TrySelectHandlingModel(HandlingModelId.C);
                vehicle.ThrottleInput = 1f;
                vehicle.SteerInput = 1f;
                vehicle.BrakeInput = 1f;
                vehicle.SetCombatModifiers(.5f, .5f, .5f);
                rigidbody.linearVelocity = new Vector3(1f, 2f, 3f);
                rigidbody.angularVelocity = Vector3.one;

                vehicle.SetInputEnabled(false);
                vehicle.Teleport(new Pose(new Vector3(4f, 0f, 2f), Quaternion.identity));

                Assert.That(vehicle.ActiveHandlingModel, Is.EqualTo(HandlingModelId.C));
                Assert.That(vehicle.HasHandlingModelFallback, Is.False);
                Assert.That(vehicle.ThrottleInput, Is.Zero);
                Assert.That(vehicle.SteerInput, Is.Zero);
                Assert.That(vehicle.BrakeInput, Is.Zero);
                Assert.That(vehicle.HasCombatModifier, Is.False);
                Assert.That(rigidbody.linearVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(rigidbody.angularVelocity, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DeliberateDriftRequiresGroundedForwardSpeedBrakeAndSteering()
        {
            DeliberateDriftState initial = DeliberateDriftModel.InitialState;
            DeliberateDriftDecision valid = DeliberateDriftModel.Evaluate(initial, .8f, .6f, 8f,
                0f, 0f, 4, 1f, 1f, .02f);
            DeliberateDriftDecision noBrake = DeliberateDriftModel.Evaluate(initial, .5f, .6f, 8f,
                0f, 0f, 4, 1f, 1f, .02f);
            DeliberateDriftDecision noSteering = DeliberateDriftModel.Evaluate(initial, .8f, .2f, 8f,
                0f, 0f, 4, 1f, 1f, .02f);
            DeliberateDriftDecision tooSlow = DeliberateDriftModel.Evaluate(initial, .8f, .6f, 4f,
                0f, 0f, 4, 1f, 1f, .02f);

            Assert.That(valid.State.Phase, Is.EqualTo(DeliberateDriftPhase.Drift));
            Assert.That(valid.IsDrifting, Is.True);
            Assert.That(valid.RearGripMultiplier, Is.EqualTo(DeliberateDriftModel.DriftRearGripMultiplier));
            Assert.That(valid.WheelBrakeMultiplier,
                Is.EqualTo(DeliberateDriftModel.DriftWheelBrakeMultiplier));
            Assert.That(noBrake.State.Phase, Is.EqualTo(DeliberateDriftPhase.Grip));
            Assert.That(noSteering.State.Phase, Is.EqualTo(DeliberateDriftPhase.Grip));
            Assert.That(tooSlow.State.Phase, Is.EqualTo(DeliberateDriftPhase.Grip));
        }

        [Test]
        public void DeliberateDriftUsesHoldAndRecoveryHysteresis()
        {
            DeliberateDriftDecision entered = DeliberateDriftModel.Evaluate(DeliberateDriftModel.InitialState,
                .8f, .6f, 8f, 1f, 0f, 4, 1f, 1f, .02f);
            DeliberateDriftDecision heldBelowEntryThresholds = DeliberateDriftModel.Evaluate(entered.State,
                .4f, .15f, 3.5f, 1f, 0f, 4, 1f, 1f, .02f);
            DeliberateDriftDecision exiting = DeliberateDriftModel.Evaluate(heldBelowEntryThresholds.State,
                .4f, .05f, 3.5f, 1f, 0f, 4, 1f, 1f, .02f);
            DeliberateDriftDecision recovered = DeliberateDriftModel.Evaluate(exiting.State,
                0f, 0f, 3.5f, .25f, 0f, 4, 1f, 1f,
                DeliberateDriftModel.MinimumRecoverySeconds);

            Assert.That(heldBelowEntryThresholds.State.Phase, Is.EqualTo(DeliberateDriftPhase.Drift));
            Assert.That(exiting.State.Phase, Is.EqualTo(DeliberateDriftPhase.Recovery));
            Assert.That(exiting.IsDrifting, Is.False);
            Assert.That(exiting.RearGripMultiplier,
                Is.EqualTo(DeliberateDriftModel.RecoveryRearGripMultiplier).Within(.0001f));
            Assert.That(recovered.State.Phase, Is.EqualTo(DeliberateDriftPhase.Grip));
            Assert.That(recovered.RearGripMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void DeliberateDriftNeverEntersOrAssistsInReverseOrAirborne()
        {
            DeliberateDriftDecision reverse = DeliberateDriftModel.Evaluate(DeliberateDriftModel.InitialState,
                1f, 1f, -8f, 4f, 90f, 4, 1f, 1f, .02f);
            DeliberateDriftDecision airborne = DeliberateDriftModel.Evaluate(DeliberateDriftModel.InitialState,
                1f, 1f, 8f, 4f, 90f, 0, 1f, 1f, .02f);
            DeliberateDriftState active = new DeliberateDriftState(DeliberateDriftPhase.Drift, 0f);
            DeliberateDriftDecision reverseExit = DeliberateDriftModel.Evaluate(active,
                1f, 1f, -8f, 4f, 90f, 4, 1f, 1f, .02f);
            DeliberateDriftDecision airborneExit = DeliberateDriftModel.Evaluate(active,
                1f, 1f, 8f, 4f, 90f, 0, 1f, 1f, .02f);

            Assert.That(reverse.State.Phase, Is.EqualTo(DeliberateDriftPhase.Grip));
            Assert.That(airborne.State.Phase, Is.EqualTo(DeliberateDriftPhase.Grip));
            Assert.That(reverse.ShouldApplyAssistance, Is.False);
            Assert.That(airborne.ShouldApplyAssistance, Is.False);
            Assert.That(reverseExit.State.Phase, Is.EqualTo(DeliberateDriftPhase.Recovery));
            Assert.That(airborneExit.State.Phase, Is.EqualTo(DeliberateDriftPhase.Recovery));
            Assert.That(reverseExit.ShouldApplyAssistance, Is.False);
            Assert.That(airborneExit.ShouldApplyAssistance, Is.False);
            Assert.That(reverseExit.YawAccelerationDegreesPerSecondSquared, Is.Zero);
            Assert.That(airborneExit.LateralAccelerationMetersPerSecondSquared, Is.Zero);
        }

        [Test]
        public void DeliberateDriftYawAndVelocityAlignmentAreBoundedAndRecoverTowardStableMotion()
        {
            DeliberateDriftDecision drift = DeliberateDriftModel.Evaluate(DeliberateDriftModel.InitialState,
                1f, 1f, 8f, 50f, -1000f, 4, 1f, 1f, .02f);
            DeliberateDriftDecision recovery = DeliberateDriftModel.Evaluate(drift.State,
                0f, 0f, 8f, 50f, 1000f, 4, 1f, 1f, .02f);
            DeliberateDriftDecision disrupted = DeliberateDriftModel.Evaluate(DeliberateDriftModel.InitialState,
                1f, 1f, 8f, 50f, -1000f, 4, .5f, .25f, .02f);

            Assert.That(Mathf.Abs(drift.TargetYawRateDegreesPerSecond),
                Is.LessThanOrEqualTo(DeliberateDriftModel.MaximumTargetYawRateDegreesPerSecond));
            Assert.That(drift.YawAccelerationDegreesPerSecondSquared,
                Is.EqualTo(DeliberateDriftModel.MaximumYawAccelerationDegreesPerSecondSquared));
            Assert.That(drift.LateralAccelerationMetersPerSecondSquared,
                Is.EqualTo(-DeliberateDriftModel.MaximumDriftLateralAcceleration));
            Assert.That(recovery.State.Phase, Is.EqualTo(DeliberateDriftPhase.Recovery));
            Assert.That(recovery.TargetYawRateDegreesPerSecond, Is.Zero);
            Assert.That(recovery.YawAccelerationDegreesPerSecondSquared,
                Is.EqualTo(-DeliberateDriftModel.MaximumYawAccelerationDegreesPerSecondSquared));
            Assert.That(recovery.LateralAccelerationMetersPerSecondSquared,
                Is.EqualTo(-DeliberateDriftModel.MaximumRecoveryLateralAcceleration));
            Assert.That(Mathf.Abs(disrupted.YawAccelerationDegreesPerSecondSquared),
                Is.LessThanOrEqualTo(DeliberateDriftModel.MaximumYawAccelerationDegreesPerSecondSquared * .5f));
            Assert.That(Mathf.Abs(disrupted.LateralAccelerationMetersPerSecondSquared),
                Is.LessThanOrEqualTo(DeliberateDriftModel.MaximumDriftLateralAcceleration * .25f));
        }

        [Test]
        public void DeliberateDriftDriveChangesDirectionPromptlyAndSymmetrically()
        {
            DirectionAwareDriveDecision forward = DeliberateDriftModel.EvaluateDrive(
                1f, 0f, -.5f, .5f, 25f);
            DirectionAwareDriveDecision reverse = DeliberateDriftModel.EvaluateDrive(
                -1f, 0f, .5f, .5f, 25f);

            Assert.That(forward.IsOpposingMotion, Is.False);
            Assert.That(reverse.IsOpposingMotion, Is.False);
            Assert.That(forward.MotorInput, Is.EqualTo(1f));
            Assert.That(reverse.MotorInput, Is.EqualTo(-1f));
            Assert.That(forward.BrakeFraction, Is.EqualTo(reverse.BrakeFraction));

            float forwardSteer = DirectionAwareWheelColliderModel.CalculateTargetSteerAngle(
                1f, 1f, 8f, 25f, 35f, 1f);
            float reverseSteer = DirectionAwareWheelColliderModel.CalculateTargetSteerAngle(
                1f, -1f, -8f, 25f, 35f, 1f);
            Assert.That(reverseSteer, Is.EqualTo(forwardSteer *
                DirectionAwareWheelColliderModel.ReverseSteeringMultiplier).Within(.0001f));
        }

        [Test]
        public void DeliberateDriftSelectionSurvivesControlDisableAndTeleportReset()
        {
            GameObject gameObject = new GameObject("DeliberateDriftLifecycleTest");
            try
            {
                Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
                VehicleController vehicle = gameObject.AddComponent<VehicleController>();
                typeof(VehicleController).GetMethod("Awake", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)?.Invoke(vehicle, null);
                vehicle.TrySelectHandlingModel(HandlingModelId.D);
                vehicle.ThrottleInput = 1f;
                vehicle.SteerInput = 1f;
                vehicle.BrakeInput = 1f;
                vehicle.SetCombatModifiers(.5f, .5f, .5f);
                rigidbody.linearVelocity = new Vector3(1f, 2f, 3f);
                rigidbody.angularVelocity = Vector3.one;

                vehicle.SetInputEnabled(false);
                vehicle.Teleport(new Pose(new Vector3(4f, 0f, 2f), Quaternion.identity));

                Assert.That(vehicle.ActiveHandlingModel, Is.EqualTo(HandlingModelId.D));
                Assert.That(vehicle.HasHandlingModelFallback, Is.False);
                Assert.That(vehicle.ThrottleInput, Is.Zero);
                Assert.That(vehicle.SteerInput, Is.Zero);
                Assert.That(vehicle.BrakeInput, Is.Zero);
                Assert.That(vehicle.IsDrifting, Is.False);
                Assert.That(vehicle.HasCombatModifier, Is.False);
                Assert.That(rigidbody.linearVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(rigidbody.angularVelocity, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void VehicleSelectionRejectsUnknownIdsWithoutChangingSelection()
        {
            GameObject gameObject = new GameObject("HandlingModelSelectionTest");
            try
            {
                gameObject.AddComponent<Rigidbody>();
                VehicleController vehicle = gameObject.AddComponent<VehicleController>();
                Assert.That(vehicle.SelectedHandlingModel, Is.EqualTo(HandlingModelId.A));
                Assert.That(vehicle.ActiveHandlingModel, Is.EqualTo(HandlingModelId.A));
                Assert.That(vehicle.HasHandlingModelFallback, Is.False);

                Assert.That(vehicle.TrySelectHandlingModel(HandlingModelId.D), Is.True);
                Assert.That(vehicle.SelectedHandlingModel, Is.EqualTo(HandlingModelId.D));
                Assert.That(vehicle.ActiveHandlingModel, Is.EqualTo(HandlingModelId.D));
                Assert.That(vehicle.HasHandlingModelFallback, Is.False);

                Assert.That(vehicle.TrySelectHandlingModel((HandlingModelId)99), Is.False);
                Assert.That(vehicle.SelectedHandlingModel, Is.EqualTo(HandlingModelId.D));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase(-2f, LongitudinalDirection.Reverse)]
        [TestCase(-.1f, LongitudinalDirection.Stationary)]
        [TestCase(0f, LongitudinalDirection.Stationary)]
        [TestCase(.1f, LongitudinalDirection.Stationary)]
        [TestCase(2f, LongitudinalDirection.Forward)]
        public void SignedDirectionUsesSymmetricDeadZone(float speed, LongitudinalDirection expected)
        {
            Assert.That(HandlingMetrics.ClassifyDirection(speed), Is.EqualTo(expected));
        }

        [Test]
        public void DirectionChangeTimerCompletesAndNeutralInputCancelsWithoutLosingHistory()
        {
            DirectionChangeTimer timer = default;
            Assert.That(timer.Step(.02d, 1f, -4f, out _), Is.False);
            Assert.That(timer.IsMeasuring, Is.True);
            Assert.That(timer.Step(.02d, 1f, -.05f, out _), Is.False);
            Assert.That(timer.Step(.02d, 1f, 1f, out DirectionChangeMeasurement completed), Is.True);
            Assert.That(completed.From, Is.EqualTo(LongitudinalDirection.Reverse));
            Assert.That(completed.To, Is.EqualTo(LongitudinalDirection.Forward));
            Assert.That(completed.DurationSeconds, Is.EqualTo(.06d).Within(.000001d));
            Assert.That(timer.CompletedCount, Is.EqualTo(1));

            timer.Step(.02d, -1f, 2f, out _);
            Assert.That(timer.IsMeasuring, Is.True);
            timer.Step(.02d, 0f, 1f, out _);
            Assert.That(timer.IsMeasuring, Is.False);
            Assert.That(timer.CompletedCount, Is.EqualTo(1));
            Assert.That(timer.LastCompleted.DurationSeconds, Is.EqualTo(.06d).Within(.000001d));
        }

        [Test]
        public void TurnRadiusUsesYawRateAndRejectsAStationaryOrStraightSample()
        {
            Assert.That(HandlingMetrics.CalculateTurnRadius(10f, 90f), Is.EqualTo(6.366198f).Within(.0001f));
            Assert.That(HandlingMetrics.CalculateTurnRadius(-10f, -90f), Is.EqualTo(6.366198f).Within(.0001f));
            Assert.That(HandlingMetrics.CalculateTurnRadius(0f, 90f), Is.EqualTo(float.PositiveInfinity));
            Assert.That(HandlingMetrics.CalculateTurnRadius(10f, 0f), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void MotionSampleKeepsSignedSlipYawDriftAndSpinEvidence()
        {
            HandlingMotionSample stable = HandlingMetrics.CreateMotionSample(4f, 4f, -60f, true);
            Assert.That(stable.Direction, Is.EqualTo(LongitudinalDirection.Forward));
            Assert.That(stable.PlanarSpeed, Is.EqualTo(Mathf.Sqrt(32f)).Within(.0001f));
            Assert.That(stable.SlipAngleDegrees, Is.EqualTo(45f).Within(.0001f));
            Assert.That(stable.YawRateDegreesPerSecond, Is.EqualTo(-60f));
            Assert.That(stable.IsDrifting, Is.True);
            Assert.That(stable.IsSpin, Is.False);

            HandlingMotionSample reverseSpin = HandlingMetrics.CreateMotionSample(-1f, -4f, 181f, false);
            Assert.That(reverseSpin.Direction, Is.EqualTo(LongitudinalDirection.Reverse));
            Assert.That(reverseSpin.SlipAngleDegrees, Is.LessThan(0f));
            Assert.That(reverseSpin.IsDrifting, Is.False);
            Assert.That(reverseSpin.IsSpin, Is.True);
        }

        [Test]
        public void MetricsRejectNonFiniteInputsAndInvalidThresholds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HandlingMetrics.ClassifyDirection(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => HandlingMetrics.CalculateTurnRadius(1f, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => HandlingMetrics.CreateMotionSample(1f, 0f, 0f, false, 0f));
            DirectionChangeTimer timer = default;
            Assert.Throws<ArgumentOutOfRangeException>(() => timer.Step(-.01d, 1f, -1f, out _));
        }
    }
}
