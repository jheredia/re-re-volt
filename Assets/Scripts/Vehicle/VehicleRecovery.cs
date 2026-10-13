using System;
using Core;
using UnityEngine;

namespace Vehicle
{
    public readonly struct RecoveryPresentationPayload
    {
        public RecoveryPresentationPayload(Pose destination)
        {
            Destination = destination;
        }

        public Pose Destination { get; }
    }

    /// <summary>Returns to the last accepted checkpoint, then the grid, without changing race progress.</summary>
    [RequireComponent(typeof(VehicleController), typeof(Rigidbody))]
    public sealed class VehicleRecovery : MonoBehaviour
    {
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private Vector3 clearanceHalfExtents = new Vector3(.72f, .35f, 1f);
        [SerializeField] private float groundClearance = .65f;
        private readonly RaycastHit[] groundHits = new RaycastHit[32];
        private readonly Collider[] overlaps = new Collider[32];
        private VehicleController vehicle;
        private RaceManager race;
        private Pose gridPose;
        private Pose checkpointPose;
        private bool hasCheckpoint;
        private bool initialized;
        private float nextRecoveryTime;

        public event Action Recovered;
        public event Action<RecoveryPresentationPayload> RecoveredAt;

        private void Awake() => vehicle = GetComponent<VehicleController>();

        public void Initialize(Pose fallback, RaceManager raceManager)
        {
            race = raceManager;
            gridPose = fallback;
            hasCheckpoint = false;
            initialized = true;
            nextRecoveryTime = 0f;
        }

        /// <summary>Call only for an accepted checkpoint. Anchor forward must point along the circuit.</summary>
        public void RecordCheckpoint(Pose pose)
        {
            checkpointPose = pose;
            hasCheckpoint = true;
        }

        public void ConfigureGroundMask(LayerMask mask) => groundMask = mask;

        private void Update()
        {
            if (initialized && race != null && race.RaceRunning && Time.timeScale > 0f &&
                transform.position.y < gridPose.position.y - 15f) TryRecover();
        }

        public bool TryRecover()
        {
            if (!initialized || Time.unscaledTime < nextRecoveryTime) return false;
            nextRecoveryTime = Time.unscaledTime + .5f;
            Physics.SyncTransforms();
            if (!(hasCheckpoint && TryFindClearPose(checkpointPose, out Pose destination)) &&
                !TryFindClearPose(gridPose, out destination)) return false;
            vehicle.Teleport(destination);
            if (race != null) race.ReportSegmentProgress(gameObject, 0f);
            Recovered?.Invoke();
            try { RecoveredAt?.Invoke(new RecoveryPresentationPayload(destination)); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            return true;
        }

        public void ResetToGrid(Pose pose)
        {
            gridPose = pose;
            hasCheckpoint = false;
            nextRecoveryTime = 0f;
            vehicle.Teleport(pose);
        }

        private bool TryFindClearPose(Pose anchor, out Pose result)
        {
            result = default;
            Quaternion rotation = Quaternion.Euler(0f, anchor.rotation.eulerAngles.y, 0f);
            // Search beside and behind the anchor, never forward through the next checkpoint.
            for (int attempt = 0; attempt < 7; attempt++)
            {
                float lateral = attempt == 0 ? 0f : (attempt % 2 == 0 ? 2f : -2f);
                float backward = attempt < 3 ? 0f : -2.5f * ((attempt - 1) / 2);
                Vector3 probe = anchor.position + rotation * new Vector3(lateral, 0f, backward);
                int count = Physics.RaycastNonAlloc(probe + Vector3.up * 3f, Vector3.down, groundHits,
                    8f, groundMask, QueryTriggerInteraction.Ignore);
                if (count == groundHits.Length) continue;
                float nearest = float.PositiveInfinity;
                int support = -1;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = groundHits[i];
                    if (hit.collider.GetComponentInParent<VehicleController>() != null || hit.normal.y < .8f || hit.distance >= nearest) continue;
                    support = i;
                    nearest = hit.distance;
                }
                if (support < 0) continue;
                Vector3 position = groundHits[support].point + Vector3.up * groundClearance;
                int overlapCount = Physics.OverlapBoxNonAlloc(position, clearanceHalfExtents, overlaps, rotation,
                    ~0, QueryTriggerInteraction.Ignore);
                if (overlapCount == overlaps.Length) continue;
                bool blocked = false;
                for (int i = 0; i < overlapCount; i++)
                    if (!overlaps[i].transform.IsChildOf(transform)) blocked = true;
                if (blocked) continue;
                result = new Pose(position, rotation);
                return true;
            }
            return false;
        }
    }
}
