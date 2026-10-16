using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Core
{
    public enum RacePhase
    {
        Idle,
        Racing,
        Finished
    }

    /// <summary>Read-only-to-consumers runtime state for one registered racer.</summary>
    public sealed class RacerState
    {
        internal RacerState(
            string racerId,
            string displayName,
            bool isHuman,
            int playerIndex,
            int registrationOrder)
        {
            RacerId = racerId;
            DisplayName = displayName;
            IsHuman = isHuman;
            PlayerIndex = playerIndex;
            RegistrationOrder = registrationOrder;
            Reset();
        }

        public string RacerId { get; }
        public string DisplayName { get; }
        public bool IsHuman { get; }
        public int PlayerIndex { get; }
        public int CompletedLaps { get; internal set; }
        public int CurrentLap { get; internal set; }
        public int NextCheckpointIndex { get; internal set; }
        public int LastCheckpointIndex { get; internal set; }
        public float SegmentProgress { get; internal set; }
        public int CurrentPosition { get; internal set; }
        public int FinishPosition { get; internal set; }
        public float FinishTime { get; internal set; }
        public bool IsFinished => FinishPosition > 0;

        internal int RegistrationOrder { get; }

        internal void Reset()
        {
            CompletedLaps = 0;
            CurrentLap = 1;
            NextCheckpointIndex = 0;
            LastCheckpointIndex = -1;
            SegmentProgress = 0f;
            CurrentPosition = RegistrationOrder + 1;
            FinishPosition = 0;
            FinishTime = -1f;
        }
    }

    /// <summary>
    /// Deterministic, Unity-independent authority for ordered checkpoints,
    /// lap completion, standings, and finish results.
    /// </summary>
    public sealed class RaceSession
    {
        public const int DefaultMaximumRacers = 4;

        private readonly List<RacerState> registrationOrder = new();
        private readonly List<RacerState> standings = new();
        private readonly ReadOnlyCollection<RacerState> standingsView;
        private readonly Dictionary<string, RacerState> racersById = new(StringComparer.Ordinal);
        private readonly int maximumRacers;
        private int finishCount;
        private int nextRegistrationOrder;

        public RaceSession(int totalLaps, int checkpointCount, int maximumRacers = DefaultMaximumRacers)
        {
            if (totalLaps < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(totalLaps), "A race needs at least one lap.");
            }

            if (checkpointCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(checkpointCount), "A race needs at least one checkpoint.");
            }

            if (maximumRacers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRacers));
            }

            TotalLaps = totalLaps;
            CheckpointCount = checkpointCount;
            this.maximumRacers = maximumRacers;
            standingsView = standings.AsReadOnly();
        }

        public int TotalLaps { get; private set; }
        public int CheckpointCount { get; private set; }
        public int RacerCount => registrationOrder.Count;
        public float ElapsedTime { get; private set; }
        public RacePhase Phase { get; private set; } = RacePhase.Idle;
        public IReadOnlyList<RacerState> Standings => standingsView;

        public bool TryRegisterRacer(
            string racerId,
            string displayName,
            bool isHuman,
            int playerIndex,
            out RacerState racer)
        {
            racer = null;
            if (Phase != RacePhase.Idle ||
                string.IsNullOrWhiteSpace(racerId) ||
                racersById.ContainsKey(racerId) ||
                registrationOrder.Count >= maximumRacers)
            {
                return false;
            }

            if (isHuman && (playerIndex < 0 || playerIndex >= maximumRacers || HasPlayerIndex(playerIndex)))
            {
                return false;
            }

            racer = new RacerState(
                racerId,
                string.IsNullOrWhiteSpace(displayName) ? racerId : displayName,
                isHuman,
                isHuman ? playerIndex : -1,
                nextRegistrationOrder++);

            registrationOrder.Add(racer);
            racersById.Add(racerId, racer);
            standings.Add(racer);
            UpdateStandings();
            return true;
        }

        /// <summary>Changes idle track rules without replacing registered racer identities.</summary>
        public bool TryReconfigure(int totalLaps, int checkpointCount)
        {
            if (Phase != RacePhase.Idle || totalLaps < 1 || checkpointCount < 1) return false;
            TotalLaps = totalLaps;
            CheckpointCount = checkpointCount;
            Reset();
            return true;
        }

        public bool TryUnregisterRacer(string racerId)
        {
            if (Phase != RacePhase.Idle || !racersById.TryGetValue(racerId, out RacerState racer))
            {
                return false;
            }

            racersById.Remove(racerId);
            registrationOrder.Remove(racer);
            standings.Remove(racer);
            UpdateStandings();
            return true;
        }

        public bool TryGetRacer(string racerId, out RacerState racer)
        {
            return racersById.TryGetValue(racerId, out racer);
        }

        public bool Begin()
        {
            if (Phase != RacePhase.Idle || registrationOrder.Count == 0)
            {
                return false;
            }

            Phase = RacePhase.Racing;
            return true;
        }

        public void Reset()
        {
            Phase = RacePhase.Idle;
            ElapsedTime = 0f;
            finishCount = 0;

            for (int i = 0; i < registrationOrder.Count; i++)
            {
                registrationOrder[i].Reset();
            }

            UpdateStandings();
        }

        public void AdvanceTime(float deltaTime)
        {
            if (Phase == RacePhase.Racing && deltaTime > 0f)
            {
                ElapsedTime += deltaTime;
            }
        }

        public bool TryPassCheckpoint(string racerId, int checkpointIndex)
        {
            if (Phase != RacePhase.Racing ||
                !racersById.TryGetValue(racerId, out RacerState racer) ||
                racer.IsFinished ||
                checkpointIndex != racer.NextCheckpointIndex)
            {
                return false;
            }

            racer.LastCheckpointIndex = checkpointIndex;
            racer.NextCheckpointIndex++;
            racer.SegmentProgress = 0f;

            if (racer.NextCheckpointIndex >= CheckpointCount)
            {
                racer.CompletedLaps++;
                racer.NextCheckpointIndex = 0;

                if (racer.CompletedLaps >= TotalLaps)
                {
                    finishCount++;
                    racer.CurrentLap = TotalLaps;
                    racer.FinishPosition = finishCount;
                    racer.FinishTime = ElapsedTime;
                }
                else
                {
                    racer.CurrentLap = racer.CompletedLaps + 1;
                }
            }

            UpdateStandings();

            if (finishCount == registrationOrder.Count)
            {
                Phase = RacePhase.Finished;
            }

            return true;
        }

        public bool TrySetSegmentProgress(string racerId, float normalizedProgress)
        {
            if (Phase != RacePhase.Racing ||
                !racersById.TryGetValue(racerId, out RacerState racer) ||
                racer.IsFinished)
            {
                return false;
            }

            racer.SegmentProgress = Math.Clamp(normalizedProgress, 0f, 0.9999f);
            UpdateStandings();
            return true;
        }

        private bool HasPlayerIndex(int playerIndex)
        {
            for (int i = 0; i < registrationOrder.Count; i++)
            {
                RacerState racer = registrationOrder[i];
                if (racer.IsHuman && racer.PlayerIndex == playerIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateStandings()
        {
            standings.Sort(CompareRacers);
            for (int i = 0; i < standings.Count; i++)
            {
                standings[i].CurrentPosition = i + 1;
            }
        }

        private static int CompareRacers(RacerState a, RacerState b)
        {
            if (a.IsFinished || b.IsFinished)
            {
                if (a.IsFinished && b.IsFinished)
                {
                    return a.FinishPosition.CompareTo(b.FinishPosition);
                }

                return a.IsFinished ? -1 : 1;
            }

            int lapComparison = b.CompletedLaps.CompareTo(a.CompletedLaps);
            if (lapComparison != 0)
            {
                return lapComparison;
            }

            int checkpointComparison = b.NextCheckpointIndex.CompareTo(a.NextCheckpointIndex);
            if (checkpointComparison != 0)
            {
                return checkpointComparison;
            }

            int segmentComparison = b.SegmentProgress.CompareTo(a.SegmentProgress);
            return segmentComparison != 0
                ? segmentComparison
                : a.RegistrationOrder.CompareTo(b.RegistrationOrder);
        }
    }
}
