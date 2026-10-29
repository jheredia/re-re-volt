using System;
using System.Collections.Generic;
using Weapons;

namespace ReReVolt.Online.Contracts
{
    [Serializable]
    public struct WorldVector
    {
        public float X, Y, Z;
        public WorldVector(float x, float y, float z) { X = x; Y = y; Z = z; }
        public bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public struct WorldRotation
    {
        public float X, Y, Z, W;
        public bool IsValid
        {
            get
            {
                if (!WorldVector.Finite(X) || !WorldVector.Finite(Y) || !WorldVector.Finite(Z) || !WorldVector.Finite(W)) return false;
                double squared = (double)X * X + (double)Y * Y + (double)Z * Z + (double)W * W;
                return Math.Abs(squared - 1) <= .02;
            }
        }
    }

    public enum DrivingPhase { Countdown, Racing, Results }
    public enum SnapshotError { None, WrongEpoch, WrongRound, WrongTrack, Stale, InvalidState, InvalidRoster }

    [Serializable]
    public struct DrivingRacerState
    {
        public uint RacerId;
        public WorldVector Position, Velocity, AngularVelocity;
        public WorldRotation Rotation;
        public int CompletedLaps, NextCheckpoint, PositionInRace, FinishPosition;
        public float FinishTime;
        public ulong AcknowledgedInput;
        public uint RecoveryRevision;
        public WeaponKind HeldWeapon;
    }

    [Serializable]
    public struct DrivingPickupState
    {
        public int Index;
        public WeaponKind Offering;
        public bool Available;
    }

    [Serializable]
    public struct DrivingWeaponState
    {
        public uint Id, ShooterId;
        public WeaponKind Kind;
        public WorldVector Position, Direction;
        public float Age;
    }

    /// <summary>Host repair state for racers, pickups and the bounded live-weapon set.</summary>
    [Serializable]
    public sealed class DrivingWorldSnapshot
    {
        public ulong Epoch, Sequence, Tick;
        public uint Round;
        public string TrackId;
        public DrivingPhase Phase;
        public int Countdown;
        public float Elapsed;
        public DrivingRacerState[] Racers;
        public DrivingPickupState[] Pickups;
        public DrivingWeaponState[] Weapons;
    }

    /// <summary>Client-side, package-free snapshot validation. No vehicle or race authority is granted.</summary>
    public sealed class OnlineDrivingSnapshotReceiver
    {
        private readonly ulong epoch;
        private readonly uint round;
        private readonly string track;
        private readonly int laps, checkpoints;
        private readonly HashSet<uint> racers = new HashSet<uint>();
        private readonly HashSet<uint> humans = new HashSet<uint>();
        private DrivingWorldSnapshot latest;

        public OnlineDrivingSnapshotReceiver(LobbySnapshot lobby, int totalLaps, int checkpointCount)
        {
            if (lobby == null || lobby.Epoch == 0 || lobby.Round == 0 || string.IsNullOrEmpty(lobby.TrackId) ||
                lobby.Phase == OnlinePhase.Lobby || lobby.Racers == null || lobby.Racers.Length < 1 || lobby.Racers.Length > 4 || totalLaps < 1 || checkpointCount < 1)
                throw new ArgumentException("A locked race roster and valid track rules are required.");
            foreach (var racer in lobby.Racers)
            {
                if (racer == null || racer.Id == 0 || !racers.Add(racer.Id)) throw new ArgumentException("Invalid racer roster.");
                if (!racer.Bot) humans.Add(racer.Id);
            }
            epoch = lobby.Epoch; round = lobby.Round; track = lobby.TrackId;
            laps = totalLaps; checkpoints = checkpointCount;
        }

        // Defensive copies prevent caller mutation from changing the ordering/repair baseline.
        public DrivingWorldSnapshot Latest => latest == null ? null : Copy(latest);

        public SnapshotError Accept(DrivingWorldSnapshot candidate)
        {
            if (candidate == null) return SnapshotError.InvalidState;
            if (candidate.Epoch != epoch) return SnapshotError.WrongEpoch;
            if (candidate.Round != round) return SnapshotError.WrongRound;
            if (!string.Equals(candidate.TrackId, track, StringComparison.Ordinal)) return SnapshotError.WrongTrack;
            if (candidate.Sequence == 0 || (latest != null && (candidate.Sequence <= latest.Sequence || candidate.Tick < latest.Tick)))
                return SnapshotError.Stale;
            if (!Enum.IsDefined(typeof(DrivingPhase), candidate.Phase) || !WorldVector.Finite(candidate.Elapsed) || candidate.Elapsed < 0 ||
                candidate.Countdown < 0 || candidate.Countdown > 3 || (candidate.Phase != DrivingPhase.Countdown && candidate.Countdown != 0) ||
                (latest != null && (candidate.Elapsed < latest.Elapsed || (int)candidate.Phase < (int)latest.Phase)))
                return SnapshotError.InvalidState;
            if (candidate.Racers == null || candidate.Racers.Length != racers.Count) return SnapshotError.InvalidRoster;
            if (candidate.Pickups == null || candidate.Pickups.Length != OnlineCombatInventoryAuthority.PickupCount ||
                candidate.Weapons == null || candidate.Weapons.Length > 16) return SnapshotError.InvalidState;
            for (int i = 0; i < candidate.Pickups.Length; i++)
            {
                DrivingPickupState pickup = candidate.Pickups[i];
                if (pickup.Index != i || (pickup.Offering != WeaponKind.Bolt && pickup.Offering != WeaponKind.Oil) ||
                    (candidate.Phase != DrivingPhase.Racing && pickup.Available)) return SnapshotError.InvalidState;
            }
            var weaponIds = new HashSet<uint>();
            foreach (DrivingWeaponState weapon in candidate.Weapons)
            {
                if (candidate.Phase != DrivingPhase.Racing || weapon.Id == 0 || !weaponIds.Add(weapon.Id) ||
                    !humans.Contains(weapon.ShooterId) ||
                    (weapon.Kind != WeaponKind.Bolt && weapon.Kind != WeaponKind.Oil) ||
                    !weapon.Position.IsFinite || !weapon.Direction.IsFinite || !WorldVector.Finite(weapon.Age) ||
                    weapon.Age < 0 || weapon.Age >= (weapon.Kind == WeaponKind.Bolt ? 3f : 9f))
                    return SnapshotError.InvalidState;
                double directionLength = (double)weapon.Direction.X * weapon.Direction.X +
                    (double)weapon.Direction.Y * weapon.Direction.Y + (double)weapon.Direction.Z * weapon.Direction.Z;
                if (Math.Abs(directionLength - 1) > .03) return SnapshotError.InvalidState;
            }
            var seen = new HashSet<uint>();
            var positions = new HashSet<int>();
            var finishes = new HashSet<int>();
            foreach (var racer in candidate.Racers)
            {
                if (!racers.Contains(racer.RacerId) || !seen.Add(racer.RacerId)) return SnapshotError.InvalidRoster;
                if (!racer.Position.IsFinite || !racer.Velocity.IsFinite || !racer.AngularVelocity.IsFinite || !racer.Rotation.IsValid ||
                    (racer.HeldWeapon != WeaponKind.None && racer.HeldWeapon != WeaponKind.Bolt && racer.HeldWeapon != WeaponKind.Oil) ||
                    racer.CompletedLaps < 0 || racer.CompletedLaps > laps || racer.NextCheckpoint < 0 || racer.NextCheckpoint >= checkpoints ||
                    racer.PositionInRace < 1 || racer.PositionInRace > racers.Count || !positions.Add(racer.PositionInRace) ||
                    racer.FinishPosition < 0 || racer.FinishPosition > racers.Count || !WorldVector.Finite(racer.FinishTime))
                    return SnapshotError.InvalidState;
                if (racer.FinishPosition > 0)
                {
                    if (racer.PositionInRace != racer.FinishPosition || racer.CompletedLaps != laps || racer.NextCheckpoint != 0 || racer.FinishTime < 0 || racer.FinishTime > candidate.Elapsed ||
                        !finishes.Add(racer.FinishPosition)) return SnapshotError.InvalidState;
                }
                else if (racer.CompletedLaps >= laps || racer.FinishTime != -1 || candidate.Phase == DrivingPhase.Results)
                    return SnapshotError.InvalidState;
                if (candidate.Phase == DrivingPhase.Countdown &&
                    (racer.CompletedLaps != 0 || racer.NextCheckpoint != 0 || racer.FinishPosition != 0 ||
                     racer.HeldWeapon != WeaponKind.None)) return SnapshotError.InvalidState;
                if (latest != null)
                    foreach (var previous in latest.Racers)
                    {
                        if (previous.RacerId != racer.RacerId) continue;
                        if (racer.AcknowledgedInput < previous.AcknowledgedInput || racer.RecoveryRevision < previous.RecoveryRevision ||
                            racer.CompletedLaps < previous.CompletedLaps ||
                            (racer.CompletedLaps == previous.CompletedLaps && racer.NextCheckpoint < previous.NextCheckpoint) ||
                            (previous.FinishPosition > 0 && (racer.FinishPosition != previous.FinishPosition || racer.FinishTime != previous.FinishTime)))
                            return SnapshotError.InvalidState;
                    }
            }
            for (int i = 1; i <= finishes.Count; i++) if (!finishes.Contains(i)) return SnapshotError.InvalidState;
            latest = Copy(candidate);
            return SnapshotError.None;
        }

        private static DrivingWorldSnapshot Copy(DrivingWorldSnapshot value) => new DrivingWorldSnapshot
        {
            Epoch = value.Epoch, Sequence = value.Sequence, Tick = value.Tick, Round = value.Round,
            TrackId = value.TrackId, Phase = value.Phase, Countdown = value.Countdown, Elapsed = value.Elapsed,
            Racers = (DrivingRacerState[])value.Racers.Clone(),
            Pickups = (DrivingPickupState[])value.Pickups.Clone(), Weapons = (DrivingWeaponState[])value.Weapons.Clone()
        };
    }
}
