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
using UnityEngine.TestTools;
using Vehicle;

namespace ReReVolt.Tests.Online
{
    /// <summary>Local NGO host smoke; it does not claim Relay or a second peer.</summary>
    public sealed class OnlineRoundLoopbackIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private NetworkManager network;
        private OnlineLobbyProtocol protocol;
        private OnlineRoundCoordinator coordinator;

        [UnityTest]
        public IEnumerator HostCountdownPublishesRacingAndUnlocksFirstOwnedInput()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            root = new GameObject("Online loopback host test");
            UnityTransport transport = root.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 0, "127.0.0.1");
            network = root.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            Assert.That(network.StartHost(), Is.True, "Local NGO transport must start without Relay.");
            float deadline = Time.realtimeSinceStartup + 3;
            while (!network.IsConnectedClient && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(network.IsConnectedClient, Is.True);

            protocol = new OnlineLobbyProtocol(network, Time.realtimeSinceStartupAsDouble);
            coordinator = root.AddComponent<OnlineRoundCoordinator>();
            var bootstrap = root.AddComponent<Stage3Bootstrap>(); // An inert presentation dependency; do not initialize offline play.
            coordinator.Initialize(protocol, bootstrap);
            uint racerId = protocol.Snapshot.Racers[0].Id;
            protocol.Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = racerId, Ready = true });
            Assert.That(protocol.Snapshot.Racers[0].Ready, Is.True);
            protocol.Submit(new LobbyCommand { Action = LobbyAction.Start });
            Assert.That(coordinator.IsHostRound, Is.True);
            Assert.That(protocol.Snapshot.Phase, Is.EqualTo(OnlinePhase.Countdown));

            deadline = Time.realtimeSinceStartup + 6;
            while (protocol.Snapshot.Phase == OnlinePhase.Countdown && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(protocol.Snapshot.Phase, Is.EqualTo(OnlinePhase.Racing),
                "The host must publish its world-owned Racing transition to input senders.");
            deadline = Time.realtimeSinceStartup + 1;
            while (Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(root.GetComponent<OnlineRaceHostWorld>().CaptureSnapshot().Racers[0].AcknowledgedInput,
                Is.GreaterThan(0), "The local sender must submit an owned frame after phase publication.");
            Assert.That(root.GetComponent<OnlineRaceHostWorld>().TryGetVehicle(racerId, out VehicleController vehicle),
                Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            InputSystem.Update();
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(vehicle.ThrottleInput, Is.GreaterThan(.9f));
            coordinator.SetLocalControlsSuspended(true);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(vehicle.ThrottleInput, Is.Zero,
                "An open local menu must send neutral input without pausing the host race.");
            coordinator.SetLocalControlsSuspended(false);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(vehicle.ThrottleInput, Is.GreaterThan(.9f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        [UnityTearDown]
        public IEnumerator CleanupRoundAndTransport()
        {
            if (coordinator != null) coordinator.AbortRound();
            protocol?.Dispose();
            if (network != null && network.IsListening) network.Shutdown();
            if (root != null) Object.Destroy(root);
            yield return null;
        }
    }
}
