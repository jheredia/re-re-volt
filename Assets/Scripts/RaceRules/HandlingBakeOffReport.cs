using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Vehicle
{
    public enum HandlingSpeedBand
    {
        Low,
        Medium,
        High
    }

    public readonly struct HandlingTurnRadiusAggregate
    {
        public HandlingTurnRadiusAggregate(int sampleCount, double totalMeters)
        {
            SampleCount = sampleCount;
            TotalMeters = totalMeters;
        }

        public int SampleCount { get; }
        public double TotalMeters { get; }
        public double? AverageMeters => SampleCount > 0 ? TotalMeters / SampleCount : null;
    }

    public readonly struct HandlingBakeOffMetrics
    {
        public HandlingBakeOffMetrics(
            int activeSampleCount,
            double activeSampleTimeSeconds,
            int directionChangeCount,
            double directionChangeTotalTimeSeconds,
            HandlingTurnRadiusAggregate forwardLow,
            HandlingTurnRadiusAggregate forwardMedium,
            HandlingTurnRadiusAggregate forwardHigh,
            HandlingTurnRadiusAggregate reverseLow,
            HandlingTurnRadiusAggregate reverseMedium,
            HandlingTurnRadiusAggregate reverseHigh,
            int driftEntryCount,
            double driftTimeSeconds,
            double peakAbsoluteSlipDegrees,
            double peakAbsoluteYawDegreesPerSecond,
            int spinEntryCount,
            int groundedEpisodeCount,
            double groundedTimeSeconds,
            int airborneEpisodeCount,
            double airborneTimeSeconds)
        {
            ActiveSampleCount = activeSampleCount;
            ActiveSampleTimeSeconds = activeSampleTimeSeconds;
            DirectionChangeCount = directionChangeCount;
            DirectionChangeTotalTimeSeconds = directionChangeTotalTimeSeconds;
            ForwardLow = forwardLow;
            ForwardMedium = forwardMedium;
            ForwardHigh = forwardHigh;
            ReverseLow = reverseLow;
            ReverseMedium = reverseMedium;
            ReverseHigh = reverseHigh;
            DriftEntryCount = driftEntryCount;
            DriftTimeSeconds = driftTimeSeconds;
            PeakAbsoluteSlipDegrees = peakAbsoluteSlipDegrees;
            PeakAbsoluteYawDegreesPerSecond = peakAbsoluteYawDegreesPerSecond;
            SpinEntryCount = spinEntryCount;
            GroundedEpisodeCount = groundedEpisodeCount;
            GroundedTimeSeconds = groundedTimeSeconds;
            AirborneEpisodeCount = airborneEpisodeCount;
            AirborneTimeSeconds = airborneTimeSeconds;
        }

        public int ActiveSampleCount { get; }
        public double ActiveSampleTimeSeconds { get; }
        public int DirectionChangeCount { get; }
        public double DirectionChangeTotalTimeSeconds { get; }
        public double? DirectionChangeAverageTimeSeconds => DirectionChangeCount > 0
            ? DirectionChangeTotalTimeSeconds / DirectionChangeCount
            : null;
        public HandlingTurnRadiusAggregate ForwardLow { get; }
        public HandlingTurnRadiusAggregate ForwardMedium { get; }
        public HandlingTurnRadiusAggregate ForwardHigh { get; }
        public HandlingTurnRadiusAggregate ReverseLow { get; }
        public HandlingTurnRadiusAggregate ReverseMedium { get; }
        public HandlingTurnRadiusAggregate ReverseHigh { get; }
        public int DriftEntryCount { get; }
        public double DriftTimeSeconds { get; }
        public double PeakAbsoluteSlipDegrees { get; }
        public double PeakAbsoluteYawDegreesPerSecond { get; }
        public int SpinEntryCount { get; }
        public int GroundedEpisodeCount { get; }
        public double GroundedTimeSeconds { get; }
        public int AirborneEpisodeCount { get; }
        public double AirborneTimeSeconds { get; }
    }

    /// <summary>Unity-independent, allocation-free-per-step accumulator for one player's active race samples.</summary>
    public sealed class HandlingBakeOffAccumulator
    {
        public const float LowSpeedUpperBoundMetersPerSecond = 5f;
        public const float MediumSpeedUpperBoundMetersPerSecond = 12f;

        private readonly int[] turnRadiusSampleCounts = new int[6];
        private readonly double[] turnRadiusTotals = new double[6];
        private DirectionChangeTimer directionChangeTimer;
        private double directionChangeTotalTimeSeconds;
        private int activeSampleCount;
        private double activeSampleTimeSeconds;
        private int driftEntryCount;
        private double driftTimeSeconds;
        private double peakAbsoluteSlipDegrees;
        private double peakAbsoluteYawDegreesPerSecond;
        private int spinEntryCount;
        private bool wasDrifting;
        private bool wasSpinning;
        private bool hasGroundState;
        private bool wasGrounded;
        private int groundedEpisodeCount;
        private double groundedTimeSeconds;
        private int airborneEpisodeCount;
        private double airborneTimeSeconds;

        public void Reset()
        {
            Array.Clear(turnRadiusSampleCounts, 0, turnRadiusSampleCounts.Length);
            Array.Clear(turnRadiusTotals, 0, turnRadiusTotals.Length);
            directionChangeTimer.Reset();
            directionChangeTotalTimeSeconds = 0d;
            activeSampleCount = 0;
            activeSampleTimeSeconds = 0d;
            driftEntryCount = 0;
            driftTimeSeconds = 0d;
            peakAbsoluteSlipDegrees = 0d;
            peakAbsoluteYawDegreesPerSecond = 0d;
            spinEntryCount = 0;
            wasDrifting = false;
            wasSpinning = false;
            hasGroundState = false;
            wasGrounded = false;
            groundedEpisodeCount = 0;
            groundedTimeSeconds = 0d;
            airborneEpisodeCount = 0;
            airborneTimeSeconds = 0d;
        }

        public void Step(double deltaSeconds, float throttle, float steering, in HandlingMotionSample motion,
            bool isGrounded)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            ValidateFinite(throttle, nameof(throttle));
            ValidateFinite(steering, nameof(steering));
            ValidateFinite(motion.SignedLongitudinalSpeed, nameof(motion));
            ValidateFinite(motion.PlanarSpeed, nameof(motion));
            ValidateFinite(motion.SlipAngleDegrees, nameof(motion));
            ValidateFinite(motion.YawRateDegreesPerSecond, nameof(motion));

            activeSampleCount++;
            activeSampleTimeSeconds += deltaSeconds;

            if (directionChangeTimer.Step(deltaSeconds, throttle, motion.SignedLongitudinalSpeed,
                    out DirectionChangeMeasurement completed))
                directionChangeTotalTimeSeconds += completed.DurationSeconds;

            float turnRadius = motion.TurnRadius;
            if (Math.Abs(steering) >= HandlingMetrics.DefaultInputDeadZone &&
                !float.IsInfinity(turnRadius) && !float.IsNaN(turnRadius) &&
                motion.Direction != LongitudinalDirection.Stationary)
            {
                int radiusIndex = GetRadiusIndex(motion.Direction, ClassifySpeedBand(motion.PlanarSpeed));
                turnRadiusSampleCounts[radiusIndex]++;
                turnRadiusTotals[radiusIndex] += turnRadius;
            }

            if (motion.IsDrifting && !wasDrifting) driftEntryCount++;
            if (motion.IsDrifting) driftTimeSeconds += deltaSeconds;
            wasDrifting = motion.IsDrifting;

            if (motion.IsSpin && !wasSpinning) spinEntryCount++;
            wasSpinning = motion.IsSpin;
            peakAbsoluteSlipDegrees = Math.Max(peakAbsoluteSlipDegrees, Math.Abs(motion.SlipAngleDegrees));
            peakAbsoluteYawDegreesPerSecond = Math.Max(peakAbsoluteYawDegreesPerSecond,
                Math.Abs(motion.YawRateDegreesPerSecond));

            if (!hasGroundState || wasGrounded != isGrounded)
            {
                if (isGrounded) groundedEpisodeCount++;
                else airborneEpisodeCount++;
                hasGroundState = true;
                wasGrounded = isGrounded;
            }

            if (isGrounded) groundedTimeSeconds += deltaSeconds;
            else airborneTimeSeconds += deltaSeconds;
        }

        public HandlingBakeOffMetrics Capture()
        {
            return new HandlingBakeOffMetrics(
                activeSampleCount,
                activeSampleTimeSeconds,
                directionChangeTimer.CompletedCount,
                directionChangeTotalTimeSeconds,
                GetTurnRadiusAggregate(LongitudinalDirection.Forward, HandlingSpeedBand.Low),
                GetTurnRadiusAggregate(LongitudinalDirection.Forward, HandlingSpeedBand.Medium),
                GetTurnRadiusAggregate(LongitudinalDirection.Forward, HandlingSpeedBand.High),
                GetTurnRadiusAggregate(LongitudinalDirection.Reverse, HandlingSpeedBand.Low),
                GetTurnRadiusAggregate(LongitudinalDirection.Reverse, HandlingSpeedBand.Medium),
                GetTurnRadiusAggregate(LongitudinalDirection.Reverse, HandlingSpeedBand.High),
                driftEntryCount,
                driftTimeSeconds,
                peakAbsoluteSlipDegrees,
                peakAbsoluteYawDegreesPerSecond,
                spinEntryCount,
                groundedEpisodeCount,
                groundedTimeSeconds,
                airborneEpisodeCount,
                airborneTimeSeconds);
        }

        public static HandlingSpeedBand ClassifySpeedBand(float planarSpeedMetersPerSecond)
        {
            ValidateFinite(planarSpeedMetersPerSecond, nameof(planarSpeedMetersPerSecond));
            float speed = Math.Abs(planarSpeedMetersPerSecond);
            if (speed < LowSpeedUpperBoundMetersPerSecond) return HandlingSpeedBand.Low;
            return speed < MediumSpeedUpperBoundMetersPerSecond
                ? HandlingSpeedBand.Medium
                : HandlingSpeedBand.High;
        }

        private HandlingTurnRadiusAggregate GetTurnRadiusAggregate(LongitudinalDirection direction,
            HandlingSpeedBand speedBand)
        {
            int index = GetRadiusIndex(direction, speedBand);
            return new HandlingTurnRadiusAggregate(turnRadiusSampleCounts[index], turnRadiusTotals[index]);
        }

        private static int GetRadiusIndex(LongitudinalDirection direction, HandlingSpeedBand speedBand)
        {
            if (direction != LongitudinalDirection.Forward && direction != LongitudinalDirection.Reverse)
                throw new ArgumentOutOfRangeException(nameof(direction));
            return (direction == LongitudinalDirection.Reverse ? 3 : 0) + (int)speedBand;
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public sealed class HandlingBakeOffRow
    {
        public HandlingBakeOffRow(string runId, string createdUtc, string trackId, int playerNumber,
            string racerId, string deviceKind, string modelId, HandlingBakeOffMetrics metrics,
            int collisionCount, int finishPosition, int completedLaps, int totalLaps, double? finishTimeSeconds)
        {
            RunId = runId ?? string.Empty;
            CreatedUtc = createdUtc ?? string.Empty;
            TrackId = trackId ?? string.Empty;
            PlayerNumber = playerNumber;
            RacerId = racerId ?? string.Empty;
            DeviceKind = deviceKind ?? string.Empty;
            ModelId = modelId ?? string.Empty;
            Metrics = metrics;
            CollisionCount = collisionCount;
            FinishPosition = finishPosition;
            CompletedLaps = completedLaps;
            TotalLaps = totalLaps;
            FinishTimeSeconds = finishTimeSeconds;
        }

        public string RunId { get; }
        public string CreatedUtc { get; }
        public string TrackId { get; }
        public int PlayerNumber { get; }
        public string RacerId { get; }
        public string DeviceKind { get; }
        public string ModelId { get; }
        public HandlingBakeOffMetrics Metrics { get; }
        public int CollisionCount { get; }
        public int FinishPosition { get; }
        public int CompletedLaps { get; }
        public int TotalLaps { get; }
        public double? FinishTimeSeconds { get; }
        public double? AverageLapTimeSeconds => FinishTimeSeconds.HasValue && TotalLaps > 0
            ? FinishTimeSeconds.Value / TotalLaps
            : null;

        // Intentionally blank in automatic exports; players fill these after the controlled manual run.
        public double? ManualSlalomTimeSeconds { get; set; }
        public int? ManualSlalomConeContacts { get; set; }
        public int? PlayerRank { get; set; }
        public int? ConfidenceOneToFive { get; set; }
        public string ShortReason { get; set; }
    }

    public sealed class HandlingBakeOffReport
    {
        public HandlingBakeOffReport(string runId, string createdUtc, string trackId,
            IReadOnlyList<HandlingBakeOffRow> rows)
        {
            RunId = runId ?? throw new ArgumentNullException(nameof(runId));
            CreatedUtc = createdUtc ?? throw new ArgumentNullException(nameof(createdUtc));
            TrackId = trackId ?? throw new ArgumentNullException(nameof(trackId));
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        public string RunId { get; }
        public string CreatedUtc { get; }
        public string TrackId { get; }
        public IReadOnlyList<HandlingBakeOffRow> Rows { get; }
    }

    public static class HandlingBakeOffCsv
    {
        public const string Header =
            "run_id,created_utc,track_id,player_number,racer_id,device_kind,model_id," +
            "active_sample_count,active_sample_time_s,direction_change_count,direction_change_total_time_s," +
            "direction_change_average_time_s,forward_low_radius_sample_count,forward_low_radius_average_m," +
            "forward_medium_radius_sample_count,forward_medium_radius_average_m,forward_high_radius_sample_count," +
            "forward_high_radius_average_m,reverse_low_radius_sample_count,reverse_low_radius_average_m," +
            "reverse_medium_radius_sample_count,reverse_medium_radius_average_m,reverse_high_radius_sample_count," +
            "reverse_high_radius_average_m,drift_entry_count,drift_time_s,peak_abs_slip_deg," +
            "peak_abs_yaw_deg_s,spin_entry_count,grounded_episode_count,grounded_time_s," +
            "airborne_episode_count,airborne_time_s,collision_count,finish_position,completed_laps,total_laps," +
            "finish_time_s,average_lap_time_s,slalom_time_s,slalom_cone_contacts,player_rank," +
            "confidence_1_to_5,short_reason";

        public static string Format(HandlingBakeOffReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            StringBuilder csv = new StringBuilder(512 + report.Rows.Count * 512);
            csv.Append(Header).Append('\n');
            for (int i = 0; i < report.Rows.Count; i++) AppendRow(csv, report.Rows[i]);
            return csv.ToString();
        }

        private static void AppendRow(StringBuilder csv, HandlingBakeOffRow row)
        {
            HandlingBakeOffMetrics metrics = row.Metrics;
            Append(csv, row.RunId);
            Append(csv, row.CreatedUtc);
            Append(csv, row.TrackId);
            Append(csv, row.PlayerNumber);
            Append(csv, row.RacerId);
            Append(csv, row.DeviceKind);
            Append(csv, row.ModelId);
            Append(csv, metrics.ActiveSampleCount);
            Append(csv, metrics.ActiveSampleTimeSeconds);
            Append(csv, metrics.DirectionChangeCount);
            Append(csv, metrics.DirectionChangeTotalTimeSeconds);
            Append(csv, metrics.DirectionChangeAverageTimeSeconds);
            AppendRadius(csv, metrics.ForwardLow);
            AppendRadius(csv, metrics.ForwardMedium);
            AppendRadius(csv, metrics.ForwardHigh);
            AppendRadius(csv, metrics.ReverseLow);
            AppendRadius(csv, metrics.ReverseMedium);
            AppendRadius(csv, metrics.ReverseHigh);
            Append(csv, metrics.DriftEntryCount);
            Append(csv, metrics.DriftTimeSeconds);
            Append(csv, metrics.PeakAbsoluteSlipDegrees);
            Append(csv, metrics.PeakAbsoluteYawDegreesPerSecond);
            Append(csv, metrics.SpinEntryCount);
            Append(csv, metrics.GroundedEpisodeCount);
            Append(csv, metrics.GroundedTimeSeconds);
            Append(csv, metrics.AirborneEpisodeCount);
            Append(csv, metrics.AirborneTimeSeconds);
            Append(csv, row.CollisionCount);
            Append(csv, row.FinishPosition);
            Append(csv, row.CompletedLaps);
            Append(csv, row.TotalLaps);
            Append(csv, row.FinishTimeSeconds);
            Append(csv, row.AverageLapTimeSeconds);
            Append(csv, row.ManualSlalomTimeSeconds);
            Append(csv, row.ManualSlalomConeContacts);
            Append(csv, row.PlayerRank);
            Append(csv, row.ConfidenceOneToFive);
            csv.Append(Escape(row.ShortReason)).Append('\n');
        }

        private static void AppendRadius(StringBuilder csv, HandlingTurnRadiusAggregate radius)
        {
            Append(csv, radius.SampleCount);
            Append(csv, radius.AverageMeters);
        }

        private static void Append(StringBuilder csv, string value) => csv.Append(Escape(value)).Append(',');
        private static void Append(StringBuilder csv, int value) =>
            csv.Append(value.ToString(CultureInfo.InvariantCulture)).Append(',');
        private static void Append(StringBuilder csv, int? value) =>
            csv.Append(value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty).Append(',');
        private static void Append(StringBuilder csv, double value) =>
            csv.Append(value.ToString("0.######", CultureInfo.InvariantCulture)).Append(',');
        private static void Append(StringBuilder csv, double? value) =>
            csv.Append(value.HasValue ? value.Value.ToString("0.######", CultureInfo.InvariantCulture) : string.Empty)
                .Append(',');

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
            return '"' + value.Replace("\"", "\"\"") + '"';
        }
    }
}
