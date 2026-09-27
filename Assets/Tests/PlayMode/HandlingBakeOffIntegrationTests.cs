using System.Collections;
using System.Collections.Generic;
using System.IO;
using Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.TestTools;
using Vehicle;

namespace Tests.PlayMode
{
    public sealed class HandlingBakeOffIntegrationTests : InputTestFixture
    {
        private sealed class CapturingSink : IHandlingBakeOffReportSink
        {
            public readonly List<HandlingBakeOffReport> Reports = new();

            public string Export(HandlingBakeOffReport report)
            {
                Reports.Add(report);
                return "memory://" + report.RunId;
            }
        }

        private sealed class ThrowingSink : IHandlingBakeOffReportSink
        {
            public int Attempts { get; private set; }

            public string Export(HandlingBakeOffReport report)
            {
                Attempts++;
                throw new IOException("test export failure");
            }
        }

        private GameObject root;
        private RaceManager race;
        private VehicleManager roster;
        private HandlingBakeOffCoordinator coordinator;
        private CapturingSink sink;
        private Keyboard keyboard;
        private Gamepad gamepad;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            root = new GameObject("HandlingBakeOffIntegrationTest");
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(1, 1), Is.True);
            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, null), Is.True);
            sink = new CapturingSink();
            coordinator = root.AddComponent<HandlingBakeOffCoordinator>();
            coordinator.Configure(race, roster, sink);
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator CompletionExportsOnceWithIdentityAndRematchStartsFreshRun()
        {
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer keyboardPlayer), Is.True);
            Assert.That(roster.TryJoinGamepad(gamepad, out LocalPlayer gamepadPlayer), Is.True);
            Assert.That(roster.TryCycleHandlingModel(keyboardPlayer.PlayerIndex, 1), Is.True);
            Assert.That(roster.TryCycleHandlingModel(gamepadPlayer.PlayerIndex, -1), Is.True);

            Assert.That(race.StartRace(), Is.True);
            yield return WaitForRacing();
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            Assert.That(sink.Reports, Is.Empty, "An active race must not export before completion.");

            Assert.That(race.ReportCheckpoint(keyboardPlayer.Racer.RacerId, 0), Is.True);
            Assert.That(sink.Reports, Is.Empty, "Finishing one of two players must not export the run.");
            Assert.That(race.ReportCheckpoint(gamepadPlayer.Racer.RacerId, 0), Is.True);
            Assert.That(sink.Reports.Count, Is.EqualTo(1));
            Assert.That(coordinator.ExportedRunCount, Is.EqualTo(1));
            Assert.That(coordinator.IsRunActive, Is.False);
            Assert.That(race.ReportCheckpoint(gamepadPlayer.Racer.RacerId, 0), Is.False);
            Assert.That(sink.Reports.Count, Is.EqualTo(1), "Repeated finish input must not duplicate an export.");

            HandlingBakeOffReport first = sink.Reports[0];
            Assert.That(first.Rows.Count, Is.EqualTo(2));
            AssertRow(first.Rows[0], 1, "KEYBOARD", "MODEL B");
            AssertRow(first.Rows[1], 2, "GAMEPAD", "MODEL D");
            Assert.That(first.Rows[0].Metrics.ActiveSampleCount, Is.GreaterThan(0));
            int firstRunSamples = first.Rows[0].Metrics.ActiveSampleCount;

            race.ResetRace();
            Assert.That(sink.Reports.Count, Is.EqualTo(1), "Returning to idle must not export.");
            Assert.That(race.StartRace(), Is.True);
            yield return WaitForRacing();
            Assert.That(coordinator.IsRunActive, Is.True);
            Assert.That(race.ReportCheckpoint(keyboardPlayer.Racer.RacerId, 0), Is.True);
            Assert.That(race.ReportCheckpoint(gamepadPlayer.Racer.RacerId, 0), Is.True);

            Assert.That(sink.Reports.Count, Is.EqualTo(2));
            Assert.That(coordinator.ExportedRunCount, Is.EqualTo(2));
            HandlingBakeOffReport second = sink.Reports[1];
            Assert.That(second.RunId, Is.Not.EqualTo(first.RunId));
            Assert.That(second.Rows[0].Metrics.ActiveSampleCount, Is.LessThan(firstRunSamples),
                "Rematch metrics must begin from an empty accumulator.");
            AssertRow(second.Rows[0], 1, "KEYBOARD", "MODEL B");
            AssertRow(second.Rows[1], 2, "GAMEPAD", "MODEL D");
        }

        [UnityTest]
        public IEnumerator SinkFailureIsContainedAndRaceStillFinishesWithoutRetrying()
        {
            ThrowingSink throwingSink = new ThrowingSink();
            coordinator.Configure(race, roster, throwingSink);
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer player), Is.True);
            Assert.That(race.StartRace(), Is.True);
            yield return WaitForRacing();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "\\[HandlingBakeOff\\] Could not export run .*: test export failure"));
            Assert.That(race.ReportCheckpoint(player.Racer.RacerId, 0), Is.True);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
            Assert.That(throwingSink.Attempts, Is.EqualTo(1));
            Assert.That(coordinator.ExportedRunCount, Is.Zero);
            Assert.That(coordinator.IsRunActive, Is.False);
            Assert.That(race.ReportCheckpoint(player.Racer.RacerId, 0), Is.False);
            Assert.That(throwingSink.Attempts, Is.EqualTo(1));
        }

        private static void AssertRow(HandlingBakeOffRow row, int playerNumber, string deviceKind, string modelId)
        {
            Assert.That(row.PlayerNumber, Is.EqualTo(playerNumber));
            Assert.That(row.DeviceKind, Is.EqualTo(deviceKind));
            Assert.That(row.ModelId, Is.EqualTo(modelId));
            Assert.That(row.FinishPosition, Is.InRange(1, 2));
            Assert.That(row.CompletedLaps, Is.EqualTo(1));
            Assert.That(row.TotalLaps, Is.EqualTo(1));
            Assert.That(row.FinishTimeSeconds, Is.Not.Null);
            Assert.That(row.ManualSlalomTimeSeconds, Is.Null);
            Assert.That(row.ManualSlalomConeContacts, Is.Null);
            Assert.That(row.PlayerRank, Is.Null);
            Assert.That(row.ConfidenceOneToFive, Is.Null);
            Assert.That(row.ShortReason, Is.Null);
        }

        private IEnumerator WaitForRacing()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True, "Countdown must enter Racing within 10 seconds.");
        }
    }
}
