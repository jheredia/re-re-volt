using System.Collections;
using NUnit.Framework;
using Presentation;
using ReReVolt.Online.Contracts;
using ReReVolt.Online.Unity;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.TestTools;

namespace ReReVolt.Tests.Online
{
    /// <summary>Two real NGO peers over local UDP. This does not validate Unity Relay or remote networks.</summary>
    public sealed class OnlineTwoPeerRematchTransportTests : InputTestFixture
    {
        private const ushort Port = 38003;
        private GameObject hostRoot, clientRoot;
        private NetworkManager hostNetwork, clientNetwork;
        private OnlineLobbyProtocol hostLobby, clientLobby;
        private OnlineRoundCoordinator hostRound, clientRound;

        [UnityTest]
        public IEnumerator ResultsRematchAndSecondRoundRetainPeerOwnershipAndReleaseRoundDevices()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad hostPad = InputSystem.AddDevice<Gamepad>();
            Gamepad clientPad = InputSystem.AddDevice<Gamepad>();

            hostRoot = new GameObject("Rematch transport host");
            var hostTransport = hostRoot.AddComponent<UnityTransport>();
            hostTransport.SetConnectionData("127.0.0.1", Port, "127.0.0.1");
            hostNetwork = hostRoot.AddComponent<NetworkManager>();
            hostNetwork.NetworkConfig = new NetworkConfig { NetworkTransport = hostTransport, EnableSceneManagement = false };
            Assert.That(hostNetwork.StartHost(), Is.True);

            clientRoot = new GameObject("Rematch transport client");
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
            uint hostId = 0, clientId = 0;
            foreach (LobbyRacer racer in hostLobby.Snapshot.Racers)
                if (racer.Peer == hostLobby.LocalPeer) hostId = racer.Id;
                else if (racer.Peer == clientLobby.LocalPeer) clientId = racer.Id;
            Assert.That(hostId, Is.Not.Zero);
            Assert.That(clientId, Is.Not.Zero);
            Assert.That(hostId, Is.Not.EqualTo(clientId));

            // Both readiness commands traverse the actual lobby protocol; only the
            // authoritative host can start a composed race.
            yield return ReadyAndStart(hostId, clientId, 1);
            OnlineRaceHostWorld firstWorld = hostRoot.GetComponent<OnlineRaceHostWorld>();
            Assert.That(firstWorld, Is.Not.Null);
            Assert.That(hostRoot.GetComponent<OnlineHostLocalPresentation>().LocalCameraCount, Is.EqualTo(1));
            Assert.That(clientRoot.GetComponent<OnlineRaceClientPresentation>().LocalCameraCount, Is.EqualTo(1));
            Assert.That(hostRoot.GetComponent<OnlineLocalInputSender>().LocalRacerCount, Is.EqualTo(1));
            Assert.That(clientRoot.GetComponent<OnlineLocalInputSender>().LocalRacerCount, Is.EqualTo(1));
            Assert.That(InputUser.FindUserPairedToDevice(keyboard).HasValue, Is.True);
            Assert.That(InputUser.FindUserPairedToDevice(clientPad).HasValue, Is.True);

            // Report ordered checkpoints through RaceManager, the same authority
            // used by physical trigger crossings. Do not set phase or lap fields.
            for (int lap = 0; lap < firstWorld.Race.TotalLaps; lap++)
                for (int checkpoint = 0; checkpoint < firstWorld.Race.CheckpointCount; checkpoint++)
                {
                    Assert.That(firstWorld.Race.ReportCheckpoint("online-" + hostId, checkpoint), Is.True);
                    Assert.That(firstWorld.Race.ReportCheckpoint("online-" + clientId, checkpoint), Is.True);
                }
            Assert.That(hostLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Results));
            Assert.That(firstWorld.CaptureSnapshot().Phase, Is.EqualTo(DrivingPhase.Results));

            deadline = Time.realtimeSinceStartup + 5f;
            while ((clientLobby.Snapshot?.Phase != OnlinePhase.Results ||
                    !HasStatus(clientRoot.GetComponent<OnlineRaceHud>(), clientId, "FINISHED")) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Results),
                "The remote peer must receive the host-owned Results phase.");
            Assert.That(HasStatus(clientRoot.GetComponent<OnlineRaceHud>(), clientId, "FINISHED"), Is.True,
                "The client HUD must consume a Results driving snapshot, not just a lobby phase.");

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
            Assert.That(hostLobby.Snapshot.Round, Is.EqualTo(1));
            AssertStableOwnership(hostLobby.Snapshot, hostId, clientId);
            AssertStableOwnership(clientLobby.Snapshot, hostId, clientId);
            yield return null; // Let deferred component destruction complete before rebinding devices.
            Assert.That(hostRoot.GetComponent<OnlineLocalInputSender>() == null, Is.True);
            Assert.That(clientRoot.GetComponent<OnlineLocalInputSender>() == null, Is.True);
            Assert.That(hostRoot.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(clientRoot.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(InputUser.FindUserPairedToDevice(keyboard).HasValue, Is.False);
            Assert.That(InputUser.FindUserPairedToDevice(hostPad).HasValue, Is.False);
            Assert.That(InputUser.FindUserPairedToDevice(clientPad).HasValue, Is.False);

            yield return ReadyAndStart(hostId, clientId, 2);
            OnlineRaceHostWorld secondWorld = hostRoot.GetComponent<OnlineRaceHostWorld>();
            Assert.That(secondWorld, Is.Not.Null);
            Assert.That(secondWorld, Is.Not.SameAs(firstWorld));
            Assert.That(hostRoot.GetComponent<OnlineHostLocalPresentation>().LocalCameraCount, Is.EqualTo(1));
            Assert.That(clientRoot.GetComponent<OnlineRaceClientPresentation>().LocalCameraCount, Is.EqualTo(1));
            Assert.That(hostRoot.GetComponent<OnlineLocalInputSender>().LocalRacerCount, Is.EqualTo(1));
            Assert.That(clientRoot.GetComponent<OnlineLocalInputSender>().LocalRacerCount, Is.EqualTo(1));
            Assert.That(InputUser.FindUserPairedToDevice(keyboard).HasValue, Is.True);
            Assert.That(InputUser.FindUserPairedToDevice(clientPad).HasValue, Is.True);

            deadline = Time.realtimeSinceStartup + 3f;
            ulong acknowledged = 0;
            while (acknowledged == 0 && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                foreach (DrivingRacerState state in secondWorld.CaptureSnapshot().Racers)
                    if (state.RacerId == clientId) acknowledged = state.AcknowledgedInput;
                yield return null;
            }
            Assert.That(acknowledged, Is.GreaterThan(0),
                "The retained client racer must submit fresh input in round two.");
            deadline = Time.realtimeSinceStartup + 2f;
            while (!HasStatus(clientRoot.GetComponent<OnlineRaceHud>(), clientId, "LAP") &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(HasStatus(clientRoot.GetComponent<OnlineRaceHud>(), clientId, "LAP"), Is.True,
                "The client needs a fresh round-two HUD snapshot rather than stale Results text.");

            clientNetwork.Shutdown();
            deadline = Time.realtimeSinceStartup + 3f;
            while (hostRound.IsRoundActive && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(hostRound.IsRoundActive, Is.False,
                "Disconnect must abort the new host round rather than retain a ghost racer.");
            Assert.That(hostLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Lobby));
            Assert.That(hostLobby.Snapshot.Racers, Has.Length.EqualTo(1));
            yield return null;
            Assert.That(InputUser.FindUserPairedToDevice(keyboard).HasValue, Is.False);
            Assert.That(InputUser.FindUserPairedToDevice(hostPad).HasValue, Is.False);
        }

        private IEnumerator ReadyAndStart(uint hostId, uint clientId, uint expectedRound)
        {
            clientLobby.Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = clientId, Ready = true });
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((!Ready(hostLobby.Snapshot, clientId) || clientLobby.RequestPending) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientLobby.LastError, Is.EqualTo(OnlineError.None));
            Assert.That(Ready(hostLobby.Snapshot, clientId), Is.True);
            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = hostId, Ready = true });
            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.Start });
            Assert.That(hostLobby.LastError, Is.EqualTo(OnlineError.None));
            Assert.That(hostLobby.Snapshot.Round, Is.EqualTo(expectedRound));
            Assert.That(hostRound.IsHostRound, Is.True);

            deadline = Time.realtimeSinceStartup + 7f;
            while ((!clientRound.IsRoundActive || clientLobby.Snapshot?.Phase != OnlinePhase.Racing) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientRound.IsRoundActive, Is.True);
            Assert.That(clientLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Racing));
            Assert.That(clientLobby.Snapshot.Round, Is.EqualTo(expectedRound));
            AssertStableOwnership(hostLobby.Snapshot, hostId, clientId);
            AssertStableOwnership(clientLobby.Snapshot, hostId, clientId);
        }

        private static bool Ready(LobbySnapshot snapshot, uint racerId)
        {
            if (snapshot?.Racers == null) return false;
            foreach (LobbyRacer racer in snapshot.Racers)
                if (racer.Id == racerId) return racer.Ready;
            return false;
        }

        private void AssertStableOwnership(LobbySnapshot snapshot, uint hostId, uint clientId)
        {
            Assert.That(snapshot.Racers, Has.Length.EqualTo(2));
            Assert.That(snapshot.Racers[0].Id, Is.EqualTo(hostId));
            Assert.That(snapshot.Racers[0].Peer, Is.EqualTo(hostLobby.LocalPeer));
            Assert.That(snapshot.Racers[0].Slot, Is.Zero);
            Assert.That(snapshot.Racers[1].Id, Is.EqualTo(clientId));
            Assert.That(snapshot.Racers[1].Peer, Is.EqualTo(clientLobby.LocalPeer));
            Assert.That(snapshot.Racers[1].Slot, Is.Zero);
        }

        private static bool HasStatus(OnlineRaceHud hud, uint racerId, string part) =>
            hud != null && hud.TryGetLocalStatus(racerId, out string status) && status.Contains(part);

        private void TickLobbies()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            hostLobby?.Tick(now);
            clientLobby?.Tick(now);
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
