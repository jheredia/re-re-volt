using System;
using System.Collections.Generic;

namespace Core
{
    public enum OnlineRosterError
    {
        None, NotHost, UnknownPeer, PeerCapacity, RacerCapacity, InvalidSlot,
        SlotOccupied, UnknownRacer, NotOwner, StaleRevision, WrongEpoch, Locked,
        InvalidBotCount, IdExhausted
    }

    /// <summary>A host-assigned identity for a human input slot or host-simulated bot.</summary>
    public readonly struct OnlineRacer
    {
        internal OnlineRacer(uint id, string peerId, byte? localSlot)
        {
            Id = id;
            PeerId = peerId;
            LocalSlot = localSlot;
        }

        public uint Id { get; }
        public string PeerId { get; }
        public byte? LocalSlot { get; }
        public bool IsBot => !LocalSlot.HasValue;
    }

    /// <summary>
    /// Host-side lobby authority. The caller must supply the authenticated transport
    /// peer, never an identity claimed inside a client message. Not thread-safe.
    /// </summary>
    public sealed class OnlineRacerRoster
    {
        public const int Capacity = 4;
        private readonly string hostPeer;
        private readonly HashSet<string> peers = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<OnlineRacer> racers = new List<OnlineRacer>(Capacity);
        private uint nextId = 1;
        private bool idExhausted;

        public OnlineRacerRoster(string hostPeer, ulong epoch)
        {
            if (string.IsNullOrWhiteSpace(hostPeer)) throw new ArgumentException("A host peer is required.", nameof(hostPeer));
            if (epoch == 0) throw new ArgumentOutOfRangeException(nameof(epoch));
            this.hostPeer = hostPeer;
            Epoch = epoch;
            peers.Add(hostPeer);
        }

        public ulong Epoch { get; }
        public ulong Revision { get; private set; }
        public bool Locked { get; private set; }
        public int Count => racers.Count;
        // A snapshot cannot mutate or change with the authoritative roster.
        public OnlineRacer[] Snapshot() => racers.ToArray();

        public OnlineRosterError AddPeer(string actor, string peer, ulong epoch, ulong revision)
        {
            var error = ValidateHost(actor, epoch, revision);
            if (error != OnlineRosterError.None) return error;
            if (string.IsNullOrWhiteSpace(peer)) return OnlineRosterError.UnknownPeer;
            if (peers.Contains(peer)) return OnlineRosterError.None;
            if (peers.Count == Capacity) return OnlineRosterError.PeerCapacity;
            peers.Add(peer);
            Revision++;
            return OnlineRosterError.None;
        }

        public OnlineRosterError AddHuman(string actor, byte localSlot, ulong epoch, ulong revision, out uint racerId)
        {
            racerId = 0;
            var error = Validate(actor, epoch, revision);
            if (error != OnlineRosterError.None) return error;
            if (localSlot >= Capacity) return OnlineRosterError.InvalidSlot;
            foreach (var racer in racers)
                if (!racer.IsBot && racer.PeerId == actor && racer.LocalSlot == localSlot)
                    return OnlineRosterError.SlotOccupied;
            if (racers.Count == Capacity) return OnlineRosterError.RacerCapacity;
            if (idExhausted) return OnlineRosterError.IdExhausted;
            racerId = AllocateId();
            racers.Add(new OnlineRacer(racerId, actor, localSlot));
            Revision++;
            return OnlineRosterError.None;
        }

        public OnlineRosterError RemoveHuman(string actor, uint racerId, ulong epoch, ulong revision)
        {
            var error = Validate(actor, epoch, revision);
            if (error != OnlineRosterError.None) return error;
            int index = racers.FindIndex(r => r.Id == racerId);
            if (index < 0) return OnlineRosterError.UnknownRacer;
            var racer = racers[index];
            if (racer.IsBot || racer.PeerId != actor) return OnlineRosterError.NotOwner;
            racers.RemoveAt(index);
            Revision++;
            return OnlineRosterError.None;
        }

        public OnlineRosterError SetBotCount(string actor, int count, ulong epoch, ulong revision)
        {
            var error = ValidateHost(actor, epoch, revision);
            if (error != OnlineRosterError.None) return error;
            if (count < 0 || count > 3) return OnlineRosterError.InvalidBotCount;
            int current = 0;
            foreach (var racer in racers) if (racer.IsBot) current++;
            if (racers.Count - current + count > Capacity) return OnlineRosterError.RacerCapacity;
            int added = Math.Max(0, count - current);
            if (added > 0 && (idExhausted || (ulong)nextId + (uint)added - 1 > uint.MaxValue))
                return OnlineRosterError.IdExhausted;
            if (count == current) return OnlineRosterError.None;
            for (int i = racers.Count - 1; i >= 0 && current > count; i--)
                if (racers[i].IsBot) { racers.RemoveAt(i); current--; }
            while (current < count)
            {
                racers.Add(new OnlineRacer(AllocateId(), hostPeer, null));
                current++;
            }
            Revision++;
            return OnlineRosterError.None;
        }

        public OnlineRosterError RemovePeer(string actor, string peer, ulong epoch, ulong revision)
        {
            var error = ValidateHost(actor, epoch, revision);
            if (error != OnlineRosterError.None) return error;
            if (peer == hostPeer || peer == null || !peers.Contains(peer)) return OnlineRosterError.UnknownPeer;
            peers.Remove(peer);
            racers.RemoveAll(r => !r.IsBot && r.PeerId == peer);
            Revision++;
            return OnlineRosterError.None;
        }

        public bool OwnsHuman(string peer, uint racerId)
        {
            if (peer == null || !peers.Contains(peer)) return false;
            foreach (var racer in racers)
                if (racer.Id == racerId) return !racer.IsBot && racer.PeerId == peer;
            return false;
        }

        public OnlineRosterError SetLocked(string actor, bool locked, ulong epoch, ulong revision)
        {
            if (epoch != Epoch) return OnlineRosterError.WrongEpoch;
            if (revision != Revision) return OnlineRosterError.StaleRevision;
            if (actor != hostPeer) return OnlineRosterError.NotHost;
            if (Locked != locked) { Locked = locked; Revision++; }
            return OnlineRosterError.None;
        }

        private uint AllocateId()
        {
            uint id = nextId;
            if (nextId == uint.MaxValue) idExhausted = true;
            else nextId++;
            return id;
        }

        private OnlineRosterError Validate(string actor, ulong epoch, ulong revision)
        {
            if (epoch != Epoch) return OnlineRosterError.WrongEpoch;
            if (revision != Revision) return OnlineRosterError.StaleRevision;
            if (Locked) return OnlineRosterError.Locked;
            if (actor == null || !peers.Contains(actor)) return OnlineRosterError.UnknownPeer;
            return OnlineRosterError.None;
        }

        private OnlineRosterError ValidateHost(string actor, ulong epoch, ulong revision)
        {
            var error = Validate(actor, epoch, revision);
            if (error != OnlineRosterError.None) return error;
            return actor == hostPeer ? OnlineRosterError.None : OnlineRosterError.NotHost;
        }
    }
}
