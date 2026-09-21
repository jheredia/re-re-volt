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
using Weapons;

namespace Tests.PlayMode
{
    public sealed class KeyboardIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private RaceManager race;
        private VehicleManager roster;
        private Stage3RacePresentation hud;
        private WeaponManager combat;
        private PrototypeCircuitBuilder.Result circuit;
        private Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            root = new GameObject("KeyboardTest");
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, 8), Is.True);
            circuit = PrototypeCircuitBuilder.Build(race, root.transform);
            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, circuit.Grid), Is.True);
            roster.PlayerJoined += player => player.Recovery.ConfigureGroundMask(circuit.DrivableMask);
            hud = root.AddComponent<Stage3RacePresentation>();
            hud.Configure(race, roster);
            combat = root.AddComponent<WeaponManager>();
            combat.Configure(race, roster, hud, circuit.DrivableMask);
            keyboard = InputSystem.AddDevice<Keyboard>();
            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
            circuit?.Dispose();
            Assert.That(InputUser.all.Count, Is.Zero, "Roster destruction must release every input owner.");
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator KeyboardSoloJoinsDrivesFiresRecoversPausesAndRematches()
        {
            yield return Press(Key.Enter);
            Assert.That(roster.Players.Count, Is.EqualTo(1));
            LocalPlayer player = roster.Players[0];
            Assert.That(player.Input.PairedDevice, Is.SameAs(keyboard));
            Assert.That(player.IsConnected, Is.True);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Idle), "Join must not also start.");
            Assert.That(hud.HudCount, Is.EqualTo(1));
            yield return Press(Key.Enter);
            yield return Racing();

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.LeftShift));
            yield return Frames();
            Assert.That(player.Vehicle.ThrottleInput, Is.EqualTo(1f));
            Assert.That(player.Vehicle.SteerInput, Is.EqualTo(1f));
            Assert.That(player.Vehicle.BrakeInput, Is.EqualTo(1f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow, Key.LeftArrow, Key.RightShift));
            yield return Frames();
            Assert.That(player.Vehicle.ThrottleInput, Is.EqualTo(-1f));
            Assert.That(player.Vehicle.SteerInput, Is.EqualTo(-1f));
            Assert.That(player.Vehicle.BrakeInput, Is.EqualTo(1f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.DownArrow, Key.A, Key.RightArrow));
            yield return Frames();
            Assert.That(player.Vehicle.ThrottleInput, Is.Zero);
            Assert.That(player.Vehicle.SteerInput, Is.Zero);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames();

            player.Vehicle.Teleport(new Pose(WeaponManager.GetPickupPosition(0), Quaternion.identity));
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.15f);
            Assert.That(combat.HeldWeapon(0), Is.EqualTo(WeaponKind.Bolt));
            yield return Press(Key.Space);
            Assert.That(combat.HeldWeapon(0), Is.EqualTo(WeaponKind.None));
            Assert.That(combat.LiveWeapons.Count, Is.EqualTo(1));
            int recovered = 0;
            player.Recovery.Recovered += () => recovered++;
            yield return Press(Key.R);
            Assert.That(recovered, Is.EqualTo(1));

            yield return Press(Key.Escape);
            Assert.That(hud.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            int fire = 0;
            roster.FireRequested += _ => fire++;
            yield return Press(Key.Space, Key.R, Key.W);
            Assert.That(fire, Is.Zero);
            Assert.That(recovered, Is.EqualTo(1));
            Assert.That(player.Vehicle.ThrottleInput, Is.Zero);
            yield return Press(Key.Escape);
            Assert.That(hud.IsPaused, Is.False);
            Finish(player);
            Assert.That(hud.ModalVisible, Is.True);
            yield return Press(Key.Enter);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            Assert.That(player.Racer.CompletedLaps, Is.Zero);
            yield return Racing();
            Finish(player);
            yield return Press(Key.Escape);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Idle));
            Assert.That(roster.Players.Count, Is.EqualTo(1), "Results back must not also leave.");
            Assert.That(hud.LobbyVisible, Is.True);
            yield return Press(Key.Escape);
            Assert.That(roster.Players.Count, Is.Zero);
            Assert.That(InputUser.all.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator KeyboardAndThreePadsIsolateDrivingFireRecoveryAndPause()
        {
            Assert.That(roster.TryJoinKeyboard(keyboard, out _), Is.True);
            Gamepad[] pads = new Gamepad[3];
            for (int i = 0; i < pads.Length; i++)
            {
                pads[i] = InputSystem.AddDevice<Gamepad>();
                Assert.That(roster.TryJoinGamepad(pads[i], out _), Is.True);
            }
            Assert.That(InputUser.all.Count, Is.EqualTo(4));
            Assert.That(roster.TryJoinGamepad(InputSystem.AddDevice<Gamepad>(), out _), Is.False);
            int[] fire = new int[4];
            int[] recovered = new int[4];
            int[] pause = new int[4];
            roster.FireRequested += player => fire[player.PlayerIndex]++;
            roster.PauseRequested += player => pause[player.PlayerIndex]++;
            foreach (LocalPlayer player in roster.Players)
            {
                int slot = player.PlayerIndex;
                player.Recovery.Recovered += () => recovered[slot]++;
            }
            yield return Frames();
            yield return Press(Key.Enter);
            yield return Racing();
            for (int active = 0; active < 4; active++)
            {
                InputSystem.QueueStateEvent(keyboard, active == 0
                    ? new KeyboardState(Key.UpArrow, Key.RightArrow, Key.LeftShift, Key.Space, Key.R)
                    : new KeyboardState());
                for (int i = 0; i < 3; i++)
                    InputSystem.QueueStateEvent(pads[i], active == i + 1
                        ? new GamepadState { rightTrigger = 1f, leftStick = Vector2.right }
                            .WithButton(GamepadButton.South).WithButton(GamepadButton.West).WithButton(GamepadButton.North)
                        : new GamepadState());
                yield return Frames();
                // Recovery resets raw input in its first frame; the next held frame restores driving.
                foreach (LocalPlayer player in roster.Players)
                {
                    int slot = player.PlayerIndex;
                    Assert.That(player.Vehicle.ThrottleInput, Is.EqualTo(slot == active ? 1f : 0f));
                    Assert.That(player.Vehicle.SteerInput, Is.EqualTo(slot == active ? 1f : 0f));
                    Assert.That(player.Vehicle.BrakeInput, Is.EqualTo(slot == active ? 1f : 0f));
                    Assert.That(fire[slot], Is.EqualTo(slot <= active ? 1 : 0));
                    Assert.That(recovered[slot], Is.EqualTo(slot <= active ? 1 : 0));
                }
            }
            yield return Press(Key.Escape);
            Assert.That(pause, Is.EqualTo(new[] { 1, 0, 0, 0 }));
            Assert.That(hud.IsPaused, Is.True);
            yield return Press(Key.Escape);
            InputSystem.QueueStateEvent(pads[1], new GamepadState().WithButton(GamepadButton.Start));
            yield return Frames();
            Assert.That(pause, Is.EqualTo(new[] { 2, 0, 1, 0 }));
            Assert.That(hud.IsPaused, Is.True);
            roster.enabled = false;
            Assert.That(InputUser.all.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OneKeyboardUsesFirstFreeSlotAndRetainsOwnershipAcrossDisconnect()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out _), Is.True);
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer player), Is.True);
            Assert.That(player.PlayerIndex, Is.EqualTo(1));
            Keyboard second = InputSystem.AddDevice<Keyboard>();
            Assert.That(roster.TryJoinKeyboard(keyboard, out _), Is.False);
            Assert.That(roster.TryJoinKeyboard(second, out _), Is.False);
            yield return Frames();
            yield return Press(Key.Enter);
            yield return Racing();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return Frames();
            InputSystem.RemoveDevice(keyboard);
            yield return Frames();
            Assert.That(player.IsConnected, Is.False);
            Assert.That(player.Vehicle.ThrottleInput, Is.Zero);
            Assert.That(hud.IsPaused, Is.True);
            Assert.That(roster.TryJoinKeyboard(second, out _), Is.False);
            Assert.That(roster.TryReplaceDisconnectedController(1, InputSystem.AddDevice<Gamepad>()), Is.False);
            InputSystem.AddDevice(keyboard);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames();
            Assert.That(player.IsConnected, Is.True);
            Assert.That(hud.IsPaused, Is.False);
            Assert.That(player.Vehicle.ThrottleInput, Is.Zero);
            race.ResetRace();
            Assert.That(roster.TryLeavePlayer(0), Is.True);
            Assert.That(roster.TryLeavePlayer(1), Is.True);
            Assert.That(roster.TryJoinKeyboard(second, out LocalPlayer replacement), Is.True);
            Assert.That(replacement.PlayerIndex, Is.Zero);
        }

        [Test]
        public void DestroyedPlayerObjectsDoNotInterruptRosterTeardown()
        {
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer player), Is.True);
            Object.DestroyImmediate(player.Camera.gameObject);
            Object.DestroyImmediate(player.Vehicle.gameObject);

            Assert.DoesNotThrow(() => roster.enabled = false);
            Assert.That(roster.Players, Is.Empty);
            Assert.That(InputUser.all.Count, Is.Zero, "Teardown must release input ownership after scene-order destruction.");
        }

        private void Finish(LocalPlayer player)
        {
            for (int guard = 0; guard < 24 && !player.Racer.IsFinished; guard++)
                Assert.That(race.ReportCheckpoint(player.Racer.RacerId, player.Racer.NextCheckpointIndex), Is.True);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
        }

        private IEnumerator Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames();
        }

        private IEnumerator Racing()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True, "Countdown must enter Racing within 10 seconds.");
        }

        private static IEnumerator Frames()
        {
            yield return null;
            yield return null;
        }
    }
}
