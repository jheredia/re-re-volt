using System.Collections;
using Core;
using NUnit.Framework;
using Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.TestTools;
using Vehicle;

namespace Tests.PlayMode
{
    public sealed class TrackSelectionIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private RaceManager race;
        private VehicleManager roster;
        private PrototypeCircuitBuilder.Result circuit;
        private Stage3RacePresentation presentation;
        private TrackSession tracks;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            root = new GameObject("TrackSelectionIntegrationTest");
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, PrototypeCircuitBuilder.OrderedCheckpointCount), Is.True);
            circuit = PrototypeCircuitBuilder.Build(race, root.transform);
            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, circuit.Grid), Is.True);
            roster.PlayerJoined += player => player.Recovery.ConfigureGroundMask(circuit.DrivableMask);
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
            circuit?.Dispose();
            Assert.That(InputUser.all.Count, Is.Zero);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator OnlyPairedKeyboardAndControllerCommandsNavigateAndUnavailableTrackCannotStart()
        {
            tracks = CreateSession(includeUnavailable: true);
            ConfigurePresentation();
            Gamepad outsider = InputSystem.AddDevice<Gamepad>();
            yield return PressAndRelease(outsider, GamepadButton.West);
            Assert.That(tracks.SelectedTrackId, Is.EqualTo("track-one"));

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(roster.TryJoinKeyboard(keyboard, out _), Is.True);
            yield return PressAndRelease(keyboard, Key.Space);
            Assert.That(tracks.SelectedTrackId, Is.EqualTo("unavailable"));
            Assert.That(presentation.SelectedTrackAvailable, Is.False);
            Assert.That(presentation.LobbyText, Does.Contain("UNAVAILABLE"));

            yield return PressAndRelease(keyboard, Key.Enter);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Idle));
            Assert.That(presentation.LobbyText, Does.Contain("START LOCKED"));

            yield return PressAndRelease(keyboard, Key.Space);
            Assert.That(tracks.SelectedTrackId, Is.EqualTo("track-two"));
            Gamepad owner = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(owner, out _), Is.True);
            yield return PressAndRelease(owner, GamepadButton.West);
            Assert.That(tracks.SelectedTrackId, Is.EqualTo("track-one"),
                "A connected sibling owner may navigate the shared track row.");

            yield return PressAndRelease(owner, GamepadButton.South);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
        }

        [UnityTest]
        public IEnumerator RematchRetainsTrackAndLobbyReturnAllowsChangingIt()
        {
            tracks = CreateSession(includeUnavailable: false);
            ConfigurePresentation();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer player), Is.True);
            yield return PressAndRelease(pad, GamepadButton.West);
            Assert.That(tracks.SelectedTrackId, Is.EqualTo("track-two"));

            yield return PressAndRelease(pad, GamepadButton.South);
            yield return WaitForRace();
            FinishRace(player);
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Results));

            yield return PressAndRelease(pad, GamepadButton.South);
            Assert.That(tracks.SelectedTrackId, Is.EqualTo("track-two"));
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            yield return WaitForRace();
            FinishRace(player);

            yield return PressAndRelease(pad, GamepadButton.East);
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Setup));
            Assert.That(tracks.SelectedTrackId, Is.EqualTo("track-two"));
            yield return PressAndRelease(pad, GamepadButton.West);
            Assert.That(tracks.SelectedTrackId, Is.EqualTo("track-one"));
        }

        private void ConfigurePresentation()
        {
            presentation = root.AddComponent<Stage3RacePresentation>();
            presentation.Configure(race, roster, trackSession: tracks,
                selectTrackCommand: direction => tracks.TryCycle(direction));
            Physics.SyncTransforms();
        }

        private static TrackSession CreateSession(bool includeUnavailable)
        {
            TrackRuntimeBuilder availableBuilder = (race, parent, style) =>
                throw new System.InvalidOperationException("UI contract tests do not build content.");
            TrackDefinition first = new("track-one", "TRACK ONE", "FAST FLOW", "PREVIEW ONE", 3,
                PrototypeCircuitBuilder.OrderedCheckpointCount, availableBuilder);
            TrackDefinition second = new("track-two", "TRACK TWO", "TECHNICAL FLOW", "PREVIEW TWO", 3,
                PrototypeCircuitBuilder.OrderedCheckpointCount, availableBuilder);
            return includeUnavailable
                ? new TrackSession(new[]
                {
                    first,
                    new TrackDefinition("unavailable", "TRACK PENDING", "FLOW PENDING", "PREVIEW PENDING", 4,
                        PrototypeCircuitBuilder.OrderedCheckpointCount, null),
                    second
                })
                : new TrackSession(new[] { first, second });
        }

        private void FinishRace(LocalPlayer player)
        {
            for (int lap = 0; lap < race.TotalLaps; lap++)
                for (int checkpoint = 0; checkpoint < race.CheckpointCount; checkpoint++)
                    Assert.That(race.ReportCheckpoint(player.Racer.RacerId, checkpoint), Is.True);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
        }

        private IEnumerator WaitForRace()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);
        }

        private static IEnumerator PressAndRelease(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return Frames();
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Frames();
        }

        private static IEnumerator PressAndRelease(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames();
        }

        private static IEnumerator Frames()
        {
            yield return null;
            yield return null;
        }
    }
}
