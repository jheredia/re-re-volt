using System;
using System.Collections.Generic;
using Core;

namespace ReReVolt.Online.Contracts
{
    public enum OnlinePhase { Lobby, Countdown, Racing, Results }
    public enum LobbyAction { AddHuman, RemoveHuman, SetReady, SelectTrack, SetBots, Start, Rematch }
    public enum OnlineError
    {
        None, WrongEpoch, StaleRevision, UnknownPeer, NotHost, NotOwner, Capacity,
        InvalidCommand, Locked, NotReady, IncompatibleBuild, SimulationUnavailable
    }

    [Serializable]
    public sealed class LobbyCommand
    {
        public ulong Epoch;
        public ulong Revision;
        public LobbyAction Action;
        public uint RacerId;
        public byte Slot;
        public bool Ready;
        public int BotCount;
        public string TrackId;
    }

    [Serializable]
    public sealed class LobbyRacer
    {
        public uint Id;
        public string Peer;
        public int Slot;
        public bool Bot;
        public bool Ready;
    }

    [Serializable]
    public sealed class LobbySnapshot
    {
        public ulong Epoch;
        public ulong Revision;
        public uint Round;
        public string HostPeer;
        public string TrackId;
        public OnlinePhase Phase;
        public string[] Peers;
        public LobbyRacer[] Racers;
    }

    /// <summary>Only the host holds this mutable state. Actor IDs come from the transport envelope.</summary>
    public sealed class OnlineLobbyAuthority
    {
        private readonly OnlineRacerRoster roster;
        private readonly string host;
        private readonly HashSet<string> tracks;
        private readonly List<string> peers = new List<string>(4);
        private readonly HashSet<uint> ready = new HashSet<uint>();
        private string track;
        public ulong Epoch => roster.Epoch;
        public ulong Revision { get; private set; }
        public uint Round { get; private set; }
        public OnlinePhase Phase { get; private set; }

        public OnlineLobbyAuthority(string hostPeer, ulong epoch, IEnumerable<string> trackIds, string initialTrack)
        {
            tracks = new HashSet<string>(trackIds ?? throw new ArgumentNullException(nameof(trackIds)), StringComparer.Ordinal);
            if (!tracks.Contains(initialTrack)) throw new ArgumentException("Initial track must be in the catalog.");
            roster = new OnlineRacerRoster(hostPeer, epoch);
            host = hostPeer;
            track = initialTrack;
            peers.Add(host);
            roster.AddHuman(host, 0, Epoch, roster.Revision, out _);
        }

        /// <summary>Admission includes one human; an admitted peer can never be an accidental spectator.</summary>
        public OnlineError AdmitPeer(string authenticatedPeer)
        {
            if (string.IsNullOrWhiteSpace(authenticatedPeer)) return OnlineError.UnknownPeer;
            if (peers.Contains(authenticatedPeer)) return OnlineError.None;
            if (Phase != OnlinePhase.Lobby) return OnlineError.Locked;
            if (peers.Count >= 4 || roster.Count >= 4) return OnlineError.Capacity;
            var error = roster.AddPeer(host, authenticatedPeer, Epoch, roster.Revision);
            if (error != OnlineRosterError.None) return Map(error);
            error = roster.AddHuman(authenticatedPeer, 0, Epoch, roster.Revision, out _);
            if (error != OnlineRosterError.None)
            {
                roster.RemovePeer(host, authenticatedPeer, Epoch, roster.Revision);
                return Map(error);
            }
            peers.Add(authenticatedPeer);
            Changed();
            return OnlineError.None;
        }

        /// <summary>Peer loss aborts an active race to the retained lobby. Host loss is handled by the client.</summary>
        public bool RemovePeer(string authenticatedPeer)
        {
            if (authenticatedPeer == host || !peers.Contains(authenticatedPeer)) return false;
            roster.SetLocked(host, false, Epoch, roster.Revision);
            roster.RemovePeer(host, authenticatedPeer, Epoch, roster.Revision);
            peers.Remove(authenticatedPeer);
            Phase = OnlinePhase.Lobby;
            Changed();
            return true;
        }

        public OnlineError Apply(string authenticatedPeer, LobbyCommand command, bool simulationAvailable)
        {
            if (command == null) return OnlineError.InvalidCommand;
            if (command.Epoch != Epoch) return OnlineError.WrongEpoch;
            if (!peers.Contains(authenticatedPeer ?? string.Empty)) return OnlineError.UnknownPeer;
            if (command.Revision != Revision) return OnlineError.StaleRevision;
            if (command.Action == LobbyAction.Rematch)
            {
                if (authenticatedPeer != host) return OnlineError.NotHost;
                if (Phase != OnlinePhase.Results) return OnlineError.Locked;
                Phase = OnlinePhase.Lobby;
                roster.SetLocked(host, false, Epoch, roster.Revision);
                Changed();
                return OnlineError.None;
            }
            if (Phase != OnlinePhase.Lobby) return OnlineError.Locked;
            OnlineRosterError rosterError;
            switch (command.Action)
            {
                case LobbyAction.AddHuman:
                    rosterError = roster.AddHuman(authenticatedPeer, command.Slot, Epoch, roster.Revision, out _);
                    if (rosterError != OnlineRosterError.None) return Map(rosterError);
                    Changed();
                    return OnlineError.None;
                case LobbyAction.RemoveHuman:
                    if (!roster.OwnsHuman(authenticatedPeer, command.RacerId)) return OnlineError.NotOwner;
                    int owned = 0;
                    foreach (var racer in roster.Snapshot())
                        if (!racer.IsBot && racer.PeerId == authenticatedPeer) owned++;
                    if (owned <= 1) return OnlineError.InvalidCommand; // Leave the peer session to remove its final human.
                    rosterError = roster.RemoveHuman(authenticatedPeer, command.RacerId, Epoch, roster.Revision);
                    if (rosterError != OnlineRosterError.None) return Map(rosterError);
                    Changed();
                    return OnlineError.None;
                case LobbyAction.SetReady:
                    if (!roster.OwnsHuman(authenticatedPeer, command.RacerId)) return OnlineError.NotOwner;
                    if (command.Ready) ready.Add(command.RacerId); else ready.Remove(command.RacerId);
                    Revision++;
                    return OnlineError.None;
                case LobbyAction.SelectTrack:
                    if (authenticatedPeer != host) return OnlineError.NotHost;
                    if (!tracks.Contains(command.TrackId ?? string.Empty)) return OnlineError.InvalidCommand;
                    if (track == command.TrackId) return OnlineError.None;
                    track = command.TrackId;
                    Changed();
                    return OnlineError.None;
                case LobbyAction.SetBots:
                    if (authenticatedPeer != host) return OnlineError.NotHost;
                    ulong previous = roster.Revision;
                    rosterError = roster.SetBotCount(host, command.BotCount, Epoch, roster.Revision);
                    if (rosterError != OnlineRosterError.None) return Map(rosterError);
                    if (previous != roster.Revision) Changed();
                    return OnlineError.None;
                case LobbyAction.Start:
                    if (authenticatedPeer != host) return OnlineError.NotHost;
                    if (!simulationAvailable) return OnlineError.SimulationUnavailable;
                    if (!AllReady()) return OnlineError.NotReady;
                    if (Round == uint.MaxValue) return OnlineError.InvalidCommand;
                    roster.SetLocked(host, true, Epoch, roster.Revision);
                    Round++;
                    Phase = OnlinePhase.Countdown;
                    Revision++;
                    return OnlineError.None;
                default: return OnlineError.InvalidCommand;
            }
        }

        // These are deliberately host-runtime methods, never deserialized client actions.
        public bool BeginRace()
        {
            if (Phase != OnlinePhase.Countdown) return false;
            Phase = OnlinePhase.Racing;
            Revision++;
            return true;
        }

        public bool CompleteRace()
        {
            if (Phase != OnlinePhase.Racing) return false;
            Phase = OnlinePhase.Results;
            Revision++;
            return true;
        }

        public bool OwnsHuman(string peer, uint racer) => roster.OwnsHuman(peer, racer);

        public LobbySnapshot Snapshot()
        {
            var racers = roster.Snapshot();
            var result = new LobbyRacer[racers.Length];
            for (int i = 0; i < racers.Length; i++)
                result[i] = new LobbyRacer { Id = racers[i].Id, Peer = racers[i].PeerId,
                    Slot = racers[i].LocalSlot.HasValue ? racers[i].LocalSlot.Value : -1,
                    Bot = racers[i].IsBot, Ready = racers[i].IsBot || ready.Contains(racers[i].Id) };
            return new LobbySnapshot { Epoch = Epoch, Revision = Revision, Round = Round,
                HostPeer = host, TrackId = track, Phase = Phase, Peers = peers.ToArray(), Racers = result };
        }

        private bool AllReady()
        {
            bool human = false;
            foreach (var racer in roster.Snapshot())
            {
                if (racer.IsBot) continue;
                human = true;
                if (!ready.Contains(racer.Id)) return false;
            }
            return human;
        }

        private void Changed() { ready.Clear(); Revision++; }
        private static OnlineError Map(OnlineRosterError error)
        {
            switch (error)
            {
                case OnlineRosterError.None: return OnlineError.None;
                case OnlineRosterError.PeerCapacity:
                case OnlineRosterError.RacerCapacity: return OnlineError.Capacity;
                case OnlineRosterError.NotHost: return OnlineError.NotHost;
                case OnlineRosterError.NotOwner: return OnlineError.NotOwner;
                default: return OnlineError.InvalidCommand;
            }
        }
    }
}
