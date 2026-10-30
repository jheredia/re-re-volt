using System;
using AudioPresentation;
using ReReVolt.Online.Contracts;
using UnityEngine;
using Weapons;

namespace ReReVolt.Online.Unity
{
    /// <summary>
    /// Peer-local cue adapter. It borrows the offline session's bounded central 2D mix;
    /// accepted snapshots are observations, never an audio-driven gameplay authority.
    /// </summary>
    public sealed class OnlineAudioPresentation : MonoBehaviour
    {
        private readonly uint[] localRacers = new uint[4];
        private readonly int[] localSlots = new int[4];
        private readonly WeaponKind[] held = new WeaponKind[4];
        private readonly uint[] recovery = new uint[4];
        private RcAudioPresentation audio;
        private OnlineRaceMessageRouting routing;
        private ulong epoch, lastSequence;
        private uint round, lastWeaponId, lastEventId;
        private string trackId;
        private int localCount, lastCountdown;
        private DrivingPhase lastPhase;
        private bool hasSnapshot, ownsAudio;

        public int LocalRacerCount => localCount;

        public void Initialize(LobbySnapshot roster, string localPeer, RcAudioPresentation sessionAudio)
        {
            if (ownsAudio || roster?.Racers == null || roster.Round == 0 ||
                string.IsNullOrWhiteSpace(localPeer) || sessionAudio == null || !sessionAudio.IsConfigured)
                throw new ArgumentException("A configured audio session and locked local roster are required.");
            for (int i = 0; i < roster.Racers.Length; i++)
            {
                LobbyRacer racer = roster.Racers[i];
                if (racer == null || racer.Bot || !string.Equals(racer.Peer, localPeer, StringComparison.Ordinal)) continue;
                if (localCount >= localRacers.Length) throw new ArgumentException("Too many local racers.", nameof(roster));
                int insertion = localCount;
                while (insertion > 0 && localSlots[insertion - 1] > racer.Slot)
                {
                    localSlots[insertion] = localSlots[insertion - 1];
                    localRacers[insertion] = localRacers[insertion - 1];
                    insertion--;
                }
                localSlots[insertion] = racer.Slot;
                localRacers[insertion] = racer.Id;
                localCount++;
            }
            if (localCount == 0) throw new ArgumentException("An online audio peer must own a human racer.", nameof(roster));
            epoch = roster.Epoch;
            round = roster.Round;
            trackId = roster.TrackId;
            audio = sessionAudio;
            try
            {
                audio.BeginOnlineRound(localCount);
                ownsAudio = true;
            }
            catch { Shutdown(); throw; }
        }

        public void Bind(OnlineRaceMessageRouting messageRouting)
        {
            if (!ownsAudio || routing != null || messageRouting == null)
                throw new InvalidOperationException("Initialize the online audio adapter before binding a router.");
            routing = messageRouting;
            if (routing.IsHost) routing.HostSnapshotPublished += ApplySnapshot;
            else
            {
                routing.SnapshotAccepted += ApplySnapshot;
                if (routing.LatestSnapshot != null) ApplySnapshot(routing.LatestSnapshot);
            }
        }

        public void ApplySnapshot(DrivingWorldSnapshot snapshot)
        {
            if (!ownsAudio || snapshot?.Racers == null || snapshot.Weapons == null ||
                snapshot.CombatEvents == null || snapshot.Epoch != epoch || snapshot.Round != round ||
                !string.Equals(snapshot.TrackId, trackId, StringComparison.Ordinal) ||
                snapshot.Sequence <= lastSequence) return;

            bool first = !hasSnapshot;
            if (snapshot.Phase == DrivingPhase.Countdown && snapshot.Countdown > 0 &&
                (first || snapshot.Countdown != lastCountdown)) audio.TryPlayCue(AudioCueId.Countdown);
            else if (!first && lastPhase == DrivingPhase.Countdown && snapshot.Phase == DrivingPhase.Racing)
                audio.TryPlayCue(AudioCueId.Go);
            else if (!first && lastPhase != DrivingPhase.Results && snapshot.Phase == DrivingPhase.Results)
                audio.TryPlayCue(AudioCueId.Finish);

            for (int i = 0; i < localCount; i++)
            {
                for (int j = 0; j < snapshot.Racers.Length; j++)
                {
                    DrivingRacerState state = snapshot.Racers[j];
                    if (state.RacerId != localRacers[i]) continue;
                    WorldVector velocity = state.Velocity;
                    if (velocity.IsFinite)
                        audio.SetOnlineEngineSpeed(i, Mathf.Sqrt(velocity.X * velocity.X +
                            velocity.Y * velocity.Y + velocity.Z * velocity.Z) * 3.6f);
                    if (!first && snapshot.Phase == DrivingPhase.Racing)
                    {
                        if (state.RecoveryRevision > recovery[i]) audio.TryPlayPlayerCue(AudioCueId.Recovery, i);
                        if (held[i] == WeaponKind.None && state.HeldWeapon != WeaponKind.None)
                            audio.TryPlayPlayerCue(AudioCueId.Pickup, i);
                    }
                    held[i] = state.HeldWeapon;
                    recovery[i] = state.RecoveryRevision;
                    break;
                }
            }

            for (int i = 0; i < snapshot.Weapons.Length; i++)
            {
                DrivingWeaponState weapon = snapshot.Weapons[i];
                if (!first && weapon.Id > lastWeaponId && TryLocalIndex(weapon.ShooterId, out int slot) &&
                    snapshot.Phase == DrivingPhase.Racing)
                    audio.TryPlayPlayerCue(weapon.Kind == WeaponKind.Bolt ? AudioCueId.BoltLaunch : AudioCueId.OilDrop, slot);
                lastWeaponId = Math.Max(lastWeaponId, weapon.Id);
            }
            for (int i = 0; i < snapshot.CombatEvents.Length; i++)
            {
                DrivingCombatEvent effect = snapshot.CombatEvents[i];
                if (!first && effect.Id > lastEventId && snapshot.Phase == DrivingPhase.Racing)
                {
                    if (effect.Kind == DrivingCombatEventKind.Hit && TryLocalIndex(effect.TargetId, out int victim))
                        audio.TryPlayPlayerCue(effect.Weapon == WeaponKind.Bolt ? AudioCueId.BoltHit : AudioCueId.OilHit, victim);
                    else if (effect.Kind == DrivingCombatEventKind.WorldImpact &&
                        TryLocalIndex(effect.ShooterId, out int shooter))
                        audio.TryPlayPlayerCue(AudioCueId.Impact, shooter);
                }
                lastEventId = Math.Max(lastEventId, effect.Id);
            }
            lastSequence = snapshot.Sequence;
            lastPhase = snapshot.Phase;
            lastCountdown = snapshot.Countdown;
            hasSnapshot = true;
        }

        private bool TryLocalIndex(uint racerId, out int index)
        {
            for (int i = 0; i < localCount; i++)
                if (localRacers[i] == racerId) { index = i; return true; }
            index = -1;
            return false;
        }

        public void Shutdown()
        {
            if (routing != null)
            {
                routing.HostSnapshotPublished -= ApplySnapshot;
                routing.SnapshotAccepted -= ApplySnapshot;
            }
            routing = null;
            if (ownsAudio && audio != null) audio.EndOnlineRound();
            audio = null;
            ownsAudio = false;
            Array.Clear(localRacers, 0, localRacers.Length);
            Array.Clear(localSlots, 0, localSlots.Length);
            Array.Clear(held, 0, held.Length);
            Array.Clear(recovery, 0, recovery.Length);
            epoch = lastSequence = 0;
            round = lastWeaponId = lastEventId = 0;
            trackId = null;
            localCount = lastCountdown = 0;
            hasSnapshot = false;
        }

        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();
    }
}
