using System.Collections;
using AI;
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
using Weapons;

namespace ReReVolt.Tests.Online
{
    /// <summary>Real local UDP peers, not a Relay or cross-machine test.</summary>
    public sealed class OnlineTwoPeerMixedAiTransportTests : InputTestFixture
    {
        private const ushort Port = 37995;
        private GameObject hostRoot, clientRoot;
        private NetworkManager hostNetwork, clientNetwork;
        private OnlineLobbyProtocol hostLobby, clientLobby;
        private OnlineRoundCoordinator hostRound, clientRound;

        [UnityTest]
        public IEnumerator PatioRoundKeepsTwoBotsHostOwnedAndReplicatesTheirMovement()
        {
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Gamepad>();
            InputSystem.AddDevice<Gamepad>();

            hostRoot = new GameObject("Mixed-AI local UDP host");
            var hostTransport = hostRoot.AddComponent<UnityTransport>();
            hostTransport.SetConnectionData("127.0.0.1", Port, "127.0.0.1");
            hostNetwork = hostRoot.AddComponent<NetworkManager>();
            hostNetwork.NetworkConfig = new NetworkConfig { NetworkTransport = hostTransport, EnableSceneManagement = false };
            Assert.That(hostNetwork.StartHost(), Is.True);

            clientRoot = new GameObject("Mixed-AI local UDP client");
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
            Assert.That(hostLobby.Snapshot.Racers, Has.Length.EqualTo(2));
            Assert.That(clientLobby.Snapshot, Is.Not.Null);

            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.SelectTrack, TrackId = TrackCatalog.PatioSweepId });
            Assert.That(hostLobby.LastError, Is.EqualTo(OnlineError.None));
            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 2 });
            Assert.That(hostLobby.LastError, Is.EqualTo(OnlineError.None));
            Assert.That(hostLobby.Snapshot.TrackId, Is.EqualTo(TrackCatalog.PatioSweepId));
            Assert.That(hostLobby.Snapshot.Racers, Has.Length.EqualTo(4));
            // A fifth racer cannot displace a selected bot or silently exceed the cap.
            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.AddHuman, Slot = 1 });
            Assert.That(hostLobby.LastError, Is.EqualTo(OnlineError.Capacity));
            Assert.That(hostLobby.Snapshot.Racers, Has.Length.EqualTo(4));

            deadline = Time.realtimeSinceStartup + 5f;
            while ((clientLobby.Snapshot.Racers.Length != 4 ||
                    clientLobby.Snapshot.Revision != hostLobby.Snapshot.Revision) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientLobby.Snapshot.Racers, Has.Length.EqualTo(4));
            Assert.That(clientLobby.Snapshot.TrackId, Is.EqualTo(TrackCatalog.PatioSweepId));
            Assert.That(clientLobby.Snapshot.Revision, Is.EqualTo(hostLobby.Snapshot.Revision));

            uint hostHuman = 0, clientHuman = 0;
            var bots = new uint[2];
            int botCount = 0;
            foreach (LobbyRacer racer in hostLobby.Snapshot.Racers)
            {
                if (racer.Bot) bots[botCount++] = racer.Id;
                else if (racer.Peer == hostLobby.LocalPeer) hostHuman = racer.Id;
                else if (racer.Peer == clientLobby.LocalPeer) clientHuman = racer.Id;
            }
            Assert.That(botCount, Is.EqualTo(2));
            Assert.That(hostHuman, Is.Not.Zero);
            Assert.That(clientHuman, Is.Not.Zero);
            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = hostHuman, Ready = true });
            deadline = Time.realtimeSinceStartup + 5f;
            while (clientLobby.Snapshot.Revision != hostLobby.Snapshot.Revision &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientLobby.Snapshot.Revision, Is.EqualTo(hostLobby.Snapshot.Revision));
            clientLobby.Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = clientHuman, Ready = true });
            deadline = Time.realtimeSinceStartup + 5f;
            while ((!Ready(hostLobby.Snapshot, clientHuman) || clientLobby.RequestPending) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(Ready(hostLobby.Snapshot, clientHuman), Is.True);
            Assert.That(clientLobby.LastError, Is.EqualTo(OnlineError.None));

            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.Start });
            Assert.That(hostLobby.LastError, Is.EqualTo(OnlineError.None));
            Assert.That(hostRound.IsHostRound, Is.True);
            Assert.That(hostLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Countdown));
            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 1 });
            Assert.That(hostLobby.LastError, Is.EqualTo(OnlineError.Locked),
                "The round's four-racer roster must remain locked through countdown and racing.");
            Assert.That(hostLobby.Snapshot.Racers, Has.Length.EqualTo(4));

            var world = hostRoot.GetComponent<OnlineRaceHostWorld>();
            Assert.That(world, Is.Not.Null);
            Assert.That(world.RacerCount, Is.EqualTo(4));
            Assert.That(world.GetComponentsInChildren<AIDriverController>(true), Has.Length.EqualTo(2));
            Assert.That(hostRoot.GetComponent<OnlineHostLocalPresentation>().LocalCameraCount, Is.EqualTo(1));
            Assert.That(hostRoot.GetComponent<OnlineLocalInputSender>().LocalRacerCount, Is.EqualTo(1));
            DrivingWorldSnapshot first = world.CaptureSnapshot();
            Assert.That(first.TrackId, Is.EqualTo(TrackCatalog.PatioSweepId));
            Assert.That(first.Racers, Has.Length.EqualTo(4));
            Vector3 firstBotPosition = Position(FindState(first, bots[0]));
            Vector3 secondBotPosition = Position(FindState(first, bots[1]));

            deadline = Time.realtimeSinceStartup + 7f;
            while ((!clientRound.IsRoundActive || clientLobby.Snapshot?.Phase != OnlinePhase.Racing) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientRound.IsRoundActive, Is.True);
            Assert.That(clientLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Racing));
            Assert.That(clientLobby.Snapshot.TrackId, Is.EqualTo(TrackCatalog.PatioSweepId));
            Assert.That(clientLobby.Snapshot.Racers, Has.Length.EqualTo(4));
            Assert.That(clientRoot.GetComponentsInChildren<AIDriverController>(true), Is.Empty);
            Assert.That(clientRoot.GetComponentsInChildren<VehicleController>(true), Is.Empty);
            Assert.That(clientRoot.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(clientRoot.GetComponentsInChildren<WheelCollider>(true), Is.Empty);
            var clientView = clientRoot.GetComponent<OnlineRaceClientPresentation>();
            Assert.That(clientView.ProxyCount, Is.EqualTo(4));
            Assert.That(clientView.LocalCameraCount, Is.EqualTo(1));
            Assert.That(clientRoot.GetComponent<OnlineLocalInputSender>().LocalRacerCount, Is.EqualTo(1));
            Assert.That(clientView.TryGetLocalCamera(clientHuman, out _), Is.True);
            Assert.That(clientView.TryGetLocalCamera(bots[0], out _), Is.False);
            Assert.That(clientView.TryGetLocalCamera(bots[1], out _), Is.False);
            Assert.That(clientView.TryGetProxy(bots[0], out Transform firstProxy), Is.True);
            Assert.That(clientView.TryGetProxy(bots[1], out Transform secondProxy), Is.True);
            Assert.That(world.CombatInventory.TryCollect(bots[0], 0, world.Race.ElapsedTime, out _), Is.False,
                "Driving-only bots must not own a weapon inventory slot.");

            deadline = Time.realtimeSinceStartup + 5f;
            bool firstMoved = false, secondMoved = false;
            while ((!firstMoved || !secondMoved) && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                DrivingWorldSnapshot state = world.CaptureSnapshot();
                firstMoved |= Advanced(FindState(state, bots[0]), firstBotPosition);
                secondMoved |= Advanced(FindState(state, bots[1]), secondBotPosition);
                yield return null;
            }
            Assert.That(firstMoved && secondMoved, Is.True,
                "Both host-owned bots must autonomously move or accept ordered checkpoints during Racing.");
            yield return new WaitForSecondsRealtime(.25f); // Allow the next accepted network snapshot to reach visual proxies.
            DrivingWorldSnapshot latest = world.CaptureSnapshot();
            Assert.That(Vector3.Distance(firstProxy.position, Position(FindState(latest, bots[0]))), Is.LessThan(8f));
            Assert.That(Vector3.Distance(secondProxy.position, Position(FindState(latest, bots[1]))), Is.LessThan(8f));
            Assert.That(FindState(latest, bots[0]).HeldWeapon, Is.EqualTo(WeaponKind.None));
            Assert.That(FindState(latest, bots[1]).HeldWeapon, Is.EqualTo(WeaponKind.None));

            clientNetwork.Shutdown();
            deadline = Time.realtimeSinceStartup + 3f;
            while (hostRound.IsRoundActive && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(hostRound.IsRoundActive, Is.False,
                "Disconnecting the remote human must abort the host's mixed-AI round.");
        }

        private void TickLobbies()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            hostLobby?.Tick(now);
            clientLobby?.Tick(now);
        }

        private static bool Ready(LobbySnapshot snapshot, uint id)
        {
            foreach (LobbyRacer racer in snapshot.Racers)
                if (racer.Id == id) return racer.Ready;
            return false;
        }

        private static DrivingRacerState FindState(DrivingWorldSnapshot snapshot, uint id)
        {
            foreach (DrivingRacerState racer in snapshot.Racers)
                if (racer.RacerId == id) return racer;
            Assert.Fail("The host snapshot dropped racer " + id + ".");
            return default;
        }

        private static Vector3 Position(DrivingRacerState racer) =>
            new Vector3(racer.Position.X, racer.Position.Y, racer.Position.Z);

        private static bool Advanced(DrivingRacerState racer, Vector3 initial)
        {
            Vector3 delta = Position(racer) - initial;
            delta.y = 0;
            return delta.sqrMagnitude > 1f || racer.CompletedLaps > 0 || racer.NextCheckpoint > 0;
        }

        [UnityTearDown]
        public IEnumerator CleanupPeers()
        {
            hostRound?.AbortRound();
            clientRound?.AbortRound();
            hostLobby?.Dispose();
            clientLobby?.Dispose();
            if (clientNetwork != null && clientNetwork.IsListening) clientNetwork.Shutdown();
            if (hostNetwork != null && hostNetwork.IsListening) hostNetwork.Shutdown();
            if (clientRoot != null) Object.Destroy(clientRoot);
            if (hostRoot != null) Object.Destroy(hostRoot);
            yield return null;
        }
    }
}
