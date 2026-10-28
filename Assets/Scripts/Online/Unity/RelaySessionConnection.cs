using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace ReReVolt.Online.Unity
{
    /// <summary>
    /// Explicitly owned, opt-in transport session. This does not replicate a
    /// race: a peer is not a racer and gameplay authority remains offline-only.
    /// Create exactly one instance for the lifetime of an online UI flow.
    /// </summary>
    public sealed class RelaySessionConnection
    {
        // SessionJoined includes a connected local NGO transport, but does not prove
        // peer compatibility, authoritative lobby admission, or playable racing.
        public enum ConnectionState { Idle, Connecting, SessionJoined, Disconnecting }

        private ISession session;
        private GameObject networkObject;

        public ConnectionState State { get; private set; }
        public NetworkManager Network => networkObject == null ? null : networkObject.GetComponent<NetworkManager>();
        public string JoinCode => session == null ? null : session.Code;
        public bool IsHost => session != null && session.IsHost;

        public async Task HostAsync()
        {
            BeginConnection();
            try
            {
                await InitializeAsync();
                CreateNetworkManager();
                var options = new SessionOptions { MaxPlayers = 4, IsPrivate = true }
                    .WithRelayNetwork();
                session = await MultiplayerService.Instance.CreateSessionAsync(options);
                await WaitForTransportAsync();
                State = ConnectionState.SessionJoined;
            }
            catch
            {
                await CleanupFailedConnectionAsync();
                throw;
            }
        }

        public async Task JoinAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
                throw new ArgumentException("Enter the host's join code.", nameof(joinCode));
            BeginConnection();
            try
            {
                await InitializeAsync();
                CreateNetworkManager();
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim());
                await WaitForTransportAsync();
                State = ConnectionState.SessionJoined;
            }
            catch
            {
                await CleanupFailedConnectionAsync();
                throw;
            }
        }

        public async Task LeaveAsync()
        {
            if (State == ConnectionState.Idle) return;
            if (State != ConnectionState.SessionJoined)
                throw new InvalidOperationException("A session operation is already in progress.");
            State = ConnectionState.Disconnecting;
            try
            {
                await session.LeaveAsync();
            }
            finally
            {
                session = null;
                DestroyNetworkManager();
                State = ConnectionState.Idle;
            }
        }

        private async Task WaitForTransportAsync()
        {
            var timer = Stopwatch.StartNew();
            while (Network != null && !Network.IsConnectedClient)
            {
                if (timer.Elapsed.TotalSeconds >= 15)
                    throw new TimeoutException("Relay transport did not connect.");
                await Task.Delay(50);
            }
            if (Network == null) throw new InvalidOperationException("Relay transport was destroyed.");
        }

        private async Task CleanupFailedConnectionAsync()
        {
            try { if (session != null) await session.LeaveAsync(); }
            catch (Exception) { /* Preserve the connection failure; local shutdown is unconditional. */ }
            finally
            {
                session = null;
                DestroyNetworkManager();
                State = ConnectionState.Idle;
            }
        }

        private void BeginConnection()
        {
            if (State != ConnectionState.Idle)
                throw new InvalidOperationException("A session is already active or connecting.");
            State = ConnectionState.Connecting;
        }

        private static async Task InitializeAsync()
        {
            if (string.IsNullOrWhiteSpace(Application.cloudProjectId))
                throw new InvalidOperationException("The owner must link this project to Unity Services first.");
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private void CreateNetworkManager()
        {
            if (NetworkManager.Singleton != null)
                throw new InvalidOperationException("Another network manager is already active.");
            networkObject = new GameObject("Online Relay Network");
            UnityEngine.Object.DontDestroyOnLoad(networkObject);
            var transport = networkObject.AddComponent<UnityTransport>();
            var manager = networkObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false
            };
        }

        private void DestroyNetworkManager()
        {
            if (networkObject == null) return;
            var manager = networkObject.GetComponent<NetworkManager>();
            if (manager != null && manager.IsListening) manager.Shutdown();
            UnityEngine.Object.Destroy(networkObject);
            networkObject = null;
        }
    }
}
