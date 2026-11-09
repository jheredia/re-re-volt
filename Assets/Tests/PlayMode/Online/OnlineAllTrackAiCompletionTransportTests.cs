using System.Collections;
using System.Collections.Generic;
using AI;
using Core;
using NUnit.Framework;
using Presentation;
using ReReVolt.Online.Contracts;
using ReReVolt.Online.Unity;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Vehicle;

namespace ReReVolt.Tests.Online
{
    /// <summary>
    /// Two NGO peers over local UDP. The host bot must physically cross each authored
    /// checkpoint; only the parked human racers use direct ordered checkpoint reports.
    /// This does not prove Unity Relay or a separate-machine session.
    /// </summary>
    public sealed class OnlineAllTrackAiCompletionTransportTests : InputTestFixture
    {
        private const ushort Port = 38011;
        private const float AutonomousDeadlineSeconds = 90f;
        private GameObject hostRoot, clientRoot;
        private NetworkManager hostNetwork, clientNetwork;
        private OnlineLobbyProtocol hostLobby, clientLobby;
        private OnlineRoundCoordinator hostRound, clientRound;

        [UnityTest]
        public IEnumerator WorkshopBotFinishesMixedPeerRace() => ValidateTrack(TrackCatalog.WorkshopCircuitId);

        [UnityTest]
        public IEnumerator PatioBotFinishesMixedPeerRace() => ValidateTrack(TrackCatalog.PatioSweepId);

        [UnityTest]
        public IEnumerator MarketBotFinishesMixedPeerRace() => ValidateTrack(TrackCatalog.MarketAislesId);

        [UnityTest]
        public IEnumerator BridgeBotFinishesMixedPeerRace() => ValidateTrack(TrackCatalog.BridgeExhibitionId);

        private IEnumerator ValidateTrack(string trackId)
        {
            Time.timeScale = 1f;
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Gamepad>();
            InputSystem.AddDevice<Gamepad>();

            hostRoot = new GameObject("All-track AI host");
            var hostTransport = hostRoot.AddComponent<UnityTransport>();
            hostTransport.SetConnectionData("127.0.0.1", Port, "127.0.0.1");
            hostNetwork = hostRoot.AddComponent<NetworkManager>();
            hostNetwork.NetworkConfig = new NetworkConfig { NetworkTransport = hostTransport, EnableSceneManagement = false };
            Assert.That(hostNetwork.StartHost(), Is.True);

            clientRoot = new GameObject("All-track AI client");
            var clientTransport = clientRoot.AddComponent<UnityTransport>();
            clientTransport.SetConnectionData("127.0.0.1", Port);
            clientNetwork = clientRoot.AddComponent<NetworkManager>();
            clientNetwork.NetworkConfig = new NetworkConfig { NetworkTransport = clientTransport, EnableSceneManagement = false };
            Assert.That(clientNetwork.StartClient(), Is.True);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!clientNetwork.IsConnectedClient && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(clientNetwork.IsConnectedClient, Is.True);

            hostLobby = new OnlineLobbyProtocol(hostNetwork, Time.realtimeSinceStartupAsDouble);
            clientLobby = new OnlineLobbyProtocol(clientNetwork, Time.realtimeSinceStartupAsDouble);
            hostRound = hostRoot.AddComponent<OnlineRoundCoordinator>();
            clientRound = clientRoot.AddComponent<OnlineRoundCoordinator>();
            hostRound.Initialize(hostLobby, hostRoot.AddComponent<Stage3Bootstrap>());
            clientRound.Initialize(clientLobby, clientRoot.AddComponent<Stage3Bootstrap>());
            deadline = Time.realtimeSinceStartup + 5f;
            while ((clientLobby.Snapshot == null || hostLobby.Snapshot.Racers.Length != 2) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientLobby.Snapshot, Is.Not.Null);
            Assert.That(hostLobby.Snapshot.Racers, Has.Length.EqualTo(2));

            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.SelectTrack, TrackId = trackId });
            Assert.That(hostLobby.LastError, Is.EqualTo(OnlineError.None));
            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 1 });
            Assert.That(hostLobby.LastError, Is.EqualTo(OnlineError.None));
            deadline = Time.realtimeSinceStartup + 5f;
            while ((clientLobby.Snapshot == null || clientLobby.Snapshot.Revision != hostLobby.Snapshot.Revision) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientLobby.Snapshot, Is.Not.Null);
            Assert.That(clientLobby.Snapshot.Revision, Is.EqualTo(hostLobby.Snapshot.Revision));
            Assert.That(clientLobby.Snapshot.TrackId, Is.EqualTo(trackId));
            Assert.That(clientLobby.Snapshot.Racers, Has.Length.EqualTo(3));

            uint hostHuman = 0, clientHuman = 0, botId = 0;
            foreach (LobbyRacer racer in hostLobby.Snapshot.Racers)
            {
                if (racer.Bot) botId = racer.Id;
                else if (racer.Peer == hostLobby.LocalPeer) hostHuman = racer.Id;
                else if (racer.Peer == clientLobby.LocalPeer) clientHuman = racer.Id;
            }
            Assert.That(hostHuman, Is.Not.Zero);
            Assert.That(clientHuman, Is.Not.Zero);
            Assert.That(botId, Is.Not.Zero);

            clientLobby.Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = clientHuman, Ready = true });
            deadline = Time.realtimeSinceStartup + 5f;
            while ((!Ready(hostLobby.Snapshot, clientHuman) || clientLobby.RequestPending) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientLobby.LastError, Is.EqualTo(OnlineError.None));
            Assert.That(Ready(hostLobby.Snapshot, clientHuman), Is.True);
            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = hostHuman, Ready = true });
            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.Start });
            Assert.That(hostLobby.LastError, Is.EqualTo(OnlineError.None));

            var world = hostRoot.GetComponent<OnlineRaceHostWorld>();
            Assert.That(world, Is.Not.Null);
            Assert.That(world.RacerCount, Is.EqualTo(3));
            AIDriverController[] drivers = world.GetComponentsInChildren<AIDriverController>(true);
            Assert.That(drivers, Has.Length.EqualTo(1));
            AIDriverController driver = drivers[0];
            List<int> accepted = new(world.Race.TotalLaps * world.Race.CheckpointCount);
            world.Race.RacerProgressChanged += racer =>
            {
                if (object.ReferenceEquals(racer, driver.Racer)) accepted.Add(racer.LastCheckpointIndex);
            };

            deadline = Time.realtimeSinceStartup + 7f;
            while ((!clientRound.IsRoundActive || clientLobby.Snapshot?.Phase != OnlinePhase.Racing) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientRound.IsRoundActive, Is.True);
            Assert.That(clientLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Racing));
            Assert.That(clientLobby.Snapshot.TrackId, Is.EqualTo(trackId));
            Assert.That(world.Race.RaceRunning, Is.True);
            Assert.That(world.CombatInventory.TryCollect(botId, 0, world.Race.ElapsedTime, out _), Is.False,
                "Driving-only bots must not acquire an online weapon slot during Racing.");
            Assert.That(hostRoot.GetComponent<OnlineHostLocalPresentation>().LocalCameraCount, Is.EqualTo(1));
            Assert.That(hostRoot.GetComponent<OnlineLocalInputSender>().LocalRacerCount, Is.EqualTo(1));
            Assert.That(clientRoot.GetComponent<OnlineLocalInputSender>().LocalRacerCount, Is.EqualTo(1));
            Assert.That(clientRoot.GetComponentsInChildren<AIDriverController>(true), Is.Empty);
            Assert.That(clientRoot.GetComponentsInChildren<VehicleController>(true), Is.Empty);
            Assert.That(clientRoot.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            var clientView = clientRoot.GetComponent<OnlineRaceClientPresentation>();
            Assert.That(clientView.ProxyCount, Is.EqualTo(3));
            Assert.That(clientView.LocalCameraCount, Is.EqualTo(1));
            Assert.That(clientView.TryGetLocalCamera(clientHuman, out _), Is.True);
            Assert.That(clientView.TryGetLocalCamera(botId, out _), Is.False);
            Assert.That(clientView.TryGetProxy(botId, out Transform botProxy), Is.True);

            // Park finished humans on the venue apron, away from the bot's racing
            // line. Only the bot's progress must come from physical triggers.
            Collider venue = FindVenueGround(world);
            Assert.That(venue, Is.Not.Null, trackId + " needs an apron for parked humans.");
            Bounds bounds = venue.bounds;
            Assert.That(world.TryGetVehicle(hostHuman, out VehicleController hostCar), Is.True);
            Assert.That(world.TryGetVehicle(clientHuman, out VehicleController clientCar), Is.True);
            hostCar.Teleport(new Pose(new Vector3(bounds.max.x - 2f, bounds.max.y + .65f,
                bounds.max.z - 2f), Quaternion.identity));
            clientCar.Teleport(new Pose(new Vector3(bounds.max.x - 5f, bounds.max.y + .65f,
                bounds.max.z - 2f), Quaternion.identity));
            Physics.SyncTransforms();
            for (int lap = 0; lap < world.Race.TotalLaps; lap++)
                for (int checkpoint = 0; checkpoint < world.Race.CheckpointCount; checkpoint++)
                {
                    Assert.That(world.Race.ReportCheckpoint("online-" + hostHuman, checkpoint), Is.True);
                    Assert.That(world.Race.ReportCheckpoint("online-" + clientHuman, checkpoint), Is.True);
                }

            yield return new WaitForSecondsRealtime(.25f);
            Vector3 initialProxyPosition = botProxy.position;
            Assert.That(Vector3.Distance(initialProxyPosition, Position(FindState(world.CaptureSnapshot(), botId))),
                Is.LessThan(8f), trackId + " client must receive the first host bot snapshot.");
            bool clientObservedBotMotion = false;
            float finishDeadline = Time.realtimeSinceStartup + AutonomousDeadlineSeconds;
            while (!driver.Racer.IsFinished && Time.realtimeSinceStartup < finishDeadline)
            {
                TickLobbies();
                Vector3 proxyDelta = botProxy.position - initialProxyPosition;
                proxyDelta.y = 0f;
                clientObservedBotMotion |= proxyDelta.sqrMagnitude > 1f;
                yield return new WaitForFixedUpdate();
            }
            string diagnostic = trackId + " online bot timed out: lap=" + driver.Racer.CurrentLap +
                "/" + world.Race.TotalLaps + ", next=" + driver.Racer.NextCheckpointIndex +
                ", accepted=" + accepted.Count + ", speed=" + driver.Vehicle.CurrentSpeed.ToString("0.0") +
                "km/h, recoveries=" + driver.RecoveryCount + ", position=" + driver.transform.position + ".";
            Assert.That(driver.Racer.IsFinished, Is.True, diagnostic);
            Assert.That(accepted, Has.Count.EqualTo(world.Race.TotalLaps * world.Race.CheckpointCount));
            for (int index = 0; index < accepted.Count; index++)
                Assert.That(accepted[index], Is.EqualTo(index % world.Race.CheckpointCount),
                    trackId + " bot crossed checkpoints out of order at " + index + ".");
            Assert.That(clientObservedBotMotion, Is.True, trackId + " client must receive bot snapshots.");
            Assert.That(world.Race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
            Assert.That(world.CaptureSnapshot().Phase, Is.EqualTo(DrivingPhase.Results));

            deadline = Time.realtimeSinceStartup + 5f;
            while ((clientLobby.Snapshot?.Phase != OnlinePhase.Results ||
                    !HasStatus(clientRoot.GetComponent<OnlineRaceHud>(), clientHuman, "FINISHED")) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Results));
            Assert.That(HasStatus(clientRoot.GetComponent<OnlineRaceHud>(), clientHuman, "FINISHED"), Is.True);
            Assert.That(clientView.LocalCameraCount, Is.EqualTo(1));
            Assert.That(clientView.TryGetLocalCamera(botId, out _), Is.False);

            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.Rematch });
            Assert.That(hostLobby.LastError, Is.EqualTo(OnlineError.None));
            deadline = Time.realtimeSinceStartup + 5f;
            while ((clientLobby.Snapshot?.Phase != OnlinePhase.Lobby ||
                    hostRound.IsRoundActive || clientRound.IsRoundActive) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(hostLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Lobby));
            Assert.That(clientLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Lobby));
            Assert.That(hostRound.IsRoundActive || clientRound.IsRoundActive, Is.False);
            yield return null; // Allow deferred round-object destruction before peer teardown.
        }

        private void TickLobbies()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            hostLobby?.Tick(now);
            clientLobby?.Tick(now);
        }

        private static bool Ready(LobbySnapshot snapshot, uint racerId)
        {
            if (snapshot?.Racers == null) return false;
            foreach (LobbyRacer racer in snapshot.Racers)
                if (racer.Id == racerId) return racer.Ready;
            return false;
        }

        private static Collider FindVenueGround(OnlineRaceHostWorld world)
        {
            foreach (Transform child in world.GetComponentsInChildren<Transform>(true))
                if (child.name == "VenueGround" || child.name == "Workbench Surface")
                    return child.GetComponent<Collider>();
            return null;
        }

        private static DrivingRacerState FindState(DrivingWorldSnapshot snapshot, uint racerId)
        {
            foreach (DrivingRacerState state in snapshot.Racers)
                if (state.RacerId == racerId) return state;
            Assert.Fail("The host snapshot dropped racer " + racerId + ".");
            return default;
        }

        private static Vector3 Position(DrivingRacerState state) =>
            new(state.Position.X, state.Position.Y, state.Position.Z);

        private static bool HasStatus(OnlineRaceHud hud, uint racerId, string part) =>
            hud != null && hud.TryGetLocalStatus(racerId, out string status) && status.Contains(part);

        [UnityTearDown]
        public IEnumerator CleanupPeers()
        {
            Time.timeScale = 1f;
            hostRound?.AbortRound();
            clientRound?.AbortRound();
            hostLobby?.Dispose();
            clientLobby?.Dispose();
            if (clientNetwork != null && clientNetwork.IsListening) clientNetwork.Shutdown();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (clientNetwork != null && (clientNetwork.IsListening || clientNetwork.ShutdownInProgress) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            if (hostNetwork != null && hostNetwork.IsListening) hostNetwork.Shutdown();
            deadline = Time.realtimeSinceStartup + 3f;
            while (hostNetwork != null && (hostNetwork.IsListening || hostNetwork.ShutdownInProgress) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            if (clientRoot != null) Object.Destroy(clientRoot);
            if (hostRoot != null) Object.Destroy(hostRoot);
            yield return null;
        }
    }
}
