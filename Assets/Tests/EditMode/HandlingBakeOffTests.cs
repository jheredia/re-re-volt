using System;
using NUnit.Framework;
using UnityEngine;
using Vehicle;

namespace Tests.EditMode
{
    public sealed class HandlingBakeOffTests
    {
        [Test]
        public void CatalogExposesNeutralIdsAndExplicitBaselineFallbacks()
        {
            Assert.That(HandlingModelCatalog.GetPublicLabel(HandlingModelId.A), Is.EqualTo("MODEL A"));
            Assert.That(HandlingModelCatalog.GetPublicLabel(HandlingModelId.D), Is.EqualTo("MODEL D"));
            Assert.That(HandlingModelCatalog.IsImplemented(HandlingModelId.A), Is.True);
            Assert.That(HandlingModelCatalog.ResolveImplementedModel(HandlingModelId.A), Is.EqualTo(HandlingModelId.A));

            foreach (HandlingModelId candidate in new[] { HandlingModelId.B, HandlingModelId.C, HandlingModelId.D })
            {
                Assert.That(HandlingModelCatalog.IsImplemented(candidate), Is.False);
                Assert.That(HandlingModelCatalog.ResolveImplementedModel(candidate), Is.EqualTo(HandlingModelId.A));
            }
        }

        [Test]
        public void VehicleSelectionRejectsUnknownIdsAndReportsFallbackWithoutChangingSelection()
        {
            GameObject gameObject = new GameObject("HandlingModelSelectionTest");
            try
            {
                gameObject.AddComponent<Rigidbody>();
                VehicleController vehicle = gameObject.AddComponent<VehicleController>();
                Assert.That(vehicle.SelectedHandlingModel, Is.EqualTo(HandlingModelId.A));
                Assert.That(vehicle.ActiveHandlingModel, Is.EqualTo(HandlingModelId.A));
                Assert.That(vehicle.HasHandlingModelFallback, Is.False);

                Assert.That(vehicle.TrySelectHandlingModel(HandlingModelId.C), Is.True);
                Assert.That(vehicle.SelectedHandlingModel, Is.EqualTo(HandlingModelId.C));
                Assert.That(vehicle.ActiveHandlingModel, Is.EqualTo(HandlingModelId.A));
                Assert.That(vehicle.HasHandlingModelFallback, Is.True);

                Assert.That(vehicle.TrySelectHandlingModel((HandlingModelId)99), Is.False);
                Assert.That(vehicle.SelectedHandlingModel, Is.EqualTo(HandlingModelId.C));
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
