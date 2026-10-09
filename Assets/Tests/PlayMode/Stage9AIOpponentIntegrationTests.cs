using System.Collections;
using System.Collections.Generic;
using AI;
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
    public sealed class Stage9AIOpponentIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private RaceManager race;
        private VehicleManager humans;
        private AIManager ai;
        private Stage3RacePresentation presentation;
        private PrototypeCircuitBuilder.Result circuit;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            root = new GameObject("Stage9AITest");
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, PrototypeCircuitBuilder.OrderedCheckpointCount), Is.True);
            circuit = PrototypeCircuitBuilder.Build(race, root.transform);
            humans = root.AddComponent<VehicleManager>();
            Assert.That(humans.Configure(race, circuit.Grid), Is.True);
            humans.PlayerJoined += player => player.Recovery.ConfigureGroundMask(circuit.DrivableMask);
            ai = root.AddComponent<AIManager>();
            Assert.That(ai.Configure(race, humans, circuit.Grid, circuit.Checkpoints, circuit.DrivableMask), Is.True);
            presentation = root.AddComponent<Stage3RacePresentation>();
            presentation.Configure(race, humans, aiManager: ai);
            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
            circuit?.Dispose();
            Assert.That(InputUser.all.Count, Is.Zero, "AI cleanup must not leave a local input owner.");
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator FirstHumanCanSelectZeroToThreeBotsWithOwnedKeyboardOrController()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(humans.TryJoinKeyboard(keyboard, out _), Is.True);
            Assert.That(ai.SelectedBotCount, Is.Zero);
            Assert.That(presentation.LobbyText, Does.Contain("AI TEST CARS  0 / 3"));

            for (int expected = 1; expected <= 3; expected++)
            {
                yield return PressAndRelease(keyboard, Key.B);
                Assert.That(ai.SelectedBotCount, Is.EqualTo(expected));
            }
            yield return PressAndRelease(keyboard, Key.B);
            Assert.That(ai.SelectedBotCount, Is.Zero);

            Assert.That(humans.TryLeavePlayer(0), Is.True);
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(humans.TryJoinGamepad(pad, out _), Is.True);
            yield return PressAndRelease(pad, GamepadButton.LeftStick);
            Assert.That(ai.SelectedBotCount, Is.EqualTo(1));
            Assert.That(ai.ActiveBotCount, Is.Zero, "Setup selection must not consume human roster capacity.");
        }

        [UnityTest]
        public IEnumerator HumanJoinsClampSelectionBeforeBotsClaimAuthoritativeGridSlots()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(humans.TryJoinKeyboard(keyboard, out _), Is.True);
            Assert.That(ai.TrySetSelectedBotCount(3), Is.True);

            Assert.That(humans.TryJoinGamepad(pad, out _), Is.True);
            yield return Frames();

            Assert.That(ai.SelectedBotCount, Is.EqualTo(2));
            Assert.That(ai.MaximumSelectableBots, Is.EqualTo(2));
            Assert.That(ai.ActiveBotCount, Is.Zero);
            Assert.That(race.Racers, Has.Count.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator LobbyReleaseRemovesOnlyBotsAndRetainsTheirSetupSelection()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(humans.TryJoinKeyboard(keyboard, out LocalPlayer human), Is.True);
            Assert.That(ai.TrySetSelectedBotCount(2), Is.True);
            Assert.That(ai.PrepareRaceRoster(), Is.True, ai.LastRosterError);
            GameObject[] botObjects = new GameObject[ai.Drivers.Count];
            for (int i = 0; i < ai.Drivers.Count; i++) botObjects[i] = ai.Drivers[i].gameObject;

            Assert.That(ai.ReleaseBotsForLobby(), Is.True);
            yield return null;

            Assert.That(ai.ActiveBotCount, Is.Zero);
            Assert.That(ai.Drivers, Is.Empty);
            Assert.That(ai.SelectedBotCount, Is.EqualTo(2));
            Assert.That(race.Racers, Has.Count.EqualTo(1));
            Assert.That(race.Racers[0], Is.SameAs(human.Racer));
            for (int i = 0; i < botObjects.Length; i++) Assert.That(botObjects[i], Is.Null);
        }

        [UnityTest]
        public IEnumerator PreparedBotsShareRaceAuthorityWithoutInputCameraHudOrWeaponOwnership()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(humans.TryJoinKeyboard(keyboard, out LocalPlayer human), Is.True);
            Assert.That(ai.TrySetSelectedBotCount(3), Is.True);
            Assert.That(ai.PrepareRaceRoster(), Is.True, ai.LastRosterError);

            Assert.That(ai.ActiveBotCount, Is.EqualTo(3));
            Assert.That(race.Racers, Has.Count.EqualTo(4));
            Assert.That(humans.Players, Has.Count.EqualTo(1));
            Assert.That(presentation.HudCount, Is.EqualTo(1));
            Assert.That(human.Camera.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Collider humanCollider = human.Vehicle.GetComponent<Collider>();
            for (int i = 0; i < ai.Drivers.Count; i++)
            {
                AIDriverController driver = ai.Drivers[i];
                Assert.That(driver.Racer.RacerId, Is.EqualTo($"bot-{i + 1}"));
                Assert.That(driver.Racer.IsHuman, Is.False);
                Assert.That(driver.Racer.PlayerIndex, Is.EqualTo(-1));
                Assert.That(driver.GetComponent<InputHandler>(), Is.Null);
                Assert.That(driver.GetComponentInChildren<Camera>(true), Is.Null);
                Assert.That(driver.GetComponentInChildren<AudioListener>(true), Is.Null);
                Assert.That(Physics.GetIgnoreCollision(humanCollider, driver.GetComponent<Collider>()), Is.False,
                    "Bots and humans must share the existing collision physics.");
            }

            Assert.That(race.StartRace(), Is.True);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < ai.Drivers.Count; i++)
                Assert.That(ai.Drivers[i].Vehicle.ThrottleInput, Is.GreaterThan(0f));

            AIDriverController recoveryDriver = ai.Drivers[0];
            recoveryDriver.Vehicle.Teleport(new Pose(new Vector3(0f, 1f, 0f), Quaternion.identity));
            float recoveryDeadline = Time.realtimeSinceStartup + 3f;
            while (recoveryDriver.RecoveryCount == 0 && Time.realtimeSinceStartup < recoveryDeadline)
                yield return new WaitForFixedUpdate();
            Assert.That(recoveryDriver.RecoveryCount, Is.EqualTo(1),
                "A deterministic off-track timeout should use the shared checkpoint recovery authority.");

            Vector3 beforePause = ai.Drivers[0].transform.position;
            yield return PressAndRelease(keyboard, Key.Escape);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(ai.Drivers[0].Vehicle.ThrottleInput, Is.Zero);
            yield return null;
            Assert.That(ai.Drivers[0].transform.position, Is.EqualTo(beforePause));
            yield return PressAndRelease(keyboard, Key.Escape);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Racing));
            Assert.That(ai.ActiveBotCount, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator OneBotAutonomouslyCrossesOrderedTriggersAndFinishesThreeLaps()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(humans.TryJoinKeyboard(keyboard, out LocalPlayer human), Is.True);
            Assert.That(ai.TrySetSelectedBotCount(1), Is.True);
            Assert.That(ai.PrepareRaceRoster(), Is.True, ai.LastRosterError);
            AIDriverController driver = ai.Drivers[0];
            List<int> acceptedCheckpoints = new(race.CheckpointCount * 3);
            race.RacerProgressChanged += racer =>
            {
                if (object.ReferenceEquals(racer, driver.Racer)) acceptedCheckpoints.Add(racer.LastCheckpointIndex);
            };

            Assert.That(race.StartRace(), Is.True);
            float startDeadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < startDeadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);
            Assert.That(driver.Racer.SegmentProgress, Is.EqualTo(human.Racer.SegmentProgress),
                "Neither human nor bot may receive continuous authoritative segment progress in this slice.");
            Assert.That(driver.Racer.SegmentProgress, Is.EqualTo(0f));

            int humanGuard = race.TotalLaps * race.CheckpointCount + 1;
            while (!human.Racer.IsFinished && humanGuard-- > 0)
                Assert.That(race.ReportCheckpoint(human.Racer.RacerId, human.Racer.NextCheckpointIndex), Is.True);
            Assert.That(human.Racer.IsFinished, Is.True);

            const float autonomousDeadlineSeconds = 90f;
            float finishDeadline = Time.realtimeSinceStartup + autonomousDeadlineSeconds;
            while (!driver.Racer.IsFinished && Time.realtimeSinceStartup < finishDeadline)
                yield return new WaitForFixedUpdate();

            string diagnostic = $"Bot timed out after {autonomousDeadlineSeconds:0}s: " +
                                $"lap={driver.Racer.CurrentLap}/{race.TotalLaps}, " +
                                $"nextCheckpoint={driver.Racer.NextCheckpointIndex}, " +
                                $"accepted={acceptedCheckpoints.Count}, speed={driver.Vehicle.CurrentSpeed:0.0}km/h, " +
                                $"recoveries={driver.RecoveryCount}, position={driver.transform.position}.";
            Assert.That(driver.Racer.IsFinished, Is.True, diagnostic);
            Assert.That(driver.Racer.CompletedLaps, Is.EqualTo(3));
            Assert.That(acceptedCheckpoints, Has.Count.EqualTo(race.CheckpointCount * race.TotalLaps));
            for (int i = 0; i < acceptedCheckpoints.Count; i++)
                Assert.That(acceptedCheckpoints[i], Is.EqualTo(i % race.CheckpointCount),
                    $"Bot checkpoint trigger order diverged at accepted crossing {i}.");
            Assert.That(driver.Racer.SegmentProgress, Is.EqualTo(0f));
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Results));
            Assert.That(presentation.ModalBodyText, Does.Contain("CPU 1"));

            yield return PressAndRelease(keyboard, Key.Enter);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            Assert.That(ai.ActiveBotCount, Is.EqualTo(1), "Rematch should retain the deterministic AI identity.");
            Assert.That(race.Racers, Has.Count.EqualTo(2));
        }

        private static IEnumerator PressAndRelease(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames();
        }

        private static IEnumerator PressAndRelease(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return Frames();
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Frames();
        }

        private static IEnumerator Frames(int count = 2)
        {
            for (int i = 0; i < count; i++) yield return null;
        }
    }
}
