using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Vehicle;

namespace Tests.EditMode
{
    public sealed class HandlingBakeOffReportTests
    {
        [Test]
        public void AccumulatorMeasuresTransitionsBandsDriftSpinAndGroundEpisodes()
        {
            HandlingBakeOffAccumulator accumulator = new HandlingBakeOffAccumulator();
            accumulator.Step(.1d, -1f, .5f, Sample(4f, 90f, false, false), true);
            accumulator.Step(.1d, -1f, -.5f, Sample(-8f, 45f, true, true), false);
            accumulator.Step(.1d, 1f, .5f, Sample(-13f, 30f, false, false), true);
            accumulator.Step(.1d, 1f, -.5f, Sample(13f, 60f, false, false), true);

            HandlingBakeOffMetrics metrics = accumulator.Capture();
            Assert.That(metrics.ActiveSampleCount, Is.EqualTo(4));
            Assert.That(metrics.ActiveSampleTimeSeconds, Is.EqualTo(.4d).Within(.000001d));
            Assert.That(metrics.DirectionChangeCount, Is.EqualTo(2));
            Assert.That(metrics.DirectionChangeTotalTimeSeconds, Is.EqualTo(.4d).Within(.000001d));
            Assert.That(metrics.DirectionChangeAverageTimeSeconds, Is.EqualTo(.2d).Within(.000001d));
            Assert.That(metrics.ForwardLow.SampleCount, Is.EqualTo(1));
            Assert.That(metrics.ReverseMedium.SampleCount, Is.EqualTo(1));
            Assert.That(metrics.ReverseHigh.SampleCount, Is.EqualTo(1));
            Assert.That(metrics.ForwardHigh.SampleCount, Is.EqualTo(1));
            Assert.That(metrics.ForwardLow.AverageMeters, Is.EqualTo(4d / (Math.PI / 2d)).Within(.0001d));
            Assert.That(metrics.DriftEntryCount, Is.EqualTo(1));
            Assert.That(metrics.DriftTimeSeconds, Is.EqualTo(.1d).Within(.000001d));
            Assert.That(metrics.PeakAbsoluteSlipDegrees, Is.EqualTo(80d));
            Assert.That(metrics.PeakAbsoluteYawDegreesPerSecond, Is.EqualTo(200d));
            Assert.That(metrics.SpinEntryCount, Is.EqualTo(1));
            Assert.That(metrics.GroundedEpisodeCount, Is.EqualTo(2));
            Assert.That(metrics.GroundedTimeSeconds, Is.EqualTo(.3d).Within(.000001d));
            Assert.That(metrics.AirborneEpisodeCount, Is.EqualTo(1));
            Assert.That(metrics.AirborneTimeSeconds, Is.EqualTo(.1d).Within(.000001d));
        }

        [Test]
        public void ResetClearsPreviousRunAndSpeedBandBoundariesAreStable()
        {
            HandlingBakeOffAccumulator accumulator = new HandlingBakeOffAccumulator();
            accumulator.Step(.02d, 1f, .5f, Sample(4f, 30f, true, true), false);
            accumulator.Reset();

            HandlingBakeOffMetrics metrics = accumulator.Capture();
            Assert.That(metrics.ActiveSampleCount, Is.Zero);
            Assert.That(metrics.DriftEntryCount, Is.Zero);
            Assert.That(metrics.SpinEntryCount, Is.Zero);
            Assert.That(metrics.AirborneEpisodeCount, Is.Zero);
            Assert.That(HandlingBakeOffAccumulator.ClassifySpeedBand(4.999f), Is.EqualTo(HandlingSpeedBand.Low));
            Assert.That(HandlingBakeOffAccumulator.ClassifySpeedBand(5f), Is.EqualTo(HandlingSpeedBand.Medium));
            Assert.That(HandlingBakeOffAccumulator.ClassifySpeedBand(11.999f), Is.EqualTo(HandlingSpeedBand.Medium));
            Assert.That(HandlingBakeOffAccumulator.ClassifySpeedBand(12f), Is.EqualTo(HandlingSpeedBand.High));
        }

        [Test]
        public void TurnRadiusRequiresIntentionalSteeringAndIncludesLeftAndRightSymmetrically()
        {
            HandlingBakeOffAccumulator accumulator = new HandlingBakeOffAccumulator();
            HandlingMotionSample leftYaw = Sample(4f, -90f, false, false);
            HandlingMotionSample rightYaw = Sample(4f, 90f, false, false);

            accumulator.Step(.02d, 1f, 0f, rightYaw, true);
            accumulator.Step(.02d, 1f, HandlingMetrics.DefaultInputDeadZone * .99f, rightYaw, true);
            Assert.That(accumulator.Capture().ForwardLow.SampleCount, Is.Zero,
                "Incidental yaw without deliberate steering must not contaminate radius averages.");

            accumulator.Step(.02d, 1f, -HandlingMetrics.DefaultInputDeadZone, leftYaw, true);
            HandlingTurnRadiusAggregate afterLeft = accumulator.Capture().ForwardLow;
            accumulator.Step(.02d, 1f, HandlingMetrics.DefaultInputDeadZone, rightYaw, true);
            HandlingTurnRadiusAggregate afterBoth = accumulator.Capture().ForwardLow;

            Assert.That(afterLeft.SampleCount, Is.EqualTo(1));
            Assert.That(afterBoth.SampleCount, Is.EqualTo(2));
            Assert.That(afterBoth.AverageMeters, Is.EqualTo(afterLeft.AverageMeters).Within(.000001d));
        }

        [Test]
        public void CsvIsInvariantStableAndLeavesManualFieldsBlank()
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("es-CL");
                HandlingBakeOffReport report = CreateReport();
                string csv = HandlingBakeOffCsv.Format(report);
                string[] lines = csv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string[] headings = lines[0].Split(',');
                string[] values = lines[1].Split(',');

                Assert.That(lines[0], Is.EqualTo(HandlingBakeOffCsv.Header));
                Assert.That(values.Length, Is.EqualTo(headings.Length));
                Assert.That(Value(headings, values, "finish_time_s"), Is.EqualTo("12.5"));
                Assert.That(Value(headings, values, "average_lap_time_s"), Is.EqualTo("4.166667"));
                Assert.That(Value(headings, values, "slalom_time_s"), Is.Empty);
                Assert.That(Value(headings, values, "slalom_cone_contacts"), Is.Empty);
                Assert.That(Value(headings, values, "player_rank"), Is.Empty);
                Assert.That(Value(headings, values, "confidence_1_to_5"), Is.Empty);
                Assert.That(Value(headings, values, "short_reason"), Is.Empty);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [Test]
        public void CsvEscapesPlayerReasonWithoutChangingColumnContract()
        {
            HandlingBakeOffReport report = CreateReport();
            report.Rows[0].ShortReason = "tight, \"predictable\"\nrecovery";
            string csv = HandlingBakeOffCsv.Format(report);

            Assert.That(csv, Does.Contain("\"tight, \"\"predictable\"\"\nrecovery\""));
            Assert.That(csv, Does.StartWith(HandlingBakeOffCsv.Header + "\n"));
        }

        [Test]
        public void DesktopFileSinkWritesFortyFourColumnsAndDoesNotOverwriteExistingReport()
        {
            string runId = "sink-smoke-" + Guid.NewGuid().ToString("N");
            string directory = Path.Combine(UnityEngine.Application.persistentDataPath, "HandlingBakeOff");
            string filePrefix = "handling-bakeoff-" + runId;
            bool directoryExisted = Directory.Exists(directory);

            try
            {
                HandlingBakeOffReport report = CreateReport(runId);
                FileHandlingBakeOffReportSink sink = new FileHandlingBakeOffReportSink();

                string firstPath = sink.Export(report);
                Assert.That(Path.GetFullPath(Path.GetDirectoryName(firstPath)),
                    Is.EqualTo(Path.GetFullPath(directory)));
                Assert.That(File.Exists(firstPath), Is.True);

                string firstContents = File.ReadAllText(firstPath);
                AssertPersistedReport(firstContents, runId);

                string secondPath = sink.Export(report);
                Assert.That(secondPath, Is.Not.EqualTo(firstPath));
                Assert.That(Path.GetFullPath(Path.GetDirectoryName(secondPath)),
                    Is.EqualTo(Path.GetFullPath(directory)));
                Assert.That(File.Exists(secondPath), Is.True);
                Assert.That(File.ReadAllText(firstPath), Is.EqualTo(firstContents),
                    "A repeated export must leave the original report unchanged.");
                AssertPersistedReport(File.ReadAllText(secondPath), runId);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    foreach (string path in Directory.GetFiles(directory, filePrefix + "*.csv"))
                        File.Delete(path);

                    if (!directoryExisted && Directory.GetFileSystemEntries(directory).Length == 0)
                        Directory.Delete(directory);
                }
            }
        }

        private static HandlingMotionSample Sample(float signedSpeed, float yaw, bool drift, bool spin)
        {
            return new HandlingMotionSample(signedSpeed, 0f, Math.Abs(signedSpeed), spin ? 80f : 5f,
                spin ? 200f : yaw, drift, spin);
        }

        private static HandlingBakeOffReport CreateReport(string runId = "run-1")
        {
            HandlingBakeOffMetrics metrics = new HandlingBakeOffMetrics(
                1, .02d, 0, 0d,
                default, default, default, default, default, default,
                0, 0d, 0d, 0d, 0, 1, .02d, 0, 0d);
            HandlingBakeOffRow row = new HandlingBakeOffRow(
                runId, "2026-09-27T12:00:00.000Z", "WORKBENCH_CIRCUIT", 1,
                "player-1", "KEYBOARD", "MODEL A", metrics, 2, 1, 3, 3, 12.5d);
            return new HandlingBakeOffReport(row.RunId, row.CreatedUtc, row.TrackId,
                new[] { row });
        }

        private static void AssertPersistedReport(string csv, string expectedRunId)
        {
            string[] lines = csv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines, Has.Length.EqualTo(2));

            string[] headings = lines[0].Split(',');
            string[] values = lines[1].Split(',');
            Assert.That(lines[0], Is.EqualTo(HandlingBakeOffCsv.Header));
            Assert.That(headings, Has.Length.EqualTo(44));
            Assert.That(values, Has.Length.EqualTo(44));
            Assert.That(Value(headings, values, "run_id"), Is.EqualTo(expectedRunId));
            Assert.That(Value(headings, values, "created_utc"), Is.EqualTo("2026-09-27T12:00:00.000Z"));
            Assert.That(Value(headings, values, "track_id"), Is.EqualTo("WORKBENCH_CIRCUIT"));
            Assert.That(Value(headings, values, "player_number"), Is.EqualTo("1"));
            Assert.That(Value(headings, values, "racer_id"), Is.EqualTo("player-1"));
            Assert.That(Value(headings, values, "device_kind"), Is.EqualTo("KEYBOARD"));
            Assert.That(Value(headings, values, "model_id"), Is.EqualTo("MODEL A"));
            Assert.That(Value(headings, values, "finish_time_s"), Is.EqualTo("12.5"));
        }

        private static string Value(string[] headings, string[] values, string heading)
        {
            return values[Array.IndexOf(headings, heading)];
        }
    }
}
