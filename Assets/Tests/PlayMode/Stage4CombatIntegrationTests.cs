using System.Collections;
using System.IO;
using Core;
using NUnit.Framework;
using Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Vehicle;
using Weapons;

namespace Tests.PlayMode
{
    public sealed class Stage4CombatIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private RaceManager race;
        private VehicleManager roster;
        private Stage3RacePresentation hud;
        private WeaponManager combat;
        private PrototypeCircuitBuilder.Result circuit;
        private Gamepad padA, padB;
        private LocalPlayer a, b;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            root = new GameObject("Stage4CombatTest");
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, 8), Is.True);
            circuit = PrototypeCircuitBuilder.Build(race, root.transform);
            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, circuit.Grid), Is.True);
            hud = root.AddComponent<Stage3RacePresentation>();
            hud.Configure(race, roster);
            combat = root.AddComponent<WeaponManager>();
            combat.Configure(race, roster, hud, circuit.DrivableMask);
            padA = InputSystem.AddDevice<Gamepad>();
            padB = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(padA, out a), Is.True);
            Assert.That(roster.TryJoinGamepad(padB, out b), Is.True);
            a.Recovery.ConfigureGroundMask(circuit.DrivableMask);
            b.Recovery.ConfigureGroundMask(circuit.DrivableMask);
            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
            circuit?.Dispose();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator TwoPlayersCollectFireAndReceiveBothWeaponsIndependently()
        {
            yield return StartRace();
            yield return Collect(a, 0, WeaponKind.Bolt);
            yield return Collect(b, 1, WeaponKind.Oil);
            AssertHud("BOLT", "OIL");
            Place(a, new Vector3(-15f, .8f, 3f), 0);
            Place(b, new Vector3(-15f, .8f, 9f), 0);
            yield return Press(padA, GamepadButton.West);
            Assert.That(combat.HeldWeapon(0), Is.EqualTo(WeaponKind.None));
            Assert.That(combat.HeldWeapon(1), Is.EqualTo(WeaponKind.Oil));
            yield return new WaitForSeconds(.3f);
            Assert.That(b.Vehicle.HasCombatModifier, Is.True, "P1 projectile hit P2.");
            Assert.That(a.Vehicle.HasCombatModifier, Is.False);
            yield return new WaitForSeconds(1.1f);
            Assert.That(b.Vehicle.HasCombatModifier, Is.False);

            Place(b, new Vector3(15f, .8f, 4f), 180);
            Place(a, new Vector3(15f, .8f, 11f), 180);
            yield return Press(padB, GamepadButton.West);
            Assert.That(combat.LiveWeapons.Count, Is.EqualTo(1));
            Vector3 oil = combat.LiveWeapons[0].transform.position;
            Place(a, oil + Vector3.up * .65f, 180);
            yield return new WaitForSeconds(.8f);
            Assert.That(a.Vehicle.CombatGripMultiplier, Is.EqualTo(.18f).Within(.001f));
            Assert.That(b.Vehicle.HasCombatModifier, Is.False);
            yield return new WaitForSeconds(2.6f);
            Assert.That(a.Vehicle.HasCombatModifier, Is.False);

            // Both initial cycles have respawned with the opposite weapon.
            yield return Collect(a, 0, WeaponKind.Oil);
            yield return Collect(b, 1, WeaponKind.Bolt);
            Place(a, new Vector3(0f, .8f, 15f), 90);
            Place(b, new Vector3(-6f, .8f, 15f), 90);
            yield return Press(padB, GamepadButton.West);
            yield return new WaitForSeconds(.3f);
            Assert.That(a.Vehicle.HasCombatModifier, Is.True, "P2 projectile hit P1.");
            yield return new WaitForSeconds(1.1f);
            Place(a, new Vector3(0f, .8f, -15f), 270);
            Place(b, new Vector3(6f, .8f, -15f), 270);
            yield return Press(padA, GamepadButton.West);
            oil = combat.LiveWeapons[0].transform.position;
            Place(b, oil + Vector3.up * .65f, 270);
            yield return new WaitForSeconds(.8f);
            Assert.That(b.Vehicle.CombatGripMultiplier, Is.EqualTo(.18f).Within(.001f));
            yield return new WaitForSeconds(2.6f);
            Assert.That(b.Vehicle.HasCombatModifier, Is.False);
        }

        [UnityTest]
        public IEnumerator PickupsPauseDisconnectGraceExpiryAndRecoveryAreSafe()
        {
            yield return StartRace();
            yield return Collect(a, 0, WeaponKind.Bolt);
            Place(a, WeaponManager.GetPickupPosition(1), 90);
            yield return new WaitForSeconds(.1f);
            Assert.That(combat.Pickups[1].IsAvailable, Is.True, "Full slot cannot consume another box.");
            Place(a, new Vector3(-15f, .8f, -3f), 0);
            yield return Collect(b, 1, WeaponKind.Oil);
            Place(a, new Vector3(-15f, .8f, -3f), 0);
            Place(b, new Vector3(15f, .8f, 3f), 180);
            yield return Press(padA, GamepadButton.West);
            Assert.That(a.Vehicle.HasCombatModifier, Is.False, "Projectile permanently excludes shooter.");
            yield return Press(padB, GamepadButton.West);
            LiveCombatWeapon oil = FindWeapon(WeaponKind.Oil);
            Place(b, oil.transform.position + Vector3.up * .65f, 180);
            yield return new WaitForSeconds(.75f);
            Assert.That(b.Vehicle.HasCombatModifier, Is.False, "Shooter grace outlasts arming.");
            yield return Press(padA, GamepadButton.Start);
            double stopped = combat.SimulationTime;
            float age = oil.Age;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(combat.SimulationTime, Is.EqualTo(stopped));
            Assert.That(oil.Age, Is.EqualTo(age));
            yield return Press(padB, GamepadButton.West);
            Assert.That(combat.HeldWeapon(1), Is.EqualTo(WeaponKind.None));
            yield return Press(padA, GamepadButton.Start);
            InputSystem.RemoveDevice(padB);
            yield return Frames();
            stopped = combat.SimulationTime;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(combat.SimulationTime, Is.EqualTo(stopped));
            InputSystem.AddDevice(padB);
            yield return Frames();
            yield return new WaitForSeconds(.6f);
            Assert.That(b.Vehicle.HasCombatModifier, Is.True, "Shooter can drive into armed oil after grace.");
            Assert.That(b.Recovery.TryRecover(), Is.True);
            Assert.That(b.Vehicle.HasCombatModifier, Is.False, "Recovery clears disruption immediately.");
            yield return new WaitForSeconds(.1f);
            Assert.That(b.Vehicle.HasCombatModifier, Is.False);
            yield return new WaitForSeconds(5f);
            Assert.That(combat.LiveWeapons.Count, Is.Zero);
            Assert.That(combat.Pickups[0].IsAvailable, Is.True);
            Assert.That(combat.Pickups[0].Offering, Is.EqualTo(WeaponKind.Oil));
        }

        [UnityTest]
        public IEnumerator FinishRematchLobbyAndDisableRemoveAllCombatState()
        {
            yield return StartRace();
            yield return Collect(a, 0, WeaponKind.Bolt);
            yield return Collect(b, 1, WeaponKind.Oil);
            Place(a, new Vector3(-15f, .8f, 3f), 0);
            Place(b, new Vector3(-15f, .8f, 9f), 0);
            yield return Press(padA, GamepadButton.West);
            yield return new WaitForSeconds(.3f);
            Assert.That(b.Vehicle.HasCombatModifier, Is.True);
            Finish(b);
            Assert.That(b.Vehicle.HasCombatModifier, Is.False);
            Assert.That(combat.HeldWeapon(1), Is.EqualTo(WeaponKind.None));
            yield return Press(padB, GamepadButton.West);
            Assert.That(combat.LiveWeapons.Count, Is.Zero);
            Finish(a);
            AssertClean();
            yield return Press(padA, GamepadButton.South);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            AssertClean();
            yield return WaitForRaceStart();
            yield return Collect(a, 1, WeaponKind.Oil);
            Place(a, new Vector3(0f, .8f, 15f), 90);
            yield return Press(padA, GamepadButton.West);
            yield return Collect(b, 0, WeaponKind.Bolt);
            Assert.That(combat.LiveWeapons.Count, Is.EqualTo(1));
            race.ResetRace();
            AssertClean();
            Assert.That(hud.LobbyVisible, Is.True);
            yield return StartRace();
            yield return Collect(a, 1, WeaponKind.Oil);
            Place(a, new Vector3(0f, .8f, 15f), 90);
            yield return Press(padA, GamepadButton.West);
            combat.enabled = false;
            AssertClean();
            combat.enabled = true;
            Assert.That(combat.HeldWeapon(0), Is.EqualTo(WeaponKind.None));
        }

        [UnityTest]
        public IEnumerator ProjectileStopsAtGeometryExcludesShooterAndExpiresInOpenSpace()
        {
            yield return StartRace();
            yield return Collect(a, 0, WeaponKind.Bolt);
            Place(a, new Vector3(-15f, .8f, 15f), 0);
            yield return Press(padA, GamepadButton.West);
            yield return new WaitForSeconds(.2f);
            Assert.That(combat.LiveWeapons.Count, Is.Zero, "The outer barrier absorbs the bolt.");
            yield return new WaitForSeconds(.2f); // Respect the authoritative .35-second fire cooldown.
            yield return Collect(a, 2, WeaponKind.Bolt);
            Place(a, new Vector3(0f, 8f, 0f), 0);
            a.Vehicle.GetComponent<Rigidbody>().useGravity = false;
            yield return Press(padA, GamepadButton.West);
            LiveCombatWeapon bolt = FindWeapon(WeaponKind.Bolt);
            Assert.That(bolt, Is.Not.Null);
            Place(a, bolt.transform.position + Vector3.forward * 2f, 0);
            yield return new WaitForSeconds(.2f);
            Assert.That(a.Vehicle.HasCombatModifier, Is.False);
            Assert.That(combat.LiveWeapons.Count, Is.EqualTo(1), "Crossing the shooter's chassis cannot consume the bolt.");
            yield return new WaitForSeconds(2.5f);
            Assert.That(combat.LiveWeapons.Count, Is.EqualTo(1));
            yield return new WaitForSeconds(.4f);
            Assert.That(combat.LiveWeapons.Count, Is.Zero, "An unobstructed shot has a bounded lifetime.");
        }

        [UnityTest]
        public IEnumerator UnclaimedOilExpiresAndSuspendedInputCannotFireHeldWeapon()
        {
            yield return StartRace();
            yield return Collect(a, 1, WeaponKind.Oil);
            yield return Collect(b, 0, WeaponKind.Bolt);
            roster.SetAllVehiclesEnabled(false);
            double stopped = combat.SimulationTime;
            yield return Press(padA, GamepadButton.West);
            yield return new WaitForSeconds(.2f);
            Assert.That(combat.SimulationTime, Is.EqualTo(stopped));
            Assert.That(combat.HeldWeapon(0), Is.EqualTo(WeaponKind.Oil));
            Assert.That(combat.LiveWeapons.Count, Is.Zero);
            roster.SetAllVehiclesEnabled(true);
            Place(a, new Vector3(0f, .8f, 15f), 90);
            Place(b, new Vector3(15f, .8f, -5f), 180);
            yield return Press(padA, GamepadButton.West);
            Assert.That(combat.LiveWeapons.Count, Is.EqualTo(1));
            yield return new WaitForSeconds(8.5f);
            Assert.That(combat.LiveWeapons.Count, Is.EqualTo(1), "Unclaimed oil stays until its lifetime expires.");
            Assert.That(a.Vehicle.HasCombatModifier, Is.False, "A stationary shooter is clear of the drop footprint.");
            yield return new WaitForSeconds(.6f);
            Assert.That(combat.LiveWeapons.Count, Is.Zero);
            Assert.That(combat.FeedbackCount, Is.Zero);
            Assert.That(combat.HeldWeapon(1), Is.EqualTo(WeaponKind.Bolt));
        }

        [UnityTest]
        public IEnumerator CaptureProjectileAndOilPickupVisualReview()
        {
            GameObject lighting = new GameObject("Visual review directional light");
            lighting.transform.SetParent(root.transform, false);
            lighting.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lighting.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            yield return StartRace();
            // Let editor shader compilation finish before recording runtime materials.
            yield return new WaitForSeconds(2f);
            yield return Collect(a, 0, WeaponKind.Bolt);
            yield return Collect(b, 1, WeaponKind.Oil);
            Place(a, new Vector3(-15f, .8f, 6f), 0);
            Place(b, new Vector3(15f, .8f, -3f), 180);
            // Freeze immediately after the combat subscriber creates the shot. Four input-helper
            // frames can otherwise let a fast bolt reach the barrier before a slow editor captures it.
            System.Action<LocalPlayer> freezeShot = player => { if (player == a) Time.timeScale = 0f; };
            roster.FireRequested += freezeShot;
            try
            {
                yield return Press(padA, GamepadButton.West);
                Assert.That(FindWeapon(WeaponKind.Bolt), Is.Not.Null);
                yield return Capture("projectile");
            }
            finally
            {
                roster.FireRequested -= freezeShot;
                Time.timeScale = 1f;
            }
            Place(b, new Vector3(0f, .8f, 15f), 90);
            yield return Press(padB, GamepadButton.West);
            Place(b, new Vector3(-7f, .8f, 15f), 90);
            yield return new WaitForSeconds(5.1f); // Show the armed slick together with the respawned pickup box.
            Assert.That(FindWeapon(WeaponKind.Oil), Is.Not.Null);
            Assert.That(FindWeapon(WeaponKind.Oil).IsArmed, Is.True);
            yield return Capture("oil-pickup");
        }

        private IEnumerator StartRace()
        {
            Assert.That(race.StartRace(), Is.True);
            yield return WaitForRaceStart();
        }

        private IEnumerator WaitForRaceStart()
        {
            // Wait for the state under test, allowing frame scheduling overhead during editor stalls.
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True, "Countdown must enter Racing within 10 seconds.");
        }

        private IEnumerator Collect(LocalPlayer player, int box, WeaponKind expected)
        {
            Assert.That(combat.Pickups[box].IsAvailable, Is.True);
            Place(player, WeaponManager.GetPickupPosition(box), 0);
            yield return new WaitForSeconds(.1f);
            Assert.That(combat.HeldWeapon(player.PlayerIndex), Is.EqualTo(expected));
            Assert.That(combat.Pickups[box].IsAvailable, Is.False);
        }

        private static void Place(LocalPlayer player, Vector3 position, float yaw)
        {
            player.Vehicle.Teleport(new Pose(position, Quaternion.Euler(0, yaw, 0)));
            player.ChaseCamera.SnapToTarget();
            Physics.SyncTransforms();
        }

        private LiveCombatWeapon FindWeapon(WeaponKind kind)
        {
            foreach (LiveCombatWeapon weapon in combat.LiveWeapons) if (weapon.Kind == kind) return weapon;
            return null;
        }

        private void AssertHud(string first, string second)
        {
            string text = "";
            foreach (TextMeshProUGUI label in hud.GetComponentsInChildren<TextMeshProUGUI>()) text += label.text;
            Assert.That(text, Does.Contain(first));
            Assert.That(text, Does.Contain(second));
        }

        private void Finish(LocalPlayer player)
        {
            for (int guard = 0; guard < 24 && !player.Racer.IsFinished; guard++)
                Assert.That(race.ReportCheckpoint(player.Racer.RacerId, player.Racer.NextCheckpointIndex), Is.True);
            Assert.That(player.Racer.IsFinished, Is.True);
        }

        private void AssertClean()
        {
            Assert.That(combat.HeldWeapon(0), Is.EqualTo(WeaponKind.None));
            Assert.That(combat.HeldWeapon(1), Is.EqualTo(WeaponKind.None));
            Assert.That(combat.LiveWeapons.Count, Is.Zero);
            Assert.That(combat.FeedbackCount, Is.Zero);
            Assert.That(a.Vehicle.HasCombatModifier, Is.False);
            Assert.That(b.Vehicle.HasCombatModifier, Is.False);
            foreach (PickupSpawner pickup in combat.Pickups) Assert.That(pickup.IsAvailable, Is.True);
        }

        private static IEnumerator Press(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return Frames();
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Frames();
        }

        private static IEnumerator Frames() { yield return null; yield return null; }

        private IEnumerator Capture(string suffix)
        {
            string path = "/private/tmp/re-re-volt-stage4-" + suffix + ".png";
            if (File.Exists(path)) File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path, 1);
            for (int frame = 0; frame < 120 && !File.Exists(path); frame++) yield return null;
            Assert.That(File.Exists(path), Is.True, path);
        }
    }
}
