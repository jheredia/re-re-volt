using System.Collections;
using Core;
using NUnit.Framework;
using Presentation;
using ReReVolt.Online.Contracts;
using ReReVolt.Online.Unity;
using UnityEngine;
using UnityEngine.TestTools;
using Vehicle;

namespace ReReVolt.Tests.Online
{
    public sealed class OnlineRaceHostWorldTests
    {
        private GameObject holder;
        private OnlineRaceHostWorld world;
        private GameManager global;
        private bool ownsGlobal;
        private GameManager.GameState priorState;
        private RaceManager priorRace;
        private float priorScale;

        [SetUp] public void SetUp()
        {
            priorScale = Time.timeScale;
            global = GameManager.Instance;
            ownsGlobal = global == null;
            if (ownsGlobal) global = new GameObject("Online world test global").AddComponent<GameManager>();
            priorState = global.CurrentState; priorRace = global.RaceManager;
            global.ReturnToMainMenu();
            holder = new GameObject("Online World Test");
            var offline = new GameObject("Offline rules"); offline.transform.SetParent(holder.transform);
            global.RaceManager = offline.AddComponent<RaceManager>();
            Assert.That(global.RaceManager.ControlsGlobalGameState, Is.True);
            world = holder.AddComponent<OnlineRaceHostWorld>();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (world != null) world.Shutdown();
            if (holder != null) Object.Destroy(holder);
            if (global != null)
            {
                if (ownsGlobal) Object.Destroy(global.gameObject);
                else { global.RaceManager = priorRace; global.SetState(priorState); }
            }
            Time.timeScale = priorScale;
            yield return null;
        }
        private static OnlineLobbyAuthority Lobby(string track, int bots, bool friend = false)
        {
            var authority = new OnlineLobbyAuthority("host", 12, new[] { track }, track);
            if (friend) Assert.That(authority.AdmitPeer("friend"), Is.EqualTo(OnlineError.None));
            Apply(authority, "host", new LobbyCommand { Action = LobbyAction.SetBots, BotCount = bots });
            foreach (var racer in authority.Snapshot().Racers)
                if (!racer.Bot) Apply(authority, racer.Peer,
                    new LobbyCommand { Action = LobbyAction.SetReady, RacerId = racer.Id, Ready = true });
            Apply(authority, "host", new LobbyCommand { Action = LobbyAction.Start });
            return authority;
        }
        private static void Apply(OnlineLobbyAuthority authority, string actor, LobbyCommand command)
        {
            command.Epoch = authority.Epoch; command.Revision = authority.Revision;
            Assert.That(authority.Apply(actor, command, true), Is.EqualTo(OnlineError.None));
        }

        [UnityTest] public IEnumerator HostBuildsAllTracksWithThreeBotsAndNoLocalDevicesOrCameras()
        {
            RaceManager offline = global.RaceManager;
            foreach (var definition in TrackCatalog.Entries)
            {
                var authority = Lobby(definition.Id, 3);
                world.Initialize(authority);
                Assert.That(world.RacerCount, Is.EqualTo(4));
                Assert.That(world.CombatInventory, Is.Not.Null);
                Assert.That(holder.GetComponentsInChildren<OnlineHostPickup>().Length, Is.EqualTo(4));
                Assert.That(world.CombatInventory.TryCollect(authority.Snapshot().Racers[0].Id,
                    0, 0, out _), Is.False, "The host cannot grant a pickup during countdown.");
                Assert.That(world.Race.ControlsGlobalGameState, Is.False);
                Assert.That(global.RaceManager, Is.SameAs(offline));
                Assert.That(global.CurrentState, Is.EqualTo(GameManager.GameState.MainMenu));
                Assert.That(holder.GetComponentsInChildren<InputHandler>().Length, Is.Zero);
                Assert.That(holder.GetComponentsInChildren<Camera>().Length, Is.Zero);
                Assert.That(holder.GetComponentsInChildren<AI.AIDriverController>().Length, Is.EqualTo(3));
                var snapshot = world.CaptureSnapshot();
                var receiver = new OnlineDrivingSnapshotReceiver(authority.Snapshot(), definition.LapCount, definition.OrderedCheckpointCount);
                Assert.That(receiver.Accept(snapshot), Is.EqualTo(SnapshotError.None));
                Assert.That(snapshot.Phase, Is.EqualTo(DrivingPhase.Countdown));
                world.Shutdown();
                Assert.That(world.CombatInventory, Is.Null);
                Assert.That(authority.Phase, Is.EqualTo(OnlinePhase.Lobby));
                Assert.That(global.CurrentState, Is.EqualTo(GameManager.GameState.MainMenu));
                yield return null; // Deferred object/material cleanup before building the next track.
            }
        }

        [UnityTest] public IEnumerator AuthoredHostPickupTriggerGrantsOnlyOneHeldWeaponDuringRacing()
        {
            var authority = Lobby(TrackCatalog.Default.Id, 0);
            uint human = authority.Snapshot().Racers[0].Id;
            world.Initialize(authority);
            float deadline = Time.realtimeSinceStartup + 6;
            while (world.Race.CurrentState == RaceManager.RaceState.Countdown &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.Race.CurrentState, Is.EqualTo(RaceManager.RaceState.Racing));
            yield return new WaitForFixedUpdate(); // Enable racing-only pickup triggers.
            Assert.That(world.TryGetVehicle(human, out VehicleController vehicle), Is.True);

            vehicle.Teleport(new Pose(PrototypeCircuitBuilder.GetPickupPosition(0), Quaternion.identity));
            deadline = Time.realtimeSinceStartup + 2;
            while (world.CombatInventory.HeldWeapon(human) == Weapons.WeaponKind.None &&
                   Time.realtimeSinceStartup < deadline) yield return new WaitForFixedUpdate();
            Assert.That(world.CombatInventory.HeldWeapon(human), Is.EqualTo(Weapons.WeaponKind.Bolt));
            Assert.That(world.CombatInventory.IsPickupAvailable(0, world.Race.ElapsedTime), Is.False);
            Assert.That(world.CaptureSnapshot().Racers[0].HeldWeapon, Is.EqualTo(Weapons.WeaponKind.Bolt));

            vehicle.Teleport(new Pose(PrototypeCircuitBuilder.GetPickupPosition(1), Quaternion.identity));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(world.CombatInventory.HeldWeapon(human), Is.EqualTo(Weapons.WeaponKind.Bolt));
            Assert.That(world.CombatInventory.IsPickupAvailable(1, world.Race.ElapsedTime), Is.True);
        }

        [UnityTest] public IEnumerator InputsAreHostOwnedAndPeerLossAbortsWithoutChangingOfflineState()
        {
            var authority = Lobby(TrackCatalog.Default.Id, 2, friend: true);
            world.Initialize(authority);
            uint human = authority.Snapshot().Racers[0].Id;
            int fireRequests = 0;
            world.ActionRequested += (racerId, edges) =>
            {
                if (racerId == human && (edges & InputEdges.Fire) != 0) fireRequests++;
            };
            var frame = new PeerInputFrame { Epoch = 12, Round = authority.Round, Sequence = 1,
                Racers = new[] { new RacerInputCommand { RacerId = human, Sequence = 1, Throttle = 1000,
                    Edges = InputEdges.Fire } } };
            Assert.That(world.SubmitInput("host", frame), Is.EqualTo(InputError.IllegalPhase));
            float deadline = Time.realtimeSinceStartup + 6;
            while (world.Race.CurrentState == RaceManager.RaceState.Countdown && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(authority.Phase, Is.EqualTo(OnlinePhase.Racing));
            Assert.That(world.SubmitInput("friend", frame), Is.EqualTo(InputError.NotOwner));
            Assert.That(world.SubmitInput("host", frame), Is.EqualTo(InputError.None));
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(world.CaptureSnapshot().Racers[0].AcknowledgedInput, Is.EqualTo(1));
            Assert.That(fireRequests, Is.EqualTo(1), "An owned Fire edge reaches only host action routing.");
            yield return new WaitForFixedUpdate();
            Assert.That(fireRequests, Is.EqualTo(1), "The host must consume a Fire edge once.");
            Assert.That(world.TryGetVehicle(human, out var car), Is.True);
            Assert.That(car.ThrottleInput, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForFixedUpdate();
            Assert.That(car.ThrottleInput, Is.Zero);
            Assert.That(global.CurrentState, Is.EqualTo(GameManager.GameState.MainMenu));
            Assert.That(authority.RemovePeer("friend"), Is.True);
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(world.IsInitialized, Is.False);
            Assert.That(world.RacerCount, Is.Zero);
            Assert.That(global.CurrentState, Is.EqualTo(GameManager.GameState.MainMenu));
        }

        [UnityTest] public IEnumerator OwnedFireConsumesHostInventoryButForeignFireCannot()
        {
            var authority = Lobby(TrackCatalog.Default.Id, 0, friend: true);
            uint human = authority.Snapshot().Racers[0].Id;
            world.Initialize(authority);
            float deadline = Time.realtimeSinceStartup + 6;
            while (world.Race.CurrentState == RaceManager.RaceState.Countdown &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.Race.CurrentState, Is.EqualTo(RaceManager.RaceState.Racing));
            Assert.That(world.CombatInventory.TryCollect(human, 0, world.Race.ElapsedTime, out var item), Is.True);
            Assert.That(item, Is.EqualTo(Weapons.WeaponKind.Bolt));
            var frame = new PeerInputFrame { Epoch = authority.Epoch, Round = authority.Round, Sequence = 1,
                Racers = new[] { new RacerInputCommand { RacerId = human, Sequence = 1, Edges = InputEdges.Fire } } };
            Assert.That(world.SubmitInput("friend", frame), Is.EqualTo(InputError.NotOwner));
            Assert.That(world.CombatInventory.HeldWeapon(human), Is.EqualTo(Weapons.WeaponKind.Bolt));
            Assert.That(world.SubmitInput("host", frame), Is.EqualTo(InputError.None));
            yield return new WaitForFixedUpdate();
            Assert.That(world.CombatInventory.HeldWeapon(human), Is.EqualTo(Weapons.WeaponKind.None));
            Assert.That(world.Combat.SimulationTime, Is.GreaterThan(0));
            Assert.That(world.Combat.TryFire(human), Is.False, "A consumed slot cannot be fired again.");
            Assert.That(world.CombatInventory.TryCollect(human, 1, world.Race.ElapsedTime, out item), Is.True);
            Assert.That(item, Is.EqualTo(Weapons.WeaponKind.Oil));
            yield return new WaitForSeconds(.4f);
            Assert.That(world.TryGetVehicle(human, out VehicleController car), Is.True);
            car.Teleport(new Pose(new Vector3(0, 100, 0), Quaternion.identity));
            Assert.That(world.Combat.TryFire(human), Is.False,
                "An oil fire request needs a validated drivable drop point.");
            Assert.That(world.CombatInventory.HeldWeapon(human), Is.EqualTo(Weapons.WeaponKind.Oil));
            world.Shutdown();
            Assert.That(world.Combat, Is.Null);
        }

        [UnityTest] public IEnumerator HostBoltHitsAnotherRacerButNeverTheShooter()
        {
            var authority = Lobby(TrackCatalog.Default.Id, 0, friend: true);
            var roster = authority.Snapshot().Racers;
            uint shooterId = roster[0].Id, targetId = roster[1].Id;
            world.Initialize(authority);
            float deadline = Time.realtimeSinceStartup + 6;
            while (world.Race.CurrentState == RaceManager.RaceState.Countdown &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.Race.CurrentState, Is.EqualTo(RaceManager.RaceState.Racing));
            Assert.That(world.TryGetVehicle(shooterId, out VehicleController shooter), Is.True);
            Assert.That(world.TryGetVehicle(targetId, out VehicleController target), Is.True);
            shooter.Teleport(new Pose(new Vector3(0, 10, 0), Quaternion.identity));
            target.Teleport(new Pose(new Vector3(0, 10, 3), Quaternion.identity));
            Physics.SyncTransforms();
            Assert.That(world.CombatInventory.TryCollect(shooterId, 0, world.Race.ElapsedTime, out _), Is.True);
            Assert.That(world.Combat.TryFire(shooterId), Is.True);
            Assert.That(world.Combat.LiveCount, Is.EqualTo(1));
            deadline = Time.realtimeSinceStartup + 1;
            while (world.Combat.HitCount == 0 && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();
            Assert.That(world.Combat.HitCount, Is.EqualTo(1), "Only the target accepts the swept Bolt hit.");
            Assert.That(world.Combat.LiveCount, Is.Zero);
        }

        [UnityTest] public IEnumerator HostOilArmsOnDrivableRoadAndHitsAnotherRacer()
        {
            var authority = Lobby(TrackCatalog.Default.Id, 0, friend: true);
            var roster = authority.Snapshot().Racers;
            uint shooterId = roster[0].Id, targetId = roster[1].Id;
            world.Initialize(authority);
            float deadline = Time.realtimeSinceStartup + 6;
            while (world.Race.CurrentState == RaceManager.RaceState.Countdown &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.Race.CurrentState, Is.EqualTo(RaceManager.RaceState.Racing));
            Assert.That(world.TryGetVehicle(shooterId, out VehicleController shooter), Is.True);
            Assert.That(world.TryGetVehicle(targetId, out VehicleController target), Is.True);
            shooter.Teleport(new Pose(new Vector3(-15, .6f, -5), Quaternion.identity));
            target.Teleport(new Pose(new Vector3(0, 10, 0), Quaternion.identity));
            Physics.SyncTransforms();
            Assert.That(world.CombatInventory.TryCollect(shooterId, 1, world.Race.ElapsedTime, out var item), Is.True);
            Assert.That(item, Is.EqualTo(Weapons.WeaponKind.Oil));
            Assert.That(world.Combat.TryFire(shooterId), Is.True);
            target.Teleport(new Pose(new Vector3(-15, .6f, -7.7f), Quaternion.identity));
            Physics.SyncTransforms();
            world.Combat.Step(.61f);
            Assert.That(world.Combat.HitCount, Is.EqualTo(1));
            Assert.That(world.Combat.LiveCount, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator HostLocalViewsFollowOnlyOwnedAuthoritativeCarsAndReleaseCleanly()
        {
            var authority = new OnlineLobbyAuthority("host", 13,
                new[] { TrackCatalog.Default.Id }, TrackCatalog.Default.Id);
            Apply(authority, "host", new LobbyCommand { Action = LobbyAction.AddHuman, Slot = 1 });
            foreach (var racer in authority.Snapshot().Racers)
                Apply(authority, "host", new LobbyCommand { Action = LobbyAction.SetReady,
                    RacerId = racer.Id, Ready = true });
            Apply(authority, "host", new LobbyCommand { Action = LobbyAction.Start });
            world.Initialize(authority);

            var views = holder.AddComponent<OnlineHostLocalPresentation>();
            views.Initialize(authority.Snapshot(), "host", world);
            Assert.That(views.LocalCameraCount, Is.EqualTo(2));
            Camera[] cameras = views.GetComponentsInChildren<Camera>();
            Assert.That(cameras.Length, Is.EqualTo(2));
            Assert.That(cameras[0].rect, Is.EqualTo(new Rect(0, .5f, 1, .5f)));
            Assert.That(cameras[1].rect, Is.EqualTo(new Rect(0, 0, 1, .5f)));
            Assert.That(views.GetComponentsInChildren<AudioListener>(), Is.Empty);
            Assert.That(holder.GetComponentsInChildren<VehicleController>().Length, Is.EqualTo(2),
                "Host views must not duplicate authoritative vehicles.");
            views.Shutdown();
            yield return null;
            Assert.That(views.GetComponentsInChildren<Camera>(true), Is.Empty);
        }

        [UnityTest] public IEnumerator OrderedHostCheckpointsProduceResultsAndRetainedIdsCanRematch()
        {
            var authority = Lobby(TrackCatalog.Default.Id, 1);
            var original = authority.Snapshot();
            world.Initialize(authority);
            float deadline = Time.realtimeSinceStartup + 6;
            while (world.Race.CurrentState == RaceManager.RaceState.Countdown && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.Race.CurrentState, Is.EqualTo(RaceManager.RaceState.Racing));
            foreach (var racer in original.Racers)
                for (int lap = 0; lap < world.Race.TotalLaps; lap++)
                    for (int cp = 0; cp < world.Race.CheckpointCount; cp++)
                        Assert.That(world.Race.ReportCheckpoint("online-" + racer.Id, cp), Is.True);
            Assert.That(authority.Phase, Is.EqualTo(OnlinePhase.Results));
            var snapshot = world.CaptureSnapshot();
            Assert.That(snapshot.Phase, Is.EqualTo(DrivingPhase.Results));
            Assert.That(snapshot.Racers[0].FinishPosition, Is.EqualTo(1));
            Assert.That(snapshot.Racers[1].FinishPosition, Is.EqualTo(2));
            Assert.That(new OnlineDrivingSnapshotReceiver(original, world.Race.TotalLaps, world.Race.CheckpointCount).Accept(snapshot), Is.EqualTo(SnapshotError.None));
            Assert.That(global.CurrentState, Is.EqualTo(GameManager.GameState.MainMenu));
            world.Shutdown();
            Apply(authority, "host", new LobbyCommand { Action = LobbyAction.Rematch });
            foreach (var racer in authority.Snapshot().Racers)
                if (!racer.Bot) Apply(authority, racer.Peer, new LobbyCommand { Action = LobbyAction.SetReady, RacerId = racer.Id, Ready = true });
            Apply(authority, "host", new LobbyCommand { Action = LobbyAction.Start });
            yield return null;
            world.Initialize(authority);
            snapshot = world.CaptureSnapshot();
            Assert.That(snapshot.Round, Is.EqualTo(2));
            Assert.That(snapshot.Racers[0].RacerId, Is.EqualTo(original.Racers[0].Id));
            Assert.That(snapshot.Racers[1].RacerId, Is.EqualTo(original.Racers[1].Id));
            Assert.That(snapshot.Racers[0].FinishPosition, Is.Zero);
            Assert.That(snapshot.Racers[0].CompletedLaps, Is.Zero);
        }

        [UnityTest] public IEnumerator ResultsRemainHostAuthoritativeUntilExplicitRematchAfterWorldCleanup()
        {
            var authority = Lobby(TrackCatalog.Default.Id, 0);
            world.Initialize(authority);
            float deadline = Time.realtimeSinceStartup + 6;
            while (world.Race.CurrentState == RaceManager.RaceState.Countdown && Time.realtimeSinceStartup < deadline) yield return null;
            uint racer = authority.Snapshot().Racers[0].Id;
            for (int lap = 0; lap < world.Race.TotalLaps; lap++)
                for (int checkpoint = 0; checkpoint < world.Race.CheckpointCount; checkpoint++)
                    Assert.That(world.Race.ReportCheckpoint("online-" + racer, checkpoint), Is.True);

            Assert.That(authority.Phase, Is.EqualTo(OnlinePhase.Results));
            world.Shutdown();
            Assert.That(authority.Phase, Is.EqualTo(OnlinePhase.Results),
                "Round teardown must not silently erase the host's results/rematch state.");
            Apply(authority, "host", new LobbyCommand { Action = LobbyAction.Rematch });
            Assert.That(authority.Phase, Is.EqualTo(OnlinePhase.Lobby));
        }
    }
}
