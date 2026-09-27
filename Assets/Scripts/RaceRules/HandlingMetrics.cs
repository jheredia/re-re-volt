using System;

namespace Vehicle
{
    public enum LongitudinalDirection
    {
        Reverse = -1,
        Stationary = 0,
        Forward = 1
    }

    public readonly struct DirectionChangeMeasurement
    {
        public DirectionChangeMeasurement(LongitudinalDirection from, LongitudinalDirection to, double durationSeconds)
        {
            From = from;
            To = to;
            DurationSeconds = durationSeconds;
        }

        public LongitudinalDirection From { get; }
        public LongitudinalDirection To { get; }
        public double DurationSeconds { get; }
    }

    /// <summary>Allocation-free transition timer driven by an explicit simulation delta.</summary>
    public struct DirectionChangeTimer
    {
        private LongitudinalDirection from;
        private LongitudinalDirection target;
        private double elapsedSeconds;

        public bool IsMeasuring { get; private set; }
        public LongitudinalDirection From => from;
        public LongitudinalDirection Target => target;
        public double ElapsedSeconds => elapsedSeconds;
        public int CompletedCount { get; private set; }
        public DirectionChangeMeasurement LastCompleted { get; private set; }

        public bool Step(double deltaSeconds, float throttle, float signedLongitudinalSpeed,
            out DirectionChangeMeasurement completed)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            completed = default;
            LongitudinalDirection requested = HandlingMetrics.ClassifyDirection(throttle,
                HandlingMetrics.DefaultInputDeadZone);
            LongitudinalDirection travelling = HandlingMetrics.ClassifyDirection(signedLongitudinalSpeed,
                HandlingMetrics.DefaultSpeedDeadZone);

            if (!IsMeasuring)
            {
                if (requested == LongitudinalDirection.Stationary ||
                    travelling == LongitudinalDirection.Stationary || requested == travelling) return false;
                from = travelling;
                target = requested;
                elapsedSeconds = deltaSeconds;
                IsMeasuring = true;
                return false;
            }

            if (requested != target)
            {
                Cancel();
                return false;
            }

            elapsedSeconds += deltaSeconds;
            if (travelling != target) return false;

            completed = new DirectionChangeMeasurement(from, target, elapsedSeconds);
            LastCompleted = completed;
            CompletedCount++;
            Cancel();
            return true;
        }

        /// <summary>Cancels an incomplete transition without erasing completed measurements.</summary>
        public void Cancel()
        {
            IsMeasuring = false;
            from = LongitudinalDirection.Stationary;
            target = LongitudinalDirection.Stationary;
            elapsedSeconds = 0d;
        }

        public void Reset()
        {
            Cancel();
            CompletedCount = 0;
            LastCompleted = default;
        }
    }

    public readonly struct HandlingMotionSample
    {
        public HandlingMotionSample(float signedLongitudinalSpeed, float lateralSpeed, float planarSpeed,
            float slipAngleDegrees, float yawRateDegreesPerSecond, bool isDrifting, bool isSpin)
        {
            SignedLongitudinalSpeed = signedLongitudinalSpeed;
            LateralSpeed = lateralSpeed;
            PlanarSpeed = planarSpeed;
            SlipAngleDegrees = slipAngleDegrees;
            YawRateDegreesPerSecond = yawRateDegreesPerSecond;
            IsDrifting = isDrifting;
            IsSpin = isSpin;
        }

        public float SignedLongitudinalSpeed { get; }
        public float LateralSpeed { get; }
        public float PlanarSpeed { get; }
        public float SlipAngleDegrees { get; }
        public float YawRateDegreesPerSecond { get; }
        public bool IsDrifting { get; }
        public bool IsSpin { get; }
        public LongitudinalDirection Direction => HandlingMetrics.ClassifyDirection(SignedLongitudinalSpeed,
            HandlingMetrics.DefaultSpeedDeadZone);
        public float TurnRadius => HandlingMetrics.CalculateTurnRadius(PlanarSpeed, YawRateDegreesPerSecond);
    }

    /// <summary>Engine-independent calculations shared by all handling candidates and bake-off recorders.</summary>
    public static class HandlingMetrics
    {
        public const float DefaultInputDeadZone = .1f;
        public const float DefaultSpeedDeadZone = .1f;
        public const float DefaultSpinSlipAngle = 75f;
        public const float DefaultSpinYawRate = 180f;
        private const double DegreesToRadians = Math.PI / 180d;

        public static LongitudinalDirection ClassifyDirection(float signedValue,
            float deadZone = DefaultSpeedDeadZone)
        {
            if (float.IsNaN(signedValue) || float.IsInfinity(signedValue))
                throw new ArgumentOutOfRangeException(nameof(signedValue));
            if (float.IsNaN(deadZone) || float.IsInfinity(deadZone) || deadZone < 0f)
                throw new ArgumentOutOfRangeException(nameof(deadZone));
            if (signedValue > deadZone) return LongitudinalDirection.Forward;
            if (signedValue < -deadZone) return LongitudinalDirection.Reverse;
            return LongitudinalDirection.Stationary;
        }

        /// <summary>Returns absolute instantaneous radius in metres, or infinity when no stable arc exists.</summary>
        public static float CalculateTurnRadius(float planarSpeedMetersPerSecond, float yawRateDegreesPerSecond)
        {
            ValidateFinite(planarSpeedMetersPerSecond, nameof(planarSpeedMetersPerSecond));
            ValidateFinite(yawRateDegreesPerSecond, nameof(yawRateDegreesPerSecond));
            double speed = Math.Abs(planarSpeedMetersPerSecond);
            double yawRadians = Math.Abs(yawRateDegreesPerSecond) * DegreesToRadians;
            if (speed <= DefaultSpeedDeadZone || yawRadians <= .0001d) return float.PositiveInfinity;
            return (float)(speed / yawRadians);
        }

        public static HandlingMotionSample CreateMotionSample(float signedLongitudinalSpeed, float lateralSpeed,
            float yawRateDegreesPerSecond, bool isDrifting, float spinSlipAngle = DefaultSpinSlipAngle,
            float spinYawRate = DefaultSpinYawRate)
        {
            ValidateFinite(signedLongitudinalSpeed, nameof(signedLongitudinalSpeed));
            ValidateFinite(lateralSpeed, nameof(lateralSpeed));
            ValidateFinite(yawRateDegreesPerSecond, nameof(yawRateDegreesPerSecond));
            if (float.IsNaN(spinSlipAngle) || float.IsInfinity(spinSlipAngle) || spinSlipAngle <= 0f)
                throw new ArgumentOutOfRangeException(nameof(spinSlipAngle));
            if (float.IsNaN(spinYawRate) || float.IsInfinity(spinYawRate) || spinYawRate <= 0f)
                throw new ArgumentOutOfRangeException(nameof(spinYawRate));

            double longitudinal = signedLongitudinalSpeed;
            double lateral = lateralSpeed;
            float planarSpeed = (float)Math.Sqrt(longitudinal * longitudinal + lateral * lateral);
            float slipAngle = planarSpeed <= DefaultSpeedDeadZone
                ? 0f
                : (float)(Math.Atan2(lateral, Math.Abs(longitudinal)) / DegreesToRadians);
            bool spin = Math.Abs(slipAngle) >= spinSlipAngle || Math.Abs(yawRateDegreesPerSecond) >= spinYawRate;
            return new HandlingMotionSample(signedLongitudinalSpeed, lateralSpeed, planarSpeed, slipAngle,
                yawRateDegreesPerSecond, isDrifting, spin);
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
