using System.Collections;
using System.Collections.Generic;
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
    public sealed class Stage9DemoFlowIntegrationTests : InputTestFixture
    {
        private sealed class MemoryStore : ILocalPreferenceStore
        {
            private readonly Dictionary<string, string> values = new();

            public bool TryRead(string key, out string value) => values.TryGetValue(key, out value);
            public void Write(string key, string value) => values[key] = value;
        }

        private GameObject root;
        private RaceManager race;
        private VehicleManager roster;
        private Stage3RacePresentation presentation;
        private PrototypeCircuitBuilder.Result circuit;
        private DemoFlowPreferences preferences;
        private MemoryStore store;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            root = new GameObject("Stage9DemoFlowTest");
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, PrototypeCircuitBuilder.OrderedCheckpointCount), Is.True);
            circuit = PrototypeCircuitBuilder.Build(race, root.transform);
            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, circuit.Grid), Is.True);
            roster.PlayerJoined += player => player.Recovery.ConfigureGroundMask(circuit.DrivableMask);
            store = new MemoryStore();
            preferences = new DemoFlowPreferences(store);
            preferences.Load();
            presentation = root.AddComponent<Stage3RacePresentation>();
            presentation.Configure(race, roster, preferenceSource: preferences);
            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
            circuit?.Dispose();
            Assert.That(InputUser.all.Count, Is.Zero, "Demo-flow cleanup must release every local input owner.");
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator TitleSetupAndSlotPreferencesRemainOwnedAcrossOneToFourPlayers()
        {
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Title));
            Assert.That(presentation.TitleText, Is.EqualTo("AFTER-HOURS RC CLUB"));
            Assert.That(presentation.LobbyText, Does.Contain("JOIN WITH KEYBOARD"));

            HandlingModelId[] models =
                { HandlingModelId.D, HandlingModelId.C, HandlingModelId.B, HandlingModelId.A };
            VehicleFamilyId[] families =
                { VehicleFamilyId.Drift, VehicleFamilyId.RcTruck, VehicleFamilyId.Buggy, VehicleFamilyId.Touring };
            for (int i = 0; i < 4; i++)
                Assert.That(preferences.SetPlayerSelection(i, models[i], families[i]), Is.True);

            Gamepad[] pads = new Gamepad[4];
            for (int i = 0; i < pads.Length; i++)
            {
                pads[i] = InputSystem.AddDevice<Gamepad>();
                Assert.That(roster.TryJoinGamepad(pads[i], out LocalPlayer player), Is.True);
                Assert.That(player.PlayerIndex, Is.EqualTo(i));
                Assert.That(player.Vehicle.SelectedHandlingModel, Is.EqualTo(models[i]));
                Assert.That(player.FamilyVisual.FamilyId, Is.EqualTo(families[i]));
            }
            yield return Frames();

            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Setup));
            Assert.That(presentation.TitleText, Is.EqualTo("SET UP YOUR TEST RUN"));
            Assert.That(presentation.HudCount, Is.EqualTo(4));
            Assert.That(roster.Players, Has.Count.EqualTo(4));
            for (int i = 0; i < roster.Players.Count; i++)
            {
                Viewport viewport = LocalPlayerRules.GetViewport(4, i);
                Assert.That(roster.Players[i].Camera.rect,
                    Is.EqualTo(new Rect(viewport.X, viewport.Y, viewport.Width, viewport.Height)));
            }

            for (int i = 3; i >= 0; i--) Assert.That(roster.TryLeavePlayer(i), Is.True);
            yield return Frames();
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Title));
            Assert.That(presentation.HudCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OwnedLobbyNavigationPersistsWithoutChangingRaceAuthority()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer player), Is.True);
            yield return Frames();

            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown));
            yield return Frames();
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Frames();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return Frames();
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Frames();

            Assert.That(player.FamilyVisual.FamilyId, Is.EqualTo(VehicleFamilyId.Buggy));
            Assert.That(player.Vehicle.SelectedHandlingModel, Is.EqualTo(HandlingModelId.B));
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Idle));
            Assert.That(race.Racers, Has.Count.EqualTo(1));

            DemoFlowPreferences reloaded = new(store);
            reloaded.Load();
            Assert.That(reloaded.GetVehicleFamily(0), Is.EqualTo(VehicleFamilyId.Buggy));
            Assert.That(reloaded.GetHandlingModel(0), Is.EqualTo(HandlingModelId.B));
        }

        [UnityTest]
        public IEnumerator PauseDisconnectResultsAndLobbyReturnExposeClearRecoveryStates()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer player), Is.True);
            Assert.That(race.StartRace(), Is.True);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Race));

            yield return PressAndRelease(pad, GamepadButton.Start);
            Assert.That(presentation.IsPaused, Is.True);
            Assert.That(presentation.ModalHeadingText, Is.EqualTo("RACE PAUSED / SETTINGS"));
            Assert.That(presentation.ModalFooterText, Does.Contain("CHANGE DISPLAY STYLE"));
            yield return PressAndRelease(pad, GamepadButton.Start);
            Assert.That(presentation.IsPaused, Is.False);

            InputSystem.RemoveDevice(pad);
            yield return Frames();
            Assert.That(player.IsConnected, Is.False);
            Assert.That(presentation.IsPaused, Is.True);
            Assert.That(presentation.ModalHeadingText, Is.EqualTo("INPUT CHECK"));
            Assert.That(presentation.ModalBodyText, Does.Contain("PLAYER 1 INPUT DEVICE DISCONNECTED"));
            InputSystem.AddDevice(pad);
            yield return Frames();
            Assert.That(player.IsConnected, Is.True);
            Assert.That(presentation.IsPaused, Is.False);

            for (int lap = 0; lap < race.TotalLaps; lap++)
                for (int checkpoint = 0; checkpoint < race.CheckpointCount; checkpoint++)
                    Assert.That(race.ReportCheckpoint(player.Racer.RacerId, checkpoint), Is.True);
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Results));
            Assert.That(presentation.ModalHeadingText, Is.EqualTo("FINAL BOARD"));

            yield return PressAndRelease(pad, GamepadButton.East);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Idle));
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Setup));
            Assert.That(roster.Players, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator KeyboardPauseSettingsUseExistingOwnedCommandsAndPersist()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer player), Is.True);
            yield return ExercisePauseSettings(player, keyboard);
        }

        [UnityTest]
        public IEnumerator GamepadPauseSettingsUseExistingOwnedCommandsAndPersist()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer player), Is.True);
            yield return ExercisePauseSettings(player, pad);
        }

        private IEnumerator ExercisePauseSettings(LocalPlayer player, InputDevice device)
        {
            Gamepad outsider = InputSystem.AddDevice<Gamepad>();
            Gamepad sibling = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(sibling, out _), Is.True);
            Assert.That(race.StartRace(), Is.True);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);
            yield return SettingsCommand(device, Key.Escape, GamepadButton.Start);
            Assert.That(presentation.IsPaused, Is.True);
            Assert.That(presentation.ModalBodyText, Does.Contain("> CAMERA MOTION  FULL"));
            Assert.That(presentation.ModalFooterText, Does.Contain("ENTER / A / CROSS"));
            Assert.That(presentation.ModalFooterText, Does.Contain("CYCLE VALUE, THEN NEXT ROW"));
            Assert.That(presentation.ModalFooterText, Does.Contain("OFF / REDUCED / FULL"));
            Assert.That(presentation.ModalFooterText, Does.Not.Contain("SPACE / X / SQUARE"));
            Vector3 position = player.Vehicle.transform.position;
            int laps = player.Racer.CompletedLaps;
            HandlingModelId model = player.Vehicle.SelectedHandlingModel;
            VehicleFamilyId family = player.FamilyVisual.FamilyId;

            yield return PressAndRelease(outsider, GamepadButton.South);
            yield return PressAndRelease(outsider, GamepadButton.West);
            Assert.That(presentation.ModalBodyText, Does.Contain("> CAMERA MOTION  FULL"));

            int fireRequests = 0;
            roster.FireRequested += _ => fireRequests++;
            yield return SettingsCommand(device, Key.Space, GamepadButton.West);
            Assert.That(fireRequests, Is.Zero, "Gameplay Fire remains suppressed during manual pause.");
            Assert.That(presentation.ModalBodyText, Does.Contain("> CAMERA MOTION  FULL"));
            if (device is Gamepad pad)
            {
                yield return PressAndRelease(pad, GamepadButton.East);
                Assert.That(presentation.IsPaused, Is.True, "Back retains its existing manual-pause behavior.");
                Assert.That(presentation.ModalBodyText, Does.Contain("> CAMERA MOTION  FULL"));
            }

            string[] rows = { "CAMERA MOTION", "VEHICLE MOTION", "VFX DENSITY" };
            string[] levels = { "OFF", "REDUCED", "FULL", "OFF" };
            PresentationResponseLevel[] responseLevels = { PresentationResponseLevel.Off,
                PresentationResponseLevel.Reduced, PresentationResponseLevel.Full, PresentationResponseLevel.Off };
            VfxDensityLevel[] densityLevels = { VfxDensityLevel.Off,
                VfxDensityLevel.Reduced, VfxDensityLevel.Full, VfxDensityLevel.Off };
            PresentationResponseLevel expectedCamera = PresentationResponseLevel.Full;
            PresentationResponseLevel expectedVehicle = PresentationResponseLevel.Full;
            VfxDensityLevel expectedVfx = VfxDensityLevel.Full;
            for (int round = 0; round < levels.Length; round++)
            {
                for (int row = 0; row < rows.Length; row++)
                {
                    Assert.That(presentation.ModalBodyText, Does.Contain($"> {rows[row]}  "));
                    yield return SettingsCommand(device, Key.Enter, GamepadButton.South);
                    Assert.That(presentation.ModalBodyText, Does.Contain($"  {rows[row]}  {levels[round]}"));
                    Assert.That(presentation.ModalBodyText, Does.Contain($"> {rows[(row + 1) % rows.Length]}  "));
                    if (row == 0)
                        expectedCamera = responseLevels[round];
                    else if (row == 1)
                        expectedVehicle = responseLevels[round];
                    else
                        expectedVfx = densityLevels[round];
                    Assert.That(presentation.CameraMotionSetting, Is.EqualTo(expectedCamera));
                    Assert.That(presentation.VehicleMotionSetting, Is.EqualTo(expectedVehicle));
                    Assert.That(presentation.VfxDensitySetting, Is.EqualTo(expectedVfx));
                    Assert.That(player.PresentationResponse.CameraLevel, Is.EqualTo(expectedCamera));
                    Assert.That(player.PresentationResponse.VehicleLevel, Is.EqualTo(expectedVehicle));
                    DemoFlowPreferences saved = new(store);
                    saved.Load();
                    Assert.That(saved.CameraMotion, Is.EqualTo(presentation.CameraMotionSetting));
                    Assert.That(saved.VehicleMotion, Is.EqualTo(presentation.VehicleMotionSetting));
                    Assert.That(saved.VfxDensity, Is.EqualTo(presentation.VfxDensitySetting));
                    Assert.That(presentation.IsPaused, Is.True);
                    Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Racing));
                    Assert.That(Time.timeScale, Is.Zero);
                    Assert.That(player.Vehicle.transform.position, Is.EqualTo(position));
                    Assert.That(player.Racer.CompletedLaps, Is.EqualTo(laps));
                }
            }
            Assert.That(presentation.ModalBodyText, Does.Contain("> CAMERA MOTION  OFF"), "Selection wraps.");
            DemoFlowPreferences reloaded = new(store);
            reloaded.Load();
            Assert.That(reloaded.CameraMotion, Is.EqualTo(PresentationResponseLevel.Off));
            Assert.That(reloaded.VehicleMotion, Is.EqualTo(PresentationResponseLevel.Off));
            Assert.That(reloaded.VfxDensity, Is.EqualTo(VfxDensityLevel.Off));
            Assert.That(player.PresentationResponse.CameraLevel, Is.EqualTo(PresentationResponseLevel.Off));
            Assert.That(player.PresentationResponse.VehicleLevel, Is.EqualTo(PresentationResponseLevel.Off));
            Assert.That(player.Vehicle.SelectedHandlingModel, Is.EqualTo(model));
            Assert.That(player.FamilyVisual.FamilyId, Is.EqualTo(family));

            yield return SettingsCommand(device, Key.Escape, GamepadButton.Start);
            Assert.That(presentation.IsPaused, Is.False, "Escape remains resume, even though it also maps Back.");
            yield return SettingsCommand(device, Key.Space, GamepadButton.West);
            yield return SettingsCommand(device, Key.Enter, GamepadButton.South);
            Assert.That(presentation.CameraMotionSetting, Is.EqualTo(PresentationResponseLevel.Off));
            Assert.That(presentation.VehicleMotionSetting, Is.EqualTo(PresentationResponseLevel.Off));
            Assert.That(presentation.VfxDensitySetting, Is.EqualTo(VfxDensityLevel.Off));

            yield return SettingsCommand(device, Key.Escape, GamepadButton.Start);
            InputSystem.RemoveDevice(sibling);
            yield return Frames();
            Assert.That(presentation.ModalHeadingText, Is.EqualTo("INPUT CHECK"));
            yield return SettingsCommand(device, Key.Enter, GamepadButton.South);
            yield return SettingsCommand(device, Key.Space, GamepadButton.West);
            Assert.That(presentation.CameraMotionSetting, Is.EqualTo(PresentationResponseLevel.Off),
                "Connected owners cannot edit while another player is disconnected.");
            InputSystem.RemoveDevice(device);
            yield return Frames();
            Assert.That(presentation.ModalHeadingText, Is.EqualTo("INPUT CHECK"));
            yield return PressAndRelease(outsider, GamepadButton.South);
            yield return PressAndRelease(outsider, GamepadButton.West);
            Assert.That(presentation.CameraMotionSetting, Is.EqualTo(PresentationResponseLevel.Off));
            Assert.That(presentation.ModalHeadingText, Is.EqualTo("INPUT CHECK"));
        }

        private static IEnumerator SettingsCommand(InputDevice device, Key key, GamepadButton button)
        {
            if (device is Gamepad pad)
            {
                yield return PressAndRelease(pad, button);
                yield break;
            }
            Keyboard keyboard = (Keyboard)device;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames();
        }

        [UnityTest]
        public IEnumerator ResultsConfirmationStartsOwnedRematchWithoutRejoining()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer player), Is.True);
            Assert.That(race.StartRace(), Is.True);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);

            for (int lap = 0; lap < race.TotalLaps; lap++)
                for (int checkpoint = 0; checkpoint < race.CheckpointCount; checkpoint++)
                    Assert.That(race.ReportCheckpoint(player.Racer.RacerId, checkpoint), Is.True);
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Results));

            yield return PressAndRelease(pad, GamepadButton.South);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Race));
            Assert.That(player.Racer.CompletedLaps, Is.Zero);
            Assert.That(roster.Players, Has.Count.EqualTo(1));
            Assert.That(roster.Players[0], Is.SameAs(player));
            Assert.That(roster.AllowControllerJoining, Is.False);
        }

        [UnityTest]
        public IEnumerator ResultsDisconnectAndReconnectRestoresStandingsWithoutChangingFinishedAuthority()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer player), Is.True);
            Assert.That(race.StartRace(), Is.True);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);

            for (int lap = 0; lap < race.TotalLaps; lap++)
                for (int checkpoint = 0; checkpoint < race.CheckpointCount; checkpoint++)
                    Assert.That(race.ReportCheckpoint(player.Racer.RacerId, checkpoint), Is.True);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
            string standings = presentation.ModalBodyText;
            string footer = presentation.ModalFooterText;
            Assert.That(standings, Does.Contain("PLAYER 1"));

            InputSystem.RemoveDevice(pad);
            yield return Frames();
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
            Assert.That(presentation.ModalHeadingText, Is.EqualTo("INPUT CHECK"));
            Assert.That(presentation.ModalBodyText, Does.Contain("PLAYER 1 INPUT DEVICE DISCONNECTED"));

            InputSystem.AddDevice(pad);
            yield return Frames();
            Assert.That(player.IsConnected, Is.True);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Results));
            Assert.That(presentation.ModalHeadingText, Is.EqualTo("FINAL BOARD"));
            Assert.That(presentation.ModalBodyText, Is.EqualTo(standings));
            Assert.That(presentation.ModalFooterText, Is.EqualTo(footer));
            Assert.That(roster.AllowControllerJoining, Is.False);
            Assert.That(player.Racer.IsFinished, Is.True);
        }

        [UnityTest]
        public IEnumerator PartialReconnectListsOnlyRemainingLossAndKeepsRaceGatedUntilAllReturn()
        {
            Gamepad firstPad = InputSystem.AddDevice<Gamepad>();
            Gamepad secondPad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(firstPad, out LocalPlayer first), Is.True);
            Assert.That(roster.TryJoinGamepad(secondPad, out LocalPlayer second), Is.True);
            Assert.That(race.StartRace(), Is.True);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);

            InputSystem.RemoveDevice(firstPad);
            yield return Frames();
            InputSystem.RemoveDevice(secondPad);
            yield return Frames();
            Assert.That(first.IsConnected, Is.False);
            Assert.That(second.IsConnected, Is.False);
            Assert.That(presentation.ModalBodyText, Does.Contain("PLAYER 1 INPUT DEVICE DISCONNECTED"));
            Assert.That(presentation.ModalBodyText, Does.Contain("PLAYER 2 INPUT DEVICE DISCONNECTED"));

            InputSystem.AddDevice(firstPad);
            yield return Frames();
            Assert.That(first.IsConnected, Is.True);
            Assert.That(second.IsConnected, Is.False);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Racing));
            Assert.That(presentation.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(presentation.ModalHeadingText, Is.EqualTo("INPUT CHECK"));
            Assert.That(presentation.ModalBodyText, Does.Not.Contain("PLAYER 1 INPUT DEVICE DISCONNECTED"));
            Assert.That(presentation.ModalBodyText, Does.Contain("PLAYER 2 INPUT DEVICE DISCONNECTED"));

            InputSystem.AddDevice(secondPad);
            yield return Frames();
            Assert.That(first.IsConnected, Is.True);
            Assert.That(second.IsConnected, Is.True);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Racing));
            Assert.That(presentation.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(presentation.ModalVisible, Is.False);
        }

        private static IEnumerator PressAndRelease(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return Frames();
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Frames();
        }

        private static IEnumerator Frames()
        {
            yield return null;
            yield return null;
        }
    }
}
