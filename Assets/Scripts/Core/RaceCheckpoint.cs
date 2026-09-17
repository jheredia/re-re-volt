using UnityEngine;

namespace Core
{
    /// <summary>
    /// Ordered trigger bridge. Index zero is first after the grid; the final
    /// configured index completes a lap.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class RaceCheckpoint : MonoBehaviour
    {
        [SerializeField] private RaceManager raceManager;
        [Min(0)]
        [SerializeField] private int checkpointIndex;

        public int CheckpointIndex => checkpointIndex;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Awake()
        {
            if (raceManager == null)
            {
                raceManager = FindAnyObjectByType<RaceManager>();
            }
        }

        public void Configure(RaceManager manager, int index)
        {
            raceManager = manager;
            checkpointIndex = Mathf.Max(0, index);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (raceManager != null)
            {
                raceManager.ReportCheckpoint(other.gameObject, checkpointIndex);
            }
        }
    }
}
