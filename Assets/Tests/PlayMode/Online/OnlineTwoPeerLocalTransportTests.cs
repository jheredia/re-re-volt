using System.Collections;
using NUnit.Framework;
using Presentation;
using ReReVolt.Online.Contracts;
using ReReVolt.Online.Unity;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.TestTools;
using Vehicle;

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

        [UnityTest]
        public IEnumerator TwoPeersOwnFourIndependentLocalRacersAndReleaseThemOnDisconnect()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad hostPad = InputSystem.AddDevice<Gamepad>();
            Gamepad firstClientPad = InputSystem.AddDevice<Gamepad>();
            Gamepad secondClientPad = InputSystem.AddDevice<Gamepad>();

            hostRoot = new GameObject("Four-racer local transport host");
            var hostTransport = hostRoot.AddComponent<UnityTransport>();
            hostTransport.SetConnectionData("127.0.0.1", Port, "127.0.0.1");
            hostNetwork = hostRoot.AddComponent<NetworkManager>();
            hostNetwork.NetworkConfig = new NetworkConfig { NetworkTransport = hostTransport, EnableSceneManagement = false };
            Assert.That(hostNetwork.StartHost(), Is.True);

            clientRoot = new GameObject("Four-racer local transport client");
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

            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.AddHuman, Slot = 1 });
            Assert.That(hostLobby.Snapshot.Racers, Has.Length.EqualTo(3));
            deadline = Time.realtimeSinceStartup + 5f;
            while ((clientLobby.Snapshot.Racers.Length != 3 ||
                    clientLobby.Snapshot.Revision != hostLobby.Snapshot.Revision) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientLobby.Snapshot.Racers, Has.Length.EqualTo(3));
            Assert.That(clientLobby.Snapshot.Revision, Is.EqualTo(hostLobby.Snapshot.Revision));

            clientLobby.Submit(new LobbyCommand { Action = LobbyAction.AddHuman, Slot = 1 });
            deadline = Time.realtimeSinceStartup + 5f;
            while ((hostLobby.Snapshot.Racers.Length != 4 || clientLobby.Snapshot.Racers.Length != 4 ||
                    clientLobby.RequestPending) && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(hostLobby.Snapshot.Racers, Has.Length.EqualTo(4));
            Assert.That(clientLobby.Snapshot.Racers, Has.Length.EqualTo(4));
            Assert.That(clientLobby.LastError, Is.EqualTo(OnlineError.None));
            LobbyRacer[] hostRacers = OwnedHumans(hostLobby.Snapshot, hostLobby.LocalPeer);
            LobbyRacer[] clientRacers = OwnedHumans(hostLobby.Snapshot, clientLobby.LocalPeer);
            Assert.That(hostRacers, Has.Length.EqualTo(2));
            Assert.That(clientRacers, Has.Length.EqualTo(2));
            Assert.That(hostRacers[0].Slot, Is.EqualTo(0));
            Assert.That(hostRacers[1].Slot, Is.EqualTo(1));
            Assert.That(clientRacers[0].Slot, Is.EqualTo(0));
            Assert.That(clientRacers[1].Slot, Is.EqualTo(1));

            foreach (LobbyRacer racer in hostRacers)
                hostLobby.Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = racer.Id, Ready = true });
            deadline = Time.realtimeSinceStartup + 5f;
            while (clientLobby.Snapshot.Revision != hostLobby.Snapshot.Revision && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientLobby.Snapshot.Revision, Is.EqualTo(hostLobby.Snapshot.Revision));
            foreach (LobbyRacer racer in clientRacers)
            {
                clientLobby.Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = racer.Id, Ready = true });
                deadline = Time.realtimeSinceStartup + 5f;
                while ((!IsReady(hostLobby.Snapshot, racer.Id) || clientLobby.RequestPending) &&
                       Time.realtimeSinceStartup < deadline)
                {
                    TickLobbies();
                    yield return null;
                }
                Assert.That(IsReady(hostLobby.Snapshot, racer.Id), Is.True);
                Assert.That(clientLobby.LastError, Is.EqualTo(OnlineError.None));
            }
            foreach (LobbyRacer racer in hostLobby.Snapshot.Racers)
                Assert.That(racer.Ready, Is.True, "Every local racer must ready independently before Start.");

            hostLobby.Submit(new LobbyCommand { Action = LobbyAction.Start });
            Assert.That(hostLobby.LastError, Is.EqualTo(OnlineError.None));
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
            Assert.That(hostRoot.GetComponent<OnlineRaceHostWorld>().RacerCount, Is.EqualTo(4));
            Assert.That(hostRoot.GetComponent<OnlineHostLocalPresentation>().LocalCameraCount, Is.EqualTo(2));
            Assert.That(clientRoot.GetComponent<OnlineRaceClientPresentation>().LocalCameraCount, Is.EqualTo(2));
            Assert.That(hostRoot.GetComponent<OnlineLocalInputSender>().LocalRacerCount, Is.EqualTo(2));
            Assert.That(clientRoot.GetComponent<OnlineLocalInputSender>().LocalRacerCount, Is.EqualTo(2));

            Camera[] hostCameras = hostRoot.GetComponentsInChildren<Camera>();
            Assert.That(hostCameras, Has.Length.EqualTo(2));
            Assert.That(hostCameras[0], Is.Not.SameAs(hostCameras[1]));
            Assert.That(hostCameras[0].rect.y, Is.Not.EqualTo(hostCameras[1].rect.y));
            var clientView = clientRoot.GetComponent<OnlineRaceClientPresentation>();
            Assert.That(clientView.TryGetLocalCamera(clientRacers[0].Id, out Camera firstCamera), Is.True);
            Assert.That(clientView.TryGetLocalCamera(clientRacers[1].Id, out Camera secondCamera), Is.True);
            Assert.That(firstCamera, Is.Not.SameAs(secondCamera));
            Assert.That(firstCamera.rect.y, Is.Not.EqualTo(secondCamera.rect.y));
            foreach (LobbyRacer racer in hostRacers)
                Assert.That(clientView.TryGetLocalCamera(racer.Id, out _), Is.False,
                    "A remote racer must not receive a local client viewport.");

            InputUser? keyboardOwner = InputUser.FindUserPairedToDevice(keyboard);
            InputUser? hostPadOwner = InputUser.FindUserPairedToDevice(hostPad);
            InputUser? firstClientPadOwner = InputUser.FindUserPairedToDevice(firstClientPad);
            InputUser? secondClientPadOwner = InputUser.FindUserPairedToDevice(secondClientPad);
            Assert.That(keyboardOwner.HasValue && hostPadOwner.HasValue &&
                firstClientPadOwner.HasValue && secondClientPadOwner.HasValue, Is.True);
            Assert.That(hostPadOwner.Value.id, Is.Not.EqualTo(keyboardOwner.Value.id));
            Assert.That(firstClientPadOwner.Value.id, Is.Not.EqualTo(hostPadOwner.Value.id));
            Assert.That(secondClientPadOwner.Value.id, Is.Not.EqualTo(firstClientPadOwner.Value.id));
            Assert.That(secondClientPadOwner.Value.id, Is.Not.EqualTo(keyboardOwner.Value.id));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            InputSystem.QueueStateEvent(hostPad, new GamepadState { rightTrigger = .35f });
            InputSystem.QueueStateEvent(firstClientPad, new GamepadState { rightTrigger = .55f });
            InputSystem.QueueStateEvent(secondClientPad, new GamepadState { rightTrigger = .75f });
            InputSystem.Update();
            var world = hostRoot.GetComponent<OnlineRaceHostWorld>();
            deadline = Time.realtimeSinceStartup + 3f;
            while ((!HasThrottle(world, hostRacers[0].Id, 1f) ||
                    !HasThrottle(world, hostRacers[1].Id, .35f) ||
                    !HasThrottle(world, clientRacers[0].Id, .55f) ||
                    !HasThrottle(world, clientRacers[1].Id, .75f)) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            AssertThrottle(world, hostRacers[0].Id, 1f);
            AssertThrottle(world, hostRacers[1].Id, .35f);
            AssertThrottle(world, clientRacers[0].Id, .55f);
            AssertThrottle(world, clientRacers[1].Id, .75f);
            DrivingWorldSnapshot snapshot = world.CaptureSnapshot();
            Assert.That(snapshot.Racers, Has.Length.EqualTo(4));
            foreach (LobbyRacer racer in clientRacers)
            {
                DrivingRacerState state = FindState(snapshot, racer.Id);
                Assert.That(state.RacerId, Is.EqualTo(racer.Id));
                Assert.That(state.AcknowledgedInput, Is.GreaterThan(0),
                    "The host must acknowledge each client-owned input independently.");
            }

            var clientHud = clientRoot.GetComponent<OnlineRaceHud>();
            deadline = Time.realtimeSinceStartup + 3f;
            while ((!HasLapStatus(clientHud, clientRacers[0].Id) ||
                    !HasLapStatus(clientHud, clientRacers[1].Id)) && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(HasLapStatus(clientHud, clientRacers[0].Id), Is.True,
                "The first client racer needs an accepted four-racer host snapshot.");
            Assert.That(HasLapStatus(clientHud, clientRacers[1].Id), Is.True,
                "The second client racer needs an accepted four-racer host snapshot.");

            clientNetwork.Shutdown();
            deadline = Time.realtimeSinceStartup + 3f;
            while (hostRound.IsRoundActive && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(hostRound.IsRoundActive, Is.False,
                "Disconnecting a peer that owns two racers must abort the four-racer round.");
            clientRound.AbortRound();
            yield return null;
            Assert.That(hostRoot.GetComponent<OnlineLocalInputSender>() == null, Is.True);
            Assert.That(clientRoot.GetComponent<OnlineLocalInputSender>() == null, Is.True);
            Assert.That(InputUser.FindUserPairedToDevice(keyboard).HasValue, Is.False);
            Assert.That(InputUser.FindUserPairedToDevice(hostPad).HasValue, Is.False);
            Assert.That(InputUser.FindUserPairedToDevice(firstClientPad).HasValue, Is.False);
            Assert.That(InputUser.FindUserPairedToDevice(secondClientPad).HasValue, Is.False);
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

        private static LobbyRacer[] OwnedHumans(LobbySnapshot snapshot, string peer)
        {
            var owned = new System.Collections.Generic.List<LobbyRacer>(2);
            foreach (LobbyRacer racer in snapshot.Racers)
                if (!racer.Bot && racer.Peer == peer) owned.Add(racer);
            owned.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            return owned.ToArray();
        }

        private static void AssertThrottle(OnlineRaceHostWorld world, uint racerId, float expected)
        {
            Assert.That(world.TryGetVehicle(racerId, out VehicleController vehicle), Is.True);
            Assert.That(vehicle.ThrottleInput, Is.EqualTo(expected).Within(.05f),
                "Each locally paired device must control only its own host-authoritative vehicle.");
        }

        private static bool HasThrottle(OnlineRaceHostWorld world, uint racerId, float expected) =>
            world.TryGetVehicle(racerId, out VehicleController vehicle) &&
            Mathf.Abs(vehicle.ThrottleInput - expected) <= .05f;

        private static DrivingRacerState FindState(DrivingWorldSnapshot snapshot, uint racerId)
        {
            foreach (DrivingRacerState state in snapshot.Racers)
                if (state.RacerId == racerId) return state;
            return default;
        }

        private static bool HasLapStatus(OnlineRaceHud hud, uint racerId) =>
            hud.TryGetLocalStatus(racerId, out string status) && status.Contains("LAP");

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
