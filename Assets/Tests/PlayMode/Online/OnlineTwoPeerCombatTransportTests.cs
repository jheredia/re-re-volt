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
using Weapons;

namespace ReReVolt.Tests.Online
{
    /// <summary>Two NGO peers over local UDP; this does not validate Unity Relay or remote machines.</summary>
    public sealed class OnlineTwoPeerCombatTransportTests : InputTestFixture
    {
        private const ushort Port = 37994;
        private GameObject hostRoot, clientRoot;
        private NetworkManager hostNetwork, clientNetwork;
        private OnlineLobbyProtocol hostLobby, clientLobby;
        private OnlineRoundCoordinator hostRound, clientRound;

        [UnityTest]
        public IEnumerator ClientPickupFireAndHitReachItsHudAndCombatPresentation()
        {
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Gamepad>(); // Paired to the solo host as an optional fallback.
            Gamepad clientPad = InputSystem.AddDevice<Gamepad>();

            hostRoot = new GameObject("Combat transport host");
            var hostTransport = hostRoot.AddComponent<UnityTransport>();
            hostTransport.SetConnectionData("127.0.0.1", Port, "127.0.0.1");
            hostNetwork = hostRoot.AddComponent<NetworkManager>();
            hostNetwork.NetworkConfig = new NetworkConfig { NetworkTransport = hostTransport, EnableSceneManagement = false };
            Assert.That(hostNetwork.StartHost(), Is.True);

            clientRoot = new GameObject("Combat transport client");
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
            Assert.That(hostLobby.Snapshot.Racers.Length, Is.EqualTo(2));
            Assert.That(clientLobby.Snapshot, Is.Not.Null);
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
            deadline = Time.realtimeSinceStartup + 7f;
            while ((!clientRound.IsRoundActive || clientLobby.Snapshot?.Phase != OnlinePhase.Racing) &&
                   Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(clientRound.IsRoundActive, Is.True);
            Assert.That(clientLobby.Snapshot.Phase, Is.EqualTo(OnlinePhase.Racing));
            yield return new WaitForFixedUpdate();

            var world = hostRoot.GetComponent<OnlineRaceHostWorld>();
            var clientHud = clientRoot.GetComponent<OnlineRaceHud>();
            var clientCombat = clientRoot.GetComponent<OnlineCombatPresentation>();
            Assert.That(world, Is.Not.Null);
            Assert.That(clientHud, Is.Not.Null);
            Assert.That(clientCombat, Is.Not.Null);
            Assert.That(world.TryGetVehicle(clientId, out VehicleController shooter), Is.True);
            Assert.That(world.TryGetVehicle(hostId, out VehicleController target), Is.True);

            // Collect by touching the authored host trigger, never by granting a client inventory locally.
            shooter.Teleport(new Pose(world.PickupPositions[1], Quaternion.identity));
            Physics.SyncTransforms();
            deadline = Time.realtimeSinceStartup + 3f;
            while (world.CombatInventory.HeldWeapon(clientId) == WeaponKind.None &&
                   Time.realtimeSinceStartup < deadline) yield return new WaitForFixedUpdate();
            Assert.That(world.CombatInventory.HeldWeapon(clientId), Is.EqualTo(WeaponKind.Oil));
            Assert.That(world.CombatInventory.IsPickupAvailable(1, world.Race.ElapsedTime), Is.False);
            deadline = Time.realtimeSinceStartup + 2f;
            while ((!clientHud.TryGetLocalStatus(clientId, out string status) || !status.Contains("OIL") ||
                    clientCombat.VisiblePickupCount != 3) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(clientHud.TryGetLocalStatus(clientId, out string heldStatus) && heldStatus.Contains("OIL"),
                Is.True, "The client HUD must receive the host-owned inventory state.");
            Assert.That(clientCombat.VisiblePickupCount, Is.EqualTo(3),
                "The client must hide the consumed pickup from a host snapshot.");

            shooter.Teleport(new Pose(new Vector3(-15f, .6f, -5f), Quaternion.identity));
            target.Teleport(new Pose(new Vector3(0f, 10f, 0f), Quaternion.identity));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            ulong beforeFire = AcknowledgedInput(world.CaptureSnapshot(), clientId);
            InputSystem.QueueStateEvent(clientPad, new GamepadState().WithButton(GamepadButton.West));
            yield return null;
            InputSystem.QueueStateEvent(clientPad, new GamepadState());
            deadline = Time.realtimeSinceStartup + 2f;
            while (world.Combat.LiveCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.Combat.LiveCount, Is.EqualTo(1),
                "Only the host may turn the client's Fire edge into a live weapon.");
            Assert.That(AcknowledgedInput(world.CaptureSnapshot(), clientId), Is.GreaterThan(beforeFire));
            Assert.That(world.CombatInventory.HeldWeapon(clientId), Is.EqualTo(WeaponKind.None));
            DrivingWeaponState oil = world.CaptureSnapshot().Weapons[0];
            Assert.That(oil.ShooterId, Is.EqualTo(clientId));
            Assert.That(oil.Kind, Is.EqualTo(WeaponKind.Oil));
            deadline = Time.realtimeSinceStartup + 2f;
            while ((clientCombat.LiveWeaponViewCount == 0 ||
                    !clientHud.TryGetLocalStatus(clientId, out string statusAfterFire) ||
                    !statusAfterFire.Contains("NO ITEM")) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(clientCombat.LiveWeaponViewCount, Is.EqualTo(1),
                "A live host weapon must reach the client's visual-only presentation.");
            Assert.That(clientHud.TryGetLocalStatus(clientId, out string emptyStatus) && emptyStatus.Contains("NO ITEM"),
                Is.True);

            // Place the other authoritative racer on the host's actual oil position after arming.
            target.Teleport(new Pose(new Vector3(oil.Position.X, .6f, oil.Position.Z), Quaternion.identity));
            Physics.SyncTransforms();
            deadline = Time.realtimeSinceStartup + 2f;
            while (world.Combat.HitCount == 0 && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();
            Assert.That(world.Combat.HitCount, Is.EqualTo(1));
            DrivingCombatEvent[] events = world.CaptureSnapshot().CombatEvents;
            Assert.That(events.Length, Is.EqualTo(1));
            Assert.That(events[0].Kind, Is.EqualTo(DrivingCombatEventKind.Hit));
            Assert.That(events[0].ShooterId, Is.EqualTo(clientId));
            Assert.That(events[0].TargetId, Is.EqualTo(hostId));
            deadline = Time.realtimeSinceStartup + 2f;
            while (clientCombat.ActivePulseCount == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(clientCombat.ActivePulseCount, Is.GreaterThan(0),
                "The client's hit pulse must come from the host's accepted event history.");

            clientNetwork.Shutdown();
            deadline = Time.realtimeSinceStartup + 3f;
            while (hostRound.IsRoundActive && Time.realtimeSinceStartup < deadline)
            {
                TickLobbies();
                yield return null;
            }
            Assert.That(hostRound.IsRoundActive, Is.False,
                "The host must abort the combat round when its peer disconnects.");
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

        private static ulong AcknowledgedInput(DrivingWorldSnapshot snapshot, uint racerId)
        {
            foreach (DrivingRacerState racer in snapshot.Racers)
                if (racer.RacerId == racerId) return racer.AcknowledgedInput;
            return 0;
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
