using System.Collections;
using NUnit.Framework;
using Presentation;
using ReReVolt.Online.Contracts;
using ReReVolt.Online.Unity;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace ReReVolt.Tests.Online
{
    /// <summary>Two NGO peers over local UDP; this is not a Unity Relay or cross-network test.</summary>
    public sealed class OnlineTwoPeerLocalTransportTests : InputTestFixture
    {
        private const ushort Port = 37993;
        private GameObject hostRoot, clientRoot;
        private NetworkManager hostNetwork, clientNetwork;
        private OnlineLobbyProtocol hostLobby, clientLobby;
        private OnlineRoundCoordinator hostRound, clientRound;

        [UnityTest]
        public IEnumerator ClientJoinsHostSubmitsOwnedInputAndDisconnectClearsRound()
        {
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Gamepad>(); // Host's optional solo fallback.
            InputSystem.AddDevice<Gamepad>(); // Client's independently paired device.

            hostRoot = new GameObject("Local transport host");
            var hostTransport = hostRoot.AddComponent<UnityTransport>();
            hostTransport.SetConnectionData("127.0.0.1", Port, "127.0.0.1");
            hostNetwork = hostRoot.AddComponent<NetworkManager>();
            hostNetwork.NetworkConfig = new NetworkConfig { NetworkTransport = hostTransport, EnableSceneManagement = false };
            Assert.That(hostNetwork.StartHost(), Is.True);

            clientRoot = new GameObject("Local transport client");
            var clientTransport = clientRoot.AddComponent<UnityTransport>();
            clientTransport.SetConnectionData("127.0.0.1", Port);
            clientNetwork = clientRoot.AddComponent<NetworkManager>();
            clientNetwork.NetworkConfig = new NetworkConfig { NetworkTransport = clientTransport, EnableSceneManagement = false };
            Assert.That(clientNetwork.StartClient(), Is.True);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!clientNetwork.IsConnectedClient && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(clientNetwork.IsConnectedClient, Is.True, "A second NGO transport must join the local host.");

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
            Assert.That(hostLobby.Snapshot.Racers.Length, Is.EqualTo(2));
            Assert.That(clientLobby.Snapshot, Is.Not.Null, "The client must receive the host's admitted roster.");
            uint hostId = 0, clientId = 0;
            foreach (LobbyRacer racer in hostLobby.Snapshot.Racers)
                if (racer.Peer == hostLobby.LocalPeer) hostId = racer.Id;
                else if (racer.Peer == clientLobby.LocalPeer) clientId = racer.Id;
            Assert.That(hostId, Is.Not.Zero);
            Assert.That(clientId, Is.Not.Zero);

            clientLobby.Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = clientId, Ready = true });
            deadline = Time.realtimeSinceStartup + 5f;
            while (!IsReady(hostLobby.Snapshot, clientId) && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(IsReady(hostLobby.Snapshot, clientId), Is.True);
            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = hostId, Ready = true });
            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.Start });
            Assert.That(hostRound.IsHostRound, Is.True);

            deadline = Time.realtimeSinceStartup + 7f;
            while ((!clientRound.IsRoundActive || clientLobby.Snapshot?.Phase != OnlinePhase.Racing) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientRound.IsRoundActive, Is.True, "The admitted client must compose a visual-only round.");
            Assert.That(clientLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Racing));
            Assert.That(clientRoot.GetComponent<OnlineRaceClientPresentation>().LocalCameraCount, Is.EqualTo(1));

            deadline = Time.realtimeSinceStartup + 3f;
            ulong acknowledged = 0;
            while (acknowledged == 0 && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                foreach (DrivingRacerState state in hostRoot.GetComponent<OnlineRaceHostWorld>().CaptureSnapshot().Racers)
                    if (state.RacerId == clientId) acknowledged = state.AcknowledgedInput;
                yield return null;
            }
            Assert.That(acknowledged, Is.GreaterThan(0), "The host must accept the client's owned input frame.");
            var clientHud = clientRoot.GetComponent<OnlineRaceHud>();
            deadline = Time.realtimeSinceStartup + 2f;
            while (!clientHud.TryGetLocalStatus(clientId, out string status) || !status.Contains("LAP"))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The client HUD needs an accepted host snapshot.");
                yield return null;
            }

            clientNetwork.Shutdown();
            deadline = Time.realtimeSinceStartup + 3f;
            while (hostRound.IsRoundActive && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(hostRound.IsRoundActive, Is.False,
                "A departed peer must abort the host round rather than leave a ghost racer.");
        }

        private void TickLobbies()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            hostLobby?.Tick(now);
            clientLobby?.Tick(now);
        }

        private static bool IsReady(LobbySnapshot snapshot, uint racerId)
        {
            if (snapshot?.Racers == null) return false;
            foreach (LobbyRacer racer in snapshot.Racers)
                if (racer.Id == racerId) return racer.Ready;
            return false;
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
