using System.Collections;
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
    public sealed class LocalPlayerIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private GameObject floor;
        private RaceManager race;
        private VehicleManager roster;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            root = new GameObject("Stage2Test");
            race = root.AddComponent<RaceManager>();
            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, null), Is.True);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -.5f, 0f);
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(floor);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator FourPadsHaveIsolatedDrivingFireAndPauseIncludingResume()
        {
            Gamepad[] pads = new Gamepad[4];
            for (int i = 0; i < pads.Length; i++)
            {
                pads[i] = InputSystem.AddDevice<Gamepad>();
                Assert.That(roster.TryJoinGamepad(pads[i], out _), Is.True);
            }
            Assert.That(race.Racers.Count, Is.EqualTo(4));
            Assert.That(InputUser.all.Count, Is.EqualTo(4));
            Assert.That(roster.TryJoinGamepad(pads[0], out _), Is.False);
            Gamepad fifth = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(fifth, out _), Is.False);
            int[] fire = new int[4];
            int[] pause = new int[4];
            roster.FireRequested += player => fire[player.PlayerIndex]++;
            roster.PauseRequested += player => pause[player.PlayerIndex]++;
            Assert.That(race.StartRace(), Is.True);
            Assert.That(roster.TryLeavePlayer(0), Is.False);
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.That(race.RaceRunning, Is.True);

            for (int active = 0; active < pads.Length; active++)
            {
                for (int i = 0; i < pads.Length; i++)
                    InputSystem.QueueStateEvent(pads[i], i == active
                        ? new GamepadState { rightTrigger = .8f, leftStick = new Vector2(.7f, 0) }
                            .WithButton(GamepadButton.South).WithButton(GamepadButton.West)
                        : new GamepadState());
                yield return WaitForPlayerLoop();
                for (int i = 0; i < pads.Length; i++)
                {
                    VehicleController car = roster.Players[i].Vehicle;
                    Assert.That(car.ThrottleInput, Is.EqualTo(i == active ? .8f : 0f).Within(.001f));
                    Assert.That(car.BrakeInput, Is.EqualTo(i == active ? 1f : 0f));
                    Assert.That(car.SteerInput, i == active ? Is.GreaterThan(.4f) : Is.EqualTo(0f));
                    Assert.That(fire[i], Is.EqualTo(i <= active ? 1 : 0));
                }
            }
            Time.timeScale = 0f;
            InputSystem.QueueStateEvent(pads[2], new GamepadState().WithButton(GamepadButton.Start).WithButton(GamepadButton.West));
            yield return WaitForPlayerLoop();
            Assert.That(pause, Is.EqualTo(new[] { 0, 0, 1, 0 }));
            Assert.That(fire, Is.EqualTo(new[] { 1, 1, 1, 1 }));
            foreach (LocalPlayer player in roster.Players) Assert.That(player.Vehicle.ThrottleInput, Is.Zero);
            Time.timeScale = 1f;
            InputSystem.QueueStateEvent(pads[2], new GamepadState { leftTrigger = .6f });
            yield return WaitForPlayerLoop();
            Assert.That(roster.Players[2].Vehicle.ThrottleInput, Is.EqualTo(-.6f).Within(.001f));
            RacerState originalRacer = roster.Players[2].Racer;
            InputSystem.RemoveDevice(pads[2]);
            yield return WaitForPlayerLoop();
            Assert.That(roster.Players[2].IsConnected, Is.False);
            Assert.That(roster.Players[2].Vehicle.ThrottleInput, Is.Zero);
            foreach (WheelCollider wheel in roster.Players[2].Vehicle.GetComponentsInChildren<WheelCollider>())
                Assert.That(wheel.motorTorque, Is.Zero);
            Assert.That(roster.TryLeavePlayer(2), Is.False);
            InputSystem.AddDevice(pads[2]);
            InputSystem.QueueStateEvent(pads[2], new GamepadState { rightTrigger = .5f });
            yield return WaitForPlayerLoop();
            Assert.That(roster.Players[2].Input.PairedGamepad, Is.SameAs(pads[2]));
            Assert.That(roster.Players[2].Racer, Is.SameAs(originalRacer));
            Assert.That(roster.Players[2].Vehicle.ThrottleInput, Is.EqualTo(.5f).Within(.001f));
            // A loss and regain between rendered frames must also refresh the device's control state.
            InputSystem.RemoveDevice(pads[2]);
            InputSystem.AddDevice(pads[2]);
            InputSystem.QueueStateEvent(pads[2], new GamepadState { leftTrigger = .6f });
            yield return WaitForPlayerLoop();
            Assert.That(roster.Players[2].Vehicle.ThrottleInput, Is.EqualTo(-.6f).Within(.001f));
            roster.enabled = false;
            yield return WaitForPlayerLoop();
            Assert.That(InputUser.all.Count, Is.Zero);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Idle));
            Assert.That(race.Racers.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator JoinLeaveRejoinLayoutsAndDisconnectKeepStableOwnership()
        {
            Gamepad[] pads = new Gamepad[4];
            for (int i = 0; i < 4; i++)
            {
                pads[i] = InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pads[i], new GamepadState().WithButton(GamepadButton.Start));
                yield return WaitForPlayerLoop();
                Assert.That(roster.Players.Count, Is.EqualTo(i + 1));
                for (int j = 0; j <= i; j++)
                {
                    Viewport expected = LocalPlayerRules.GetViewport(i + 1, j);
                    Assert.That(roster.Players[j].Camera.rect, Is.EqualTo(new Rect(expected.X, expected.Y, expected.Width, expected.Height)));
                }
                InputSystem.QueueStateEvent(pads[i], new GamepadState());
                yield return WaitForPlayerLoop();
            }
            InputSystem.QueueStateEvent(pads[1], new GamepadState().WithButton(GamepadButton.Select));
            yield return WaitForPlayerLoop();
            Assert.That(roster.Players.Count, Is.EqualTo(3));
            Assert.That(roster.Players[1].PlayerIndex, Is.EqualTo(2));
            Assert.That(InputUser.FindUserPairedToDevice(pads[1]).HasValue, Is.False);
            Assert.That(roster.TryJoinGamepad(pads[1], out LocalPlayer rejoined), Is.True);
            Assert.That(rejoined.PlayerIndex, Is.EqualTo(1));
            InputSystem.QueueStateEvent(pads[1], new GamepadState());
            yield return WaitForPlayerLoop();
            InputSystem.RemoveDevice(pads[0]);
            yield return WaitForPlayerLoop();
            Assert.That(roster.Players.Count, Is.EqualTo(4));
            Assert.That(roster.Players[0].IsConnected, Is.False);
            Assert.That(roster.TryReplaceDisconnectedController(0, pads[2]), Is.False);
            Gamepad replacement = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryReplaceDisconnectedController(0, replacement), Is.True);
            Assert.That(roster.Players[0].Input.PairedGamepad, Is.SameAs(replacement));
            roster.enabled = false;
            yield return WaitForPlayerLoop();
            Assert.That(InputUser.all.Count, Is.Zero);
            Assert.That(race.Racers.Count, Is.Zero);
            Assert.That(roster.Players.Count, Is.Zero);
            roster.enabled = true;
            Assert.That(roster.TryJoinGamepad(replacement, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator RecoveryUsesRecentCheckpointAndFallbackWithoutAdvancingRace()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer player), Is.True);
            yield return WaitForPlayerLoop();
            Pose checkpoint = new Pose(new Vector3(10f, .5f, 10f), Quaternion.Euler(0, 90, 0));
            player.Recovery.RecordCheckpoint(checkpoint);
            player.Vehicle.Teleport(new Pose(new Vector3(0, -20, 0), Quaternion.Euler(180, 0, 0)));
            Assert.That(player.Recovery.TryRecover(), Is.True);
            Assert.That(player.Vehicle.transform.position.x, Is.EqualTo(10f).Within(.01f));
            Assert.That(player.Vehicle.transform.position.y, Is.GreaterThan(.3f));
            Assert.That(Vector3.Dot(player.Vehicle.transform.up, Vector3.up), Is.GreaterThan(.99f));
            Assert.That(player.Vehicle.GetComponent<Rigidbody>().linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(player.Racer.CompletedLaps, Is.Zero);
            Assert.That(player.Racer.NextCheckpointIndex, Is.Zero);
            yield return new WaitForSecondsRealtime(.6f);
            player.Recovery.RecordCheckpoint(new Pose(new Vector3(500, 0, 500), Quaternion.identity));
            Assert.That(player.Recovery.TryRecover(), Is.True);
            Assert.That(player.Vehicle.transform.position.x, Is.EqualTo(0f).Within(.01f));
            Assert.That(player.Vehicle.transform.position.z, Is.EqualTo(0f).Within(.01f));
            yield return new WaitForSecondsRealtime(.6f);
            floor.SetActive(false);
            Vector3 before = player.Vehicle.transform.position;
            Assert.That(player.Recovery.TryRecover(), Is.False, "Recovery must refuse unsupported terrain.");
            Assert.That(player.Vehicle.transform.position, Is.EqualTo(before));
            race.ResetRace();
        }

        private static IEnumerator WaitForPlayerLoop()
        {
            // Test coroutines can resume before MonoBehaviour.Update; allow a complete input + gameplay frame.
            yield return null;
            yield return null;
        }
    }
}
