using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Unity-facing race coordinator. It owns countdown timing and maps scene
    /// vehicles to the deterministic RaceSession rules model.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public enum RaceState
        {
            Idle,
            Countdown,
            Racing,
            Finished
        }

        [Header("Race Settings")]
        [Min(1)]
        [SerializeField] private int totalLaps = 3;
        [Min(0f)]
        [SerializeField] private float countdownTime = 3f;
        [Min(1)]
        [SerializeField] private int checkpointCount = 1;

        private readonly Dictionary<GameObject, string> racerIdsByVehicle = new();
        private RaceSession session;
        private Coroutine countdownRoutine;

        public event Action<RaceState> RaceStateChanged;
        public event Action<int> CountdownChanged;
        public event Action<RacerState> RacerRegistered;
        public event Action<RacerState> RacerProgressChanged;
        public event Action<RacerState> RacerFinished;
        public event Action<IReadOnlyList<RacerState>> RaceCompleted;

        public RaceState CurrentState { get; private set; } = RaceState.Idle;
        public int TotalLaps => session?.TotalLaps ?? Mathf.Max(1, totalLaps);
        public int CheckpointCount => session?.CheckpointCount ?? Mathf.Max(1, checkpointCount);
        public float ElapsedTime => session?.ElapsedTime ?? 0f;
        public bool RaceRunning => CurrentState == RaceState.Racing;
        public IReadOnlyList<RacerState> Racers => session?.Standings ?? Array.Empty<RacerState>();

        private void Awake()
        {
            CreateSession();
        }

        private void Update()
        {
            if (RaceRunning)
            {
                session.AdvanceTime(Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
                countdownRoutine = null;
                session.Reset();
                SetState(RaceState.Idle);
            }
        }

        private void OnValidate()
        {
            totalLaps = Mathf.Max(1, totalLaps);
            checkpointCount = Mathf.Max(1, checkpointCount);
            countdownTime = Mathf.Max(0f, countdownTime);
        }

        /// <summary>Configures a runtime-built track before racers register.</summary>
        public bool ConfigureRace(int laps, int orderedCheckpointCount)
        {
            EnsureSession();
            if (CurrentState != RaceState.Idle || session.RacerCount > 0 || laps < 1 || orderedCheckpointCount < 1)
            {
                return false;
            }

            totalLaps = laps;
            checkpointCount = orderedCheckpointCount;
            CreateSession();
            return true;
        }

        public RacerState RegisterRacer(
            string racerId,
            GameObject vehicleObject,
            string displayName,
            bool isHuman,
            int playerIndex = -1)
        {
            EnsureSession();
            if (vehicleObject == null || racerIdsByVehicle.ContainsKey(vehicleObject))
            {
                Debug.LogError("[RaceManager] A racer needs a unique vehicle object.", this);
                return null;
            }

            if (!session.TryRegisterRacer(racerId, displayName, isHuman, playerIndex, out RacerState racer))
            {
                Debug.LogError($"[RaceManager] Could not register racer '{racerId}'. Check ID, player index, state, and the four-racer limit.", this);
                return null;
            }

            racerIdsByVehicle.Add(vehicleObject, racerId);
            RacerRegistered?.Invoke(racer);
            return racer;
        }

        public bool UnregisterRacer(GameObject vehicleObject)
        {
            EnsureSession();
            if (vehicleObject == null ||
                !racerIdsByVehicle.TryGetValue(vehicleObject, out string racerId) ||
                !session.TryUnregisterRacer(racerId))
            {
                return false;
            }

            racerIdsByVehicle.Remove(vehicleObject);
            return true;
        }

        public void ClearRacers()
        {
            EnsureSession();
            if (CurrentState != RaceState.Idle)
            {
                Debug.LogWarning("[RaceManager] Racers can only be cleared while the race is idle.", this);
                return;
            }

            CreateSession();
            racerIdsByVehicle.Clear();
        }

        public bool TryGetRacer(GameObject vehicleOrChild, out RacerState racer)
        {
            racer = null;
            return TryResolveRacerId(vehicleOrChild, out string racerId) && session.TryGetRacer(racerId, out racer);
        }

        public bool ReportCheckpoint(GameObject vehicleOrChild, int checkpointIndex)
        {
            return TryResolveRacerId(vehicleOrChild, out string racerId) &&
                   ReportCheckpoint(racerId, checkpointIndex);
        }

        public bool ReportCheckpoint(string racerId, int checkpointIndex)
        {
            EnsureSession();
            if (!session.TryGetRacer(racerId, out RacerState racer))
            {
                return false;
            }

            bool wasFinished = racer.IsFinished;
            if (!session.TryPassCheckpoint(racerId, checkpointIndex))
            {
                return false;
            }

            RacerProgressChanged?.Invoke(racer);
            if (!wasFinished && racer.IsFinished)
            {
                RacerFinished?.Invoke(racer);
            }

            if (session.Phase == RacePhase.Finished)
            {
                SetState(RaceState.Finished);
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.EndGame();
                }

                RaceCompleted?.Invoke(session.Standings);
            }

            return true;
        }

        public bool ReportSegmentProgress(GameObject vehicleOrChild, float normalizedProgress)
        {
            EnsureSession();
            return TryResolveRacerId(vehicleOrChild, out string racerId) &&
                   session.TrySetSegmentProgress(racerId, normalizedProgress);
        }

        public bool StartRace()
        {
            EnsureSession();
            if (CurrentState != RaceState.Idle || session.RacerCount == 0)
            {
                Debug.LogWarning("[RaceManager] StartRace requires an idle race with at least one registered racer.", this);
                return false;
            }

            session.Reset();
            SetState(RaceState.Countdown);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameManager.GameState.Loading);
            }

            countdownRoutine = StartCoroutine(CountdownRoutine());
            return true;
        }

        /// <summary>Resets progress while retaining joined racers for a rematch.</summary>
        public void ResetRace()
        {
            EnsureSession();
            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
                countdownRoutine = null;
            }

            session.Reset();
            SetState(RaceState.Idle);
            CountdownChanged?.Invoke(-1);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMainMenu();
            }
        }

        private IEnumerator CountdownRoutine()
        {
            float remaining = countdownTime;
            int shownNumber = -1;

            while (remaining > 0f)
            {
                int nextNumber = Mathf.CeilToInt(remaining);
                if (nextNumber != shownNumber)
                {
                    shownNumber = nextNumber;
                    CountdownChanged?.Invoke(shownNumber);
                }

                float step = Mathf.Min(1f, remaining);
                yield return new WaitForSecondsRealtime(step);
                remaining -= step;
            }

            countdownRoutine = null;
            if (!session.Begin())
            {
                Debug.LogError("[RaceManager] The race session could not enter Racing state.", this);
                SetState(RaceState.Idle);
                yield break;
            }

            CountdownChanged?.Invoke(0);
            SetState(RaceState.Racing);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartGame();
            }
        }

        private bool TryResolveRacerId(GameObject vehicleOrChild, out string racerId)
        {
            racerId = null;
            if (vehicleOrChild == null)
            {
                return false;
            }

            Transform candidate = vehicleOrChild.transform;
            while (candidate != null)
            {
                if (racerIdsByVehicle.TryGetValue(candidate.gameObject, out racerId))
                {
                    return true;
                }

                candidate = candidate.parent;
            }

            return false;
        }

        private void CreateSession()
        {
            session = new RaceSession(Mathf.Max(1, totalLaps), Mathf.Max(1, checkpointCount));
            racerIdsByVehicle.Clear();
        }

        private void EnsureSession()
        {
            if (session == null)
            {
                CreateSession();
            }
        }

        private void SetState(RaceState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            CurrentState = newState;
            RaceStateChanged?.Invoke(newState);
        }
    }
}
