using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Presentation;
using ReReVolt.Online.Contracts;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ReReVolt.Online.Unity
{
    /// <summary>NGO envelope adapter. Remote actor identity is always the callback sender, never JSON.</summary>
    public sealed class OnlineLobbyProtocol : IDisposable
    {
        private const string Channel = "rrv.lobby.v1";
        private const int ProtocolVersion = 1;
        private const int MaximumBytes = 8192;
        [Serializable]
        private sealed class Envelope
        {
            public int Version;
            public string Build;
            public bool Hello;
            public LobbyCommand Command;
            public LobbySnapshot Snapshot;
            public OnlineError Error;
        }
        private sealed class Budget { public double Start; public int Count; }
        private readonly NetworkManager network;
        private readonly string build;
        private readonly OnlineLobbyAuthority authority;
        private readonly HashSet<ulong> admitted = new HashSet<ulong>();
        private readonly Dictionary<ulong, double> awaiting = new Dictionary<ulong, double>();
        private readonly Dictionary<ulong, Budget> budgets = new Dictionary<ulong, Budget>();
        private readonly Dictionary<ulong, double> rejected = new Dictionary<ulong, double>();
        private readonly List<ulong> scratch = new List<ulong>(4);
        private readonly double createdAt;
        private double nextHello;
        private double requestStarted;
        private bool disposed;
        public LobbySnapshot Snapshot { get; private set; }
        public string LocalPeer => Peer(network.LocalClientId);
        public bool IsHost => authority != null;
        public OnlineError LastError { get; private set; }
        public string TerminalMessage { get; private set; }
        public bool RequestPending { get; private set; }

        public OnlineLobbyProtocol(NetworkManager network, double now)
        {
            this.network = network != null ? network : throw new ArgumentNullException(nameof(network));
            if (!network.IsConnectedClient) throw new InvalidOperationException("Transport must connect before lobby negotiation.");
            createdAt = now;
            build = CompatibilityFingerprint();
            if (network.IsHost)
            {
                var ids = new List<string>(4);
                foreach (var entry in TrackCatalog.Entries) if (entry.IsAvailable) ids.Add(entry.Id);
                byte[] epochBytes = Guid.NewGuid().ToByteArray();
                ulong epoch = BitConverter.ToUInt64(epochBytes, 0);
                authority = new OnlineLobbyAuthority(LocalPeer, epoch == 0 ? 1UL : epoch, ids, TrackCatalog.Default.Id);
                admitted.Add(network.LocalClientId);
                Snapshot = authority.Snapshot();
            }
            network.CustomMessagingManager.RegisterNamedMessageHandler(Channel, Receive);
            network.OnClientDisconnectCallback += OnDisconnected;
        }

        public void Tick(double now)
        {
            if (disposed || TerminalMessage != null) return;
            if (!network.IsConnectedClient)
            { TerminalMessage = "Transport disconnected. Host or join a new session."; return; }
            if (!IsHost)
            {
                if (RequestPending && now - requestStarted >= 5)
                { TerminalMessage = "Host did not answer the lobby command. Leave and retry."; return; }
                if (Snapshot != null) return;
                if (now - createdAt >= 15)
                { TerminalMessage = "Host lobby negotiation timed out. Retry with a compatible build."; return; }
                if (now < nextHello) return;
                nextHello = now + 1;
                Send(NetworkManager.ServerClientId, new Envelope { Hello = true });
                return;
            }
            foreach (ulong id in network.ConnectedClientsIds)
                if (!admitted.Contains(id) && !awaiting.ContainsKey(id) && !rejected.ContainsKey(id)) awaiting.Add(id, now);
            scratch.Clear();
            foreach (var item in awaiting) if (now - item.Value >= 15) scratch.Add(item.Key);
            foreach (ulong id in scratch) Reject(id, OnlineError.IncompatibleBuild, now);
            scratch.Clear();
            foreach (var item in rejected) if (now >= item.Value) scratch.Add(item.Key);
            foreach (ulong id in scratch)
            {
                rejected.Remove(id);
                network.DisconnectClient(id);
            }
        }

        public void Submit(LobbyCommand command)
        {
            if (disposed || Snapshot == null || TerminalMessage != null || RequestPending || command == null) return;
            command.Epoch = Snapshot.Epoch;
            command.Revision = Snapshot.Revision;
            LastError = OnlineError.None;
            if (IsHost) Apply(network.LocalClientId, command);
            else { RequestPending = true; requestStarted = Time.realtimeSinceStartupAsDouble; Send(NetworkManager.ServerClientId, new Envelope { Command = command }); }
        }

        private void Receive(ulong sender, FastBufferReader reader)
        {
            if (disposed || TerminalMessage != null) return;
            // No unbounded string/array deserialization from the wire.
            if (reader.Length < sizeof(int) || reader.Length > MaximumBytes + sizeof(int)) return;
            if (IsHost && (!network.ConnectedClients.ContainsKey(sender) || rejected.ContainsKey(sender))) return;
            if (!IsHost && sender != NetworkManager.ServerClientId) return;
            if (IsHost && !WithinBudget(sender, Time.realtimeSinceStartupAsDouble)) return;
            Envelope message;
            try
            {
                reader.ReadValueSafe(out int size);
                if (size < 2 || size > MaximumBytes || reader.Length - reader.Position != size) return;
                var bytes = new byte[size];
                for (int i = 0; i < size; i++) reader.ReadValueSafe(out bytes[i]);
                message = JsonUtility.FromJson<Envelope>(Encoding.UTF8.GetString(bytes));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            { return; }
            if (message == null) return;
            if (message.Version != ProtocolVersion || message.Build != build)
            {
                if (IsHost) Reject(sender, OnlineError.IncompatibleBuild, Time.realtimeSinceStartupAsDouble);
                else TerminalMessage = "Incompatible online build or track catalog. Install the same build on both peers.";
                return;
            }
            if (IsHost)
            {
                if (message.Hello)
                {
                    var error = authority.AdmitPeer(Peer(sender));
                    if (error != OnlineError.None) { Reject(sender, error, Time.realtimeSinceStartupAsDouble); return; }
                    admitted.Add(sender);
                    awaiting.Remove(sender);
                    Broadcast();
                }
                else if (admitted.Contains(sender) && message.Command != null) Apply(sender, message.Command);
                return;
            }
            RequestPending = false;
            LastError = message.Error;
            if (message.Error == OnlineError.Capacity || message.Error == OnlineError.Locked ||
                message.Error == OnlineError.IncompatibleBuild)
            {
                // Capacity/locked replies with a snapshot are ordinary rejected lobby edits.
                if (message.Snapshot == null)
                    TerminalMessage = Describe(message.Error);
            }
            if (!ValidSnapshot(message.Snapshot) || Array.IndexOf(message.Snapshot.Peers, LocalPeer) < 0) return;
            if (Snapshot != null && (message.Snapshot.Epoch != Snapshot.Epoch || message.Snapshot.Revision < Snapshot.Revision)) return;
            Snapshot = message.Snapshot;
        }

        private void Apply(ulong sender, LobbyCommand command)
        {
            // The world adapter is not integrated yet. Keep Start explicitly disabled at this boundary.
            OnlineError error = authority.Apply(Peer(sender), command, simulationAvailable: false);
            if (sender == network.LocalClientId) LastError = error;
            if (error == OnlineError.None) Broadcast();
            else if (sender != network.LocalClientId)
                Send(sender, new Envelope { Error = error, Snapshot = authority.Snapshot() });
        }

        private void Broadcast()
        {
            Snapshot = authority.Snapshot();
            foreach (ulong peer in admitted)
                if (peer != network.LocalClientId) Send(peer, new Envelope { Snapshot = Snapshot });
        }

        private void Reject(ulong peer, OnlineError error, double now)
        {
            awaiting.Remove(peer);
            rejected[peer] = now + .5;
            Send(peer, new Envelope { Error = error });
        }

        private bool WithinBudget(ulong peer, double now)
        {
            if (!budgets.TryGetValue(peer, out Budget budget))
            { budget = new Budget { Start = now }; budgets.Add(peer, budget); }
            if (now - budget.Start >= 1) { budget.Start = now; budget.Count = 0; }
            return ++budget.Count <= 16;
        }

        private void Send(ulong recipient, Envelope message)
        {
            if (disposed || !network.IsListening) return;
            message.Version = ProtocolVersion;
            message.Build = build;
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
            if (bytes.Length > MaximumBytes) throw new InvalidOperationException("Lobby message exceeds protocol bound.");
            using (var writer = new FastBufferWriter(bytes.Length + sizeof(int), Allocator.Temp))
            {
                writer.WriteValueSafe(bytes.Length);
                foreach (byte value in bytes) writer.WriteValueSafe(value);
                network.CustomMessagingManager.SendNamedMessage(Channel, recipient, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private void OnDisconnected(ulong peer)
        {
            if (!IsHost)
            { TerminalMessage = "Host connection lost. Host migration is unavailable. Create or join a new session."; return; }
            awaiting.Remove(peer);
            rejected.Remove(peer);
            budgets.Remove(peer);
            admitted.Remove(peer);
            if (authority.RemovePeer(Peer(peer))) Broadcast();
        }

        private static bool ValidSnapshot(LobbySnapshot snapshot)
        {
            if (snapshot == null || snapshot.Epoch == 0 || snapshot.Racers == null || snapshot.Racers.Length > 4 ||
                snapshot.Peers == null || snapshot.Peers.Length < 1 || snapshot.Peers.Length > 4 ||
                !TrackCatalog.IsAvailable(snapshot.TrackId) || !Enum.IsDefined(typeof(OnlinePhase), snapshot.Phase)) return false;
            var peers = new HashSet<string>(snapshot.Peers, StringComparer.Ordinal);
            if (peers.Count != snapshot.Peers.Length || !peers.Contains(snapshot.HostPeer)) return false;
            var ids = new HashSet<uint>();
            var slots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var racer in snapshot.Racers)
            {
                if (racer == null || racer.Id == 0 || !ids.Add(racer.Id) || !peers.Contains(racer.Peer)) return false;
                if (racer.Bot)
                { if (racer.Slot != -1 || racer.Peer != snapshot.HostPeer) return false; }
                else if (racer.Slot < 0 || racer.Slot > 3 || !slots.Add(racer.Peer + ":" + racer.Slot)) return false;
            }
            return true;
        }

        private static string CompatibilityFingerprint()
        {
            // Bump protocol/content revision whenever wire semantics or online gameplay compatibility changes.
            var text = new StringBuilder("rrv-stage10-lobby-1|").Append(Application.version);
            foreach (var entry in TrackCatalog.Entries)
                text.Append('|').Append(entry.Id).Append(':').Append(entry.LapCount).Append(':')
                    .Append(entry.OrderedCheckpointCount).Append(':').Append(entry.IsAvailable);
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }

        public static string Describe(OnlineError error)
        {
            switch (error)
            {
                case OnlineError.None: return string.Empty;
                case OnlineError.Capacity: return "Four racer slots are occupied. Host: reduce bots or remove a local racer before inviting another peer.";
                case OnlineError.StaleRevision: return "Lobby changed. Review the refreshed roster and retry.";
                case OnlineError.NotHost: return "Only the host can change track, bots or start the race.";
                case OnlineError.NotOwner: return "This racer belongs to another peer.";
                case OnlineError.NotReady: return "Every human racer must be ready.";
                case OnlineError.Locked: return "Race already started. Wait for the host to return to the lobby.";
                case OnlineError.IncompatibleBuild: return "Both peers must use the same online build and track catalog.";
                case OnlineError.SimulationUnavailable: return "Online simulation is not integrated yet. Race start is unavailable.";
                default: return "Lobby command rejected. Review the roster and retry.";
            }
        }

        private static string Peer(ulong id) => id.ToString(CultureInfo.InvariantCulture);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (network == null) return;
            network.OnClientDisconnectCallback -= OnDisconnected;
            network.CustomMessagingManager?.UnregisterNamedMessageHandler(Channel);
        }
    }
}
