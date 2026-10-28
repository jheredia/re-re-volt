using System;
using System.Collections.Generic;

namespace ReReVolt.Online.Contracts
{
    [Flags]
    public enum InputEdges : byte { None = 0, Fire = 1, Recover = 2, Pause = 4 }
    public enum InputError { None, WrongEpoch, WrongRound, IllegalPhase, InvalidFrame, NotOwner, StaleSequence, RateLimited }

    [Serializable]
    public struct RacerInputCommand
    {
        public uint RacerId;
        public ulong Sequence;
        public short Throttle;
        public short Steer;
        public short Brake;
        public InputEdges Edges;
    }

    [Serializable]
    public sealed class PeerInputFrame
    {
        public ulong Epoch;
        public uint Round;
        public ulong Sequence;
        public RacerInputCommand[] Racers;
    }

    /// <summary>One per race round. Commands are atomic, owner-checked and expire on the host clock.</summary>
    public sealed class OnlineInputAuthority
    {
        public const double InputLifetime = .250;
        private sealed class PeerState
        {
            public ulong Sequence;
            public double Window;
            public int Count;
        }
        private sealed class RacerInput
        {
            public RacerInputCommand Command;
            public InputEdges Pending;
            public double At;
        }
        private readonly OnlineLobbyAuthority lobby;
        private readonly uint round;
        private readonly Dictionary<string, PeerState> peers = new Dictionary<string, PeerState>(StringComparer.Ordinal);
        private readonly Dictionary<uint, RacerInput> inputs = new Dictionary<uint, RacerInput>();
        private double lastTime;

        public OnlineInputAuthority(OnlineLobbyAuthority lobby)
        {
            this.lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            round = lobby.Round;
        }

        public InputError Accept(string transportPeer, PeerInputFrame frame, double hostTime)
        {
            if (frame == null || !ValidTime(hostTime) || hostTime < lastTime) return InputError.InvalidFrame;
            if (frame.Epoch != lobby.Epoch) return InputError.WrongEpoch;
            if (frame.Round != round || lobby.Round != round) return InputError.WrongRound;
            if (lobby.Phase != OnlinePhase.Racing) return InputError.IllegalPhase;
            if (string.IsNullOrEmpty(transportPeer) || frame.Racers == null || frame.Racers.Length < 1 ||
                frame.Racers.Length > 4 || frame.Sequence == 0) return InputError.InvalidFrame;
            peers.TryGetValue(transportPeer, out PeerState peer);
            if (peer != null && frame.Sequence <= peer.Sequence) return InputError.StaleSequence;
            if (peer != null && hostTime - peer.Window < 1 && peer.Count >= 120) return InputError.RateLimited;
            for (int i = 0; i < frame.Racers.Length; i++)
            {
                var command = frame.Racers[i];
                if (!lobby.OwnsHuman(transportPeer, command.RacerId)) return InputError.NotOwner;
                if (command.Throttle < -1000 || command.Throttle > 1000 || command.Steer < -1000 ||
                    command.Steer > 1000 || command.Brake < 0 || command.Brake > 1000 || command.Sequence == 0 ||
                    (command.Edges & ~(InputEdges.Fire | InputEdges.Recover | InputEdges.Pause)) != 0)
                    return InputError.InvalidFrame;
                if (inputs.TryGetValue(command.RacerId, out var previous) && command.Sequence <= previous.Command.Sequence)
                    return InputError.StaleSequence;
                for (int j = 0; j < i; j++)
                    if (frame.Racers[j].RacerId == command.RacerId) return InputError.InvalidFrame;
            }
            if (peer == null) { peer = new PeerState { Window = hostTime }; peers.Add(transportPeer, peer); }
            if (hostTime - peer.Window >= 1) { peer.Window = hostTime; peer.Count = 0; }
            peer.Sequence = frame.Sequence;
            peer.Count++;
            lastTime = hostTime;
            foreach (var command in frame.Racers)
            {
                if (!inputs.TryGetValue(command.RacerId, out var input))
                { input = new RacerInput(); inputs.Add(command.RacerId, input); }
                // Preserve an edge received between physics ticks, then consume it only once.
                input.Pending |= command.Edges;
                input.Command = command;
                input.At = hostTime;
            }
            return InputError.None;
        }

        public RacerInputCommand Consume(uint racerId, double hostTime)
        {
            if (!inputs.TryGetValue(racerId, out var input)) return new RacerInputCommand { RacerId = racerId };
            var command = input.Command;
            command.Edges = input.Pending;
            input.Pending = InputEdges.None;
            if (!ValidTime(hostTime) || hostTime < input.At || hostTime - input.At >= InputLifetime ||
                lobby.Round != round || lobby.Phase != OnlinePhase.Racing)
            { command.Throttle = command.Steer = command.Brake = 0; command.Edges = InputEdges.None; }
            return command;
        }

        private static bool ValidTime(double time) => !double.IsNaN(time) && !double.IsInfinity(time) && time >= 0;
    }
}
