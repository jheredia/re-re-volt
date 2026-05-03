using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Owns the race lifecycle: countdown, lap tracking, position sorting, finish detection and results.
    /// </summary>

    public class RaceManager : MonoBehaviour
    {
        private static WaitForSeconds _waitForSeconds1 = new(1f);
        #region Race configuration
        // Config 
        [Header("Race Settings")]
        [SerializeField] private int totalLaps = 3;
        [SerializeField] private float countdownTime = 3f;
        #endregion

        #region Race state
        // State
        public enum RaceState { Idle, Countdown, Racing, Finished }
        public RaceState CurrentState { get; private set; } = RaceState.Idle;

        public int TotalLaps => totalLaps;
        public int CurrentLap { get; private set; } = 0;
        public float ElapsedTime { get; private set; } = 0f;
        public bool RaceRunning => CurrentState == RaceState.Racing;

        #endregion

        #region Racer tracking
        // Race tracking

        // Populated by VehicleManager once racers are spawnerd
        private readonly List<RacerData> racers = new();

        public IReadOnlyList<RacerData> Racers => racers;
        #endregion

        void Update()
        {
            if (!RaceRunning) return;
            ElapsedTime += Time.deltaTime; 
            UpdatePositions();
        }

        #region Public API
        public void RegisterRacer(RacerData racer)
        {
            if (racers.Contains(racer)) return;
            racers.Add(racer);
        }

        public void StartRace()
        {
            StartCoroutine(CountdownRoutine());
        }

        public void OnRacerFinished(RacerData racer)
        {
            racer.FinishTime = ElapsedTime;

            if (racer.IsPlayer)
            {
                CurrentState = RaceState.Finished;
                GameManager.Instance.EndGame();
            }
        }
        

        #endregion

        # region Internal logic
        private void UpdatePositions()
        {
            racers.Sort((a, b) =>
            {
                int lapComparison = b.CurrentLap.CompareTo(a.CurrentLap);
                if (lapComparison != 0) return lapComparison;
                return b.WaypointProgress.CompareTo(a.WaypointProgress);
            });

            for (int i = 0; i < racers.Count; i++)
            {
                racers[i].CurrentPosition = i + 1;
            }
        }

        // Countdown
        private IEnumerator CountdownRoutine()
        {
            CurrentState = RaceState.Countdown;
            ElapsedTime = 0f;
            CurrentLap = 0;

            float remaining = countdownTime;
            while (remaining > 0f)
            {
                yield return _waitForSeconds1;
                remaining -= 1f;
            }

            CurrentState = RaceState.Racing;
            GameManager.Instance.StartGame();
        }
        # endregion
    }
}

#region Racer Data Class
/// <summary>
/// Runtime data container for a single racer (player or AI)
/// </summary>
[System.Serializable]
public class RacerData
{
    public string DisplayName { get; set; }
    public bool IsPlayer { get;  set; }
    public int CurrentLap { get; set; } = 0;
    public float WaypointProgress { get; set; } = 0; // Index of last passed waypoint
    public int CurrentPosition {get; set; } = 0; // Current position
    public float FinishTime { get; set; } = -1f;
    public GameObject VehicleObject { get; set; }    
}
#endregion