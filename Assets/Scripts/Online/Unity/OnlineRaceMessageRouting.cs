using System;
using System.Collections.Generic;
using System.Text;
using Presentation;
using ReReVolt.Online.Contracts;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ReReVolt.Online.Unity
{
    /// <summary>
    /// NGO transport bridge for the already-authoritative driving contracts.
    /// The host owns an <see cref="OnlineRaceHostWorld"/> and accepts peer input;
    /// clients only validate and observe host snapshots. It deliberately does not
    /// create vehicles, cameras, local players, or mutate a client physics world.
    /// </summary>
    public sealed class OnlineRaceMessageRouting : IDisposable
    {
        private const string InputChannel = "rrv.race-input.v1";
        private const string SnapshotChannel = "rrv.race-snapshot.v1";
        private const int MaximumInputBytes = 2048;
        private const int MaximumSnapshotBytes = 8192;
        private const double SnapshotInterval = .05;

        private readonly NetworkManager network;
        private readonly OnlineRaceHostWorld hostWorld;
        private readonly OnlineDrivingSnapshotReceiver receiver;
        private readonly List<ulong> recipients = new List<ulong>(3);
        private bool disposed;
        private double nextSnapshotAt;

        /// <summary>Only raised on non-host peers after the contract accepts a host snapshot.</summary>
        public event Action<DrivingWorldSnapshot> SnapshotAccepted;
        /// <summary>Only raised on the host for the same authoritative snapshot sent to clients.</summary>
        public event Action<DrivingWorldSnapshot> HostSnapshotPublished;
        public InputError LastInputError { get; private set; }
        public SnapshotError LastSnapshotError { get; private set; }
        public bool IsHost => network.IsHost;
        public DrivingWorldSnapshot LatestSnapshot => receiver == null ? null : receiver.Latest;

        /// <param name="lockedRoster">The exact countdown/race roster used to initialize the host world.</param>
        /// <param name="hostWorld">Required only on the NGO host; clients must pass null.</param>
        public OnlineRaceMessageRouting(NetworkManager network, LobbySnapshot lockedRoster, OnlineRaceHostWorld hostWorld = null)
        {
            this.network = network != null ? network : throw new ArgumentNullException(nameof(network));
            if (!network.IsConnectedClient || !network.IsListening)
                throw new InvalidOperationException("A connected NGO transport is required.");
            if (lockedRoster == null || lockedRoster.Phase == OnlinePhase.Lobby)
                throw new ArgumentException("A locked countdown/race roster is required.", nameof(lockedRoster));
            if (!TrackCatalog.TryGet(lockedRoster.TrackId, out TrackDefinition track) || !track.IsAvailable)
                throw new ArgumentException("The selected track is unavailable.", nameof(lockedRoster));
            if (network.IsHost)
            {
                this.hostWorld = hostWorld ?? throw new ArgumentNullException(nameof(hostWorld));
                if (!hostWorld.IsInitialized)
                    throw new ArgumentException("The host world must be initialized before networking starts.", nameof(hostWorld));
            }
            else if (hostWorld != null)
            {
                throw new ArgumentException("Only the NGO host may own an online race world.", nameof(hostWorld));
            }
            receiver = network.IsHost ? null : new OnlineDrivingSnapshotReceiver(lockedRoster, track.LapCount, track.OrderedCheckpointCount);
            network.CustomMessagingManager.RegisterNamedMessageHandler(InputChannel, ReceiveInput);
            network.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotChannel, ReceiveSnapshot);
        }

        /// <summary>
        /// The input owner calls this at its bounded input cadence. A host submits
        /// locally without serializing; a client sends only to the NGO server.
        /// </summary>
        public InputError SubmitLocalInput(PeerInputFrame frame)
        {
            ThrowIfDisposed();
            if (frame == null) return LastInputError = InputError.InvalidFrame;
            if (network.IsHost)
                return LastInputError = hostWorld.SubmitInput(Peer(network.LocalClientId), frame);
            if (!OnlineRaceWireCodec.TryEncodeInput(frame, out byte[] bytes))
                return LastInputError = InputError.InvalidFrame;
            Send(InputChannel, NetworkManager.ServerClientId, bytes, NetworkDelivery.UnreliableSequenced);
            return LastInputError = InputError.None;
        }

        /// <summary>
        /// Invoke after host physics. This is intentionally explicit so the mode
        /// coordinator controls round lifetime and cadence, rather than Update.
        /// A client cannot call it to manufacture world state.
        /// </summary>
        public void PublishHostSnapshot(double hostTime)
        {
            ThrowIfDisposed();
            if (!network.IsHost || hostWorld == null || !hostWorld.IsInitialized) return;
            if (double.IsNaN(hostTime) || double.IsInfinity(hostTime) || hostTime < nextSnapshotAt) return;
            nextSnapshotAt = hostTime + SnapshotInterval;
            DrivingWorldSnapshot snapshot = hostWorld.CaptureSnapshot();
            if (!OnlineRaceWireCodec.TryEncodeSnapshot(snapshot, out byte[] bytes))
                throw new InvalidOperationException("Authoritative driving snapshot exceeded its wire contract.");
            HostSnapshotPublished?.Invoke(snapshot);
            recipients.Clear();
            foreach (ulong peer in network.ConnectedClientsIds)
                if (peer != network.LocalClientId) recipients.Add(peer);
            foreach (ulong peer in recipients)
                Send(SnapshotChannel, peer, bytes, NetworkDelivery.UnreliableSequenced);
        }

        private void ReceiveInput(ulong sender, FastBufferReader reader)
        {
            if (disposed || !network.IsHost || hostWorld == null || sender == network.LocalClientId) return;
            if (!OnlineRaceWireCodec.TryRead(reader, MaximumInputBytes, out byte[] bytes) ||
                !OnlineRaceWireCodec.TryDecodeInput(bytes, out PeerInputFrame frame))
            { LastInputError = InputError.InvalidFrame; return; }
            LastInputError = hostWorld.SubmitInput(Peer(sender), frame);
        }

        private void ReceiveSnapshot(ulong sender, FastBufferReader reader)
        {
            if (disposed || network.IsHost || sender != NetworkManager.ServerClientId || receiver == null) return;
            if (!OnlineRaceWireCodec.TryRead(reader, MaximumSnapshotBytes, out byte[] bytes) ||
                !OnlineRaceWireCodec.TryDecodeSnapshot(bytes, out DrivingWorldSnapshot snapshot))
            { LastSnapshotError = SnapshotError.InvalidState; return; }
            LastSnapshotError = receiver.Accept(snapshot);
            if (LastSnapshotError == SnapshotError.None)
                SnapshotAccepted?.Invoke(receiver.Latest);
        }

        private void Send(string channel, ulong recipient, byte[] bytes, NetworkDelivery delivery)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var writer = new FastBufferWriter(bytes.Length + sizeof(int), Allocator.Temp))
            {
                writer.WriteValueSafe(bytes.Length);
                foreach (byte value in bytes) writer.WriteValueSafe(value);
                network.CustomMessagingManager.SendNamedMessage(channel, recipient, writer, delivery);
            }
        }

        private static string Peer(ulong id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OnlineRaceMessageRouting));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            network.CustomMessagingManager?.UnregisterNamedMessageHandler(InputChannel);
            network.CustomMessagingManager?.UnregisterNamedMessageHandler(SnapshotChannel);
            SnapshotAccepted = null;
            HostSnapshotPublished = null;
        }
    }

    /// <summary>Small bounded codec isolated from NGO callbacks for focused validation.</summary>
    public static class OnlineRaceWireCodec
    {
        private const int Version = 2;
        private const int MaximumInputBytes = 2048;
        private const int MaximumSnapshotBytes = 8192;

        [Serializable] private sealed class InputEnvelope { public int Version; public PeerInputFrame Frame; }
        [Serializable] private sealed class SnapshotEnvelope { public int Version; public DrivingWorldSnapshot Snapshot; }

        public static bool TryEncodeInput(PeerInputFrame frame, out byte[] bytes) =>
            TryEncode(new InputEnvelope { Version = Version, Frame = frame }, MaximumInputBytes, out bytes);
        public static bool TryEncodeSnapshot(DrivingWorldSnapshot snapshot, out byte[] bytes) =>
            TryEncode(new SnapshotEnvelope { Version = Version, Snapshot = snapshot }, MaximumSnapshotBytes, out bytes);

        public static bool TryDecodeInput(byte[] bytes, out PeerInputFrame frame)
        {
            frame = null;
            if (!TryDecode(bytes, MaximumInputBytes, out InputEnvelope message) || message.Version != Version || message.Frame == null)
                return false;
            frame = message.Frame;
            return true;
        }

        public static bool TryDecodeSnapshot(byte[] bytes, out DrivingWorldSnapshot snapshot)
        {
            snapshot = null;
            if (!TryDecode(bytes, MaximumSnapshotBytes, out SnapshotEnvelope message) || message.Version != Version || message.Snapshot == null)
                return false;
            snapshot = message.Snapshot;
            return true;
        }

        public static bool TryRead(FastBufferReader reader, int maximumBytes, out byte[] bytes)
        {
            bytes = null;
            if (reader.Length < sizeof(int) || reader.Length > maximumBytes + sizeof(int)) return false;
            try
            {
                reader.ReadValueSafe(out int size);
                if (size < 2 || size > maximumBytes || reader.Length - reader.Position != size) return false;
                bytes = new byte[size];
                for (int i = 0; i < size; i++) reader.ReadValueSafe(out bytes[i]);
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            {
                bytes = null;
                return false;
            }
        }

        private static bool TryEncode<T>(T value, int maximumBytes, out byte[] bytes)
        {
            bytes = null;
            if (ReferenceEquals(value, null)) return false;
            try
            {
                bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(value));
                if (bytes.Length < 2 || bytes.Length > maximumBytes) { bytes = null; return false; }
                return true;
            }
            catch (ArgumentException) { bytes = null; return false; }
        }

        private static bool TryDecode<T>(byte[] bytes, int maximumBytes, out T value) where T : class
        {
            value = null;
            if (bytes == null || bytes.Length < 2 || bytes.Length > maximumBytes) return false;
            try { value = JsonUtility.FromJson<T>(Encoding.UTF8.GetString(bytes)); return value != null; }
            catch (ArgumentException) { return false; }
        }
    }
}
