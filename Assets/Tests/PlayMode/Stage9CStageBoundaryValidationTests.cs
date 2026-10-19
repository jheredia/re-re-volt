using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
using Weapons;

namespace Tests.PlayMode
{
    /// <summary>
    /// Stage-boundary-only evidence for authored tracks. This fixture deliberately lives in test source and does
    /// not alter production authority: checkpoint triggers remain the only progress path, recovery uses the shipped
    /// vehicle recovery component, and pickup/fire requests use the paired keyboard path.
    /// </summary>
    public sealed class Stage9CStageBoundaryValidationTests : InputTestFixture
    {
        private GameObject root;
        private ITrackRuntime runtime;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            runtime?.Dispose();
            runtime = null;
            if (root != null) Object.DestroyImmediate(root);
            Assert.That(InputUser.all.Count, Is.Zero);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator PatioBotRecoversCollectsCombatAndFinishesThreeLaps() =>
            ValidateAuthoredTrack(TrackCatalog.PatioSweepId);

        [UnityTest]
        public IEnumerator MarketBotRecoversCollectsCombatAndFinishesThreeLaps() =>
            ValidateAuthoredTrack(TrackCatalog.MarketAislesId);

        [UnityTest]
        public IEnumerator BridgeBotRecoversCollectsCombatAndFinishesThreeLaps() =>
            ValidateAuthoredTrack(TrackCatalog.BridgeExhibitionId);

        private IEnumerator ValidateAuthoredTrack(string trackId)
        {
            Assert.That(TrackCatalog.TryGet(trackId, out TrackDefinition definition), Is.True);
            root = new GameObject($"Stage9Boundary_{trackId}");
            RaceManager race = root.AddComponent<RaceManager>();
            typeof(RaceManager).GetField("countdownTime", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(race, 0f);
            Assert.That(race.ConfigureRace(definition.LapCount, definition.OrderedCheckpointCount), Is.True);
            TrackSession tracks = new(TrackCatalog.Entries, trackId);
            runtime = tracks.BuildSelected(race, root.transform, null);
            Assert.That(runtime, Is.Not.Null);
            Assert.That(runtime.Grid, Has.Length.EqualTo(4));
            Assert.That(runtime.Checkpoints, Has.Length.EqualTo(definition.OrderedCheckpointCount));
            Assert.That(runtime.PickupPositions, Has.Count.EqualTo(4));

            VehicleManager humans = root.AddComponent<VehicleManager>();
            Assert.That(humans.Configure(race, runtime.Grid), Is.True);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(humans.TryJoinKeyboard(keyboard, out LocalPlayer human), Is.True);
            human.Recovery.ConfigureGroundMask(runtime.DrivableMask);

            AIManager ai = root.AddComponent<AIManager>();
            Assert.That(ai.Configure(race, humans, runtime.Grid, runtime.Checkpoints, runtime.DrivableMask), Is.True);
            Stage3RacePresentation presentation = root.AddComponent<Stage3RacePresentation>();
            presentation.Configure(race, humans, aiManager: ai);
            WeaponManager weapons = root.AddComponent<WeaponManager>();
            weapons.Configure(race, humans, presentation, runtime.DrivableMask);
            Assert.That(weapons.TryReconfigureTrack(runtime.DrivableMask, runtime.PickupPositions), Is.True);
            Assert.That(weapons.Pickups, Has.Count.EqualTo(4));
            Physics.SyncTransforms();
            for (int i = 0; i < weapons.Pickups.Count; i++)
            {
                Assert.That(Vector3.Distance(weapons.Pickups[i].transform.position, runtime.PickupPositions[i]),
                    Is.LessThan(.001f), $"{trackId} pickup {i} must use authored placement.");
                Assert.That(Physics.Raycast(runtime.PickupPositions[i] + Vector3.up * 3f, Vector3.down,
                        out _, 6f, runtime.DrivableMask, QueryTriggerInteraction.Ignore), Is.True,
                    $"{trackId} pickup {i} needs drivable support.");
            }

            Assert.That(ai.TrySetSelectedBotCount(1), Is.True);
            Assert.That(ai.PrepareRaceRoster(), Is.True, ai.LastRosterError);
            AIDriverController driver = ai.Drivers[0];
            List<int> accepted = new(race.CheckpointCount * race.TotalLaps);
            race.RacerProgressChanged += racer =>
            {
                if (object.ReferenceEquals(racer, driver.Racer)) accepted.Add(racer.LastCheckpointIndex);
            };

            bool fired = false;
            weapons.WeaponFired += (player, kind) =>
            {
                if (object.ReferenceEquals(player, human) && kind != WeaponKind.None) fired = true;
            };
            Assert.That(race.StartRace(), Is.True);
            float startDeadline = Time.realtimeSinceStartup + 5f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < startDeadline) yield return null;
            Assert.That(race.RaceRunning, Is.True, trackId);

            Pose pickupPose = new(runtime.PickupPositions[0] + Vector3.up * .25f, runtime.Grid[0].rotation);
            human.Vehicle.Teleport(pickupPose);
            Physics.SyncTransforms();
            float pickupDeadline = Time.realtimeSinceStartup + 2f;
            while (weapons.HeldWeapon(human.PlayerIndex) == WeaponKind.None &&
                   Time.realtimeSinceStartup < pickupDeadline)
                yield return new WaitForFixedUpdate();
            Assert.That(weapons.HeldWeapon(human.PlayerIndex), Is.Not.EqualTo(WeaponKind.None),
                $"{trackId} first authored pickup should be collectable.");
            yield return PressAndRelease(keyboard, Key.Space);
            Assert.That(fired, Is.True, $"{trackId} must accept a paired human combat request.");

            // The combat probe intentionally teleports the human onto pickup 0. Park it on the venue apron
            // afterward so this boundary test measures autonomous route following rather than a stationary
            // test vehicle blocking the opening straight.
            Transform venueGround = runtime.Root.transform.Find("VenueGround");
            Assert.That(venueGround, Is.Not.Null);
            Collider venueCollider = venueGround.GetComponent<Collider>();
            Assert.That(venueCollider, Is.Not.Null);
            Bounds venueBounds = venueCollider.bounds;
            Vector3 parkedPosition = new(venueBounds.max.x - 2f, venueBounds.max.y + .65f,
                venueBounds.max.z - 2f);
            human.Vehicle.Teleport(new Pose(parkedPosition, runtime.Grid[0].rotation));
            Physics.SyncTransforms();

            driver.Vehicle.Teleport(new Pose(new Vector3(45f, 3f, 45f), Quaternion.identity));
            Physics.SyncTransforms();
            float recoveryDeadline = Time.realtimeSinceStartup + 5f;
            while (driver.RecoveryCount == 0 && Time.realtimeSinceStartup < recoveryDeadline)
                yield return new WaitForFixedUpdate();
            Assert.That(driver.RecoveryCount, Is.GreaterThanOrEqualTo(1),
                $"{trackId} must recover from a deterministic off-track pose.");

            int humanGuard = race.TotalLaps * race.CheckpointCount + 1;
            while (!human.Racer.IsFinished && humanGuard-- > 0)
                Assert.That(race.ReportCheckpoint(human.Racer.RacerId, human.Racer.NextCheckpointIndex), Is.True);
            Assert.That(human.Racer.IsFinished, Is.True);

            const float autonomousDeadlineSeconds = 90f;
            float finishDeadline = Time.realtimeSinceStartup + autonomousDeadlineSeconds;
            while (!driver.Racer.IsFinished && Time.realtimeSinceStartup < finishDeadline)
                yield return new WaitForFixedUpdate();

            string diagnostic = $"{trackId} timed out: lap={driver.Racer.CurrentLap}/{race.TotalLaps}, " +
                                $"next={driver.Racer.NextCheckpointIndex}, accepted={accepted.Count}, " +
                                $"speed={driver.Vehicle.CurrentSpeed:0.0}km/h, recoveries={driver.RecoveryCount}, " +
                                $"position={driver.transform.position}.";
            Assert.That(driver.Racer.IsFinished, Is.True, diagnostic);
            Assert.That(accepted, Has.Count.EqualTo(race.TotalLaps * race.CheckpointCount));
            for (int i = 0; i < accepted.Count; i++)
                Assert.That(accepted[i], Is.EqualTo(i % race.CheckpointCount),
                    $"{trackId} ordered crossing diverged at {i}.");
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
        }

        private static IEnumerator PressAndRelease(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }
    }
}
