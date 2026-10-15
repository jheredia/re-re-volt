using System;
using Core;
using UnityEngine;
using Vehicle;

namespace AI
{
    /// <summary>Deterministic command produced from one bot's local target geometry.</summary>
    public readonly struct AIDrivingDecision
    {
        public AIDrivingDecision(float throttle, float steer, float brake)
        {
            Throttle = Mathf.Clamp(throttle, -1f, 1f);
            Steer = Mathf.Clamp(steer, -1f, 1f);
            Brake = Mathf.Clamp01(brake);
        }

        public float Throttle { get; }
        public float Steer { get; }
        public float Brake { get; }
    }

    /// <summary>Shared neutral driving policy for the Stage 9A test opponents.</summary>
    public static class AIDrivingPolicy
    {
        private const float LaneOffset = .65f;

        public static AIDrivingDecision Evaluate(Vector3 localTarget, float speedKilometresPerHour,
            bool prepareForUpcomingTurn = false)
        {
            float targetAngle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
            float absoluteAngle = Mathf.Abs(targetAngle);
            float steer = Mathf.Clamp(targetAngle / 32f, -1f, 1f);
            float throttle = absoluteAngle > 70f ? .28f : absoluteAngle > 42f ? .52f : .92f;
            float brake = absoluteAngle > 58f && speedKilometresPerHour > 20f ? .65f : 0f;
            if (prepareForUpcomingTurn)
            {
                throttle = Mathf.Min(throttle, .52f);
                if (speedKilometresPerHour > 20f) brake = Mathf.Max(brake, .65f);
            }
            return new AIDrivingDecision(throttle, steer, brake);
        }

        /// <summary>
        /// Keeps a spawned bot on the same side of the first straight as its grid position. This avoids
        /// crossing through another grid occupant before the route has had room to separate the cars.
        /// </summary>
        public static float CalculateSpawnLaneOffset(Vector3 gridPosition, Vector3 firstCheckpointPosition,
            Vector3 firstCheckpointRight)
        {
            firstCheckpointRight.y = 0f;
            if (firstCheckpointRight.sqrMagnitude < .001f) return 0f;
            firstCheckpointRight.Normalize();
            Vector3 fromCheckpoint = gridPosition - firstCheckpointPosition;
            fromCheckpoint.y = 0f;
            float signedLateralOffset = Vector3.Dot(fromCheckpoint, firstCheckpointRight);
            return Mathf.Abs(signedLateralOffset) < .01f ? 0f : Mathf.Sign(signedLateralOffset) * LaneOffset;
        }

        /// <summary>
        /// Finds the lane-aligned corner between two perpendicular route legs. The point is guidance only:
        /// authoritative race progress still comes exclusively from ordered checkpoint triggers.
        /// </summary>
        public static bool TryCalculateTurnPoint(Vector3 previousPosition, Vector3 previousForward,
            Vector3 targetPosition, Vector3 targetForward, out Vector3 turnPoint)
        {
            turnPoint = default;
            previousForward.y = 0f;
            targetForward.y = 0f;
            if (previousForward.sqrMagnitude < .001f || targetForward.sqrMagnitude < .001f) return false;
            previousForward.Normalize();
            targetForward.Normalize();
            if (Mathf.Abs(Vector3.Dot(previousForward, targetForward)) > .1f) return false;

            float cross = previousForward.x * targetForward.z - previousForward.z * targetForward.x;
            if (Mathf.Abs(cross) < .1f) return false;

            Vector3 delta = targetPosition - previousPosition;
            float distanceAlongPrevious = (delta.x * targetForward.z - delta.z * targetForward.x) / cross;
            Vector3 candidate = previousPosition + previousForward * distanceAlongPrevious;
            float distanceBeforeTarget = Vector3.Dot(targetPosition - candidate, targetForward);
            if (distanceAlongPrevious <= .1f || distanceBeforeTarget <= .1f) return false;

            candidate.y = Mathf.Lerp(previousPosition.y, targetPosition.y, .5f);
            turnPoint = candidate;
            return true;
        }
    }

    /// <summary>
    /// Drives one non-human racer through the existing VehicleController command seam.
    /// Checkpoint triggers remain the only lap authority; recovery only repositions to an
    /// already accepted checkpoint and never advances race progress.
    /// </summary>
    [RequireComponent(typeof(VehicleController), typeof(VehicleRecovery), typeof(Rigidbody))]
    public sealed class AIDriverController : MonoBehaviour
    {
        private const float StuckSpeedKilometresPerHour = 1.25f;
        private const float StuckRecoverySeconds = 2.75f;
        private const float OffTrackRecoverySeconds = 1.5f;
        private const float ProgressRecoverySeconds = 8f;
        private const float TurnCommitDistance = 5f;

        private readonly RaycastHit[] surfaceHits = new RaycastHit[8];
        private VehicleController vehicle;
        private VehicleRecovery recovery;
        private RaceManager race;
        private RacerState racer;
        private Transform[] checkpoints;
        private LayerMask drivableMask;
        private float laneOffset;
        private float stuckSeconds;
        private float offTrackSeconds;
        private float progressStallSeconds;
        private float bestSegmentProgress;
        private int observedTargetIndex = -1;
        private int guidanceTargetIndex = -1;
        private Vector3 turnGuidancePosition;
        private Vector3 turnIncomingDirection;
        private bool turnGuidanceActive;
        private bool initialized;

        public RacerState Racer => racer;
        public VehicleController Vehicle => vehicle;
        public bool IsInitialized => initialized;
        public AIDrivingDecision LastDecision { get; private set; }
        public int RecoveryCount { get; private set; }

        private void Awake()
        {
            vehicle = GetComponent<VehicleController>();
            recovery = GetComponent<VehicleRecovery>();
            vehicle.SetInputEnabled(false);
        }

        public void Initialize(RaceManager raceManager, RacerState racerState, Transform[] orderedCheckpoints,
            VehicleRecovery vehicleRecovery, LayerMask roadMask, float deterministicLaneOffset)
        {
            if (raceManager == null) throw new ArgumentNullException(nameof(raceManager));
            if (racerState == null || racerState.IsHuman)
                throw new ArgumentException("An AI driver requires a registered non-human racer.", nameof(racerState));
            if (orderedCheckpoints == null || orderedCheckpoints.Length != raceManager.CheckpointCount)
                throw new ArgumentException("AI guidance must match the authoritative ordered checkpoint count.",
                    nameof(orderedCheckpoints));
            for (int i = 0; i < orderedCheckpoints.Length; i++)
                if (orderedCheckpoints[i] == null)
                    throw new ArgumentException("AI guidance cannot contain a missing checkpoint.",
                        nameof(orderedCheckpoints));

            race = raceManager;
            racer = racerState;
            checkpoints = (Transform[])orderedCheckpoints.Clone();
            recovery = vehicleRecovery != null ? vehicleRecovery : GetComponent<VehicleRecovery>();
            drivableMask = roadMask;
            laneOffset = Mathf.Clamp(deterministicLaneOffset, -1.1f, 1.1f);
            initialized = true;
            ResetDecisionState();
        }

        public void SetDrivingEnabled(bool enabled)
        {
            bool canDrive = enabled && initialized && racer != null && !racer.IsFinished;
            vehicle.SetInputEnabled(canDrive);
            if (canDrive) return;
            vehicle.ThrottleInput = 0f;
            vehicle.SteerInput = 0f;
            vehicle.BrakeInput = 0f;
            LastDecision = default;
        }

        /// <summary>Called on the physics cadence by the roster owner while racing is authoritative.</summary>
        public void Tick(float deltaTime)
        {
            if (!initialized || race == null || !race.RaceRunning || racer == null || racer.IsFinished ||
                Time.timeScale <= 0f)
            {
                SetDrivingEnabled(false);
                return;
            }

            int targetIndex = Mathf.Clamp(racer.NextCheckpointIndex, 0, checkpoints.Length - 1);
            Transform target = checkpoints[targetIndex];
            Vector3 targetPosition = target.position + target.right * laneOffset;
            Vector3 guidancePosition = ResolveGuidancePosition(targetIndex, targetPosition);
            Vector3 localTarget = transform.InverseTransformPoint(guidancePosition);
            LastDecision = AIDrivingPolicy.Evaluate(localTarget, vehicle.CurrentSpeed,
                HasUpcomingPerpendicularTurn(targetIndex, targetPosition));
            vehicle.SetInputEnabled(true);
            vehicle.ThrottleInput = LastDecision.Throttle;
            vehicle.SteerInput = LastDecision.Steer;
            vehicle.BrakeInput = LastDecision.Brake;

            float segmentProgress = CalculateSegmentProgress(targetIndex, targetPosition);
            UpdateRecovery(deltaTime, LastDecision.Throttle, targetIndex, segmentProgress);
        }

        private Vector3 ResolveGuidancePosition(int targetIndex, Vector3 targetPosition)
        {
            if (guidanceTargetIndex != targetIndex)
            {
                guidanceTargetIndex = targetIndex;
                int previousIndex = (targetIndex - 1 + checkpoints.Length) % checkpoints.Length;
                Transform previous = checkpoints[previousIndex];
                Vector3 previousPosition = previous.position + previous.right * laneOffset;
                turnIncomingDirection = Vector3.ProjectOnPlane(previous.forward, Vector3.up).normalized;
                turnGuidanceActive = AIDrivingPolicy.TryCalculateTurnPoint(previousPosition, previous.forward,
                    targetPosition, checkpoints[targetIndex].forward, out turnGuidancePosition);
            }

            if (turnGuidanceActive && Vector3.Dot(transform.position - turnGuidancePosition,
                    turnIncomingDirection) >= -TurnCommitDistance)
                turnGuidanceActive = false;
            return turnGuidanceActive ? turnGuidancePosition : targetPosition;
        }

        private bool HasUpcomingPerpendicularTurn(int targetIndex, Vector3 targetPosition)
        {
            int upcomingIndex = (targetIndex + 1) % checkpoints.Length;
            Transform upcoming = checkpoints[upcomingIndex];
            Vector3 upcomingPosition = upcoming.position + upcoming.right * laneOffset;
            return AIDrivingPolicy.TryCalculateTurnPoint(targetPosition, checkpoints[targetIndex].forward,
                upcomingPosition, upcoming.forward, out _);
        }

        public void ResetToGrid(Pose gridPose)
        {
            recovery.ResetToGrid(gridPose);
            RecoveryCount = 0;
            ResetDecisionState();
        }

        private float CalculateSegmentProgress(int targetIndex, Vector3 targetPosition)
        {
            int previousIndex = (targetIndex - 1 + checkpoints.Length) % checkpoints.Length;
            Vector3 start = checkpoints[previousIndex].position + checkpoints[previousIndex].right * laneOffset;
            Vector3 segment = targetPosition - start;
            float denominator = segment.sqrMagnitude;
            float progress = denominator > .001f
                ? Vector3.Dot(transform.position - start, segment) / denominator
                : 0f;
            return Mathf.Clamp01(progress);
        }

        private void UpdateRecovery(float deltaTime, float throttle, int targetIndex, float segmentProgress)
        {
            bool tryingToMove = throttle > .2f;
            stuckSeconds = tryingToMove && vehicle.CurrentSpeed < StuckSpeedKilometresPerHour
                ? stuckSeconds + Mathf.Max(0f, deltaTime)
                : 0f;

            int hitCount = Physics.RaycastNonAlloc(transform.position + Vector3.up * .7f, Vector3.down,
                surfaceHits, 3f, drivableMask, QueryTriggerInteraction.Ignore);
            offTrackSeconds = hitCount == 0
                ? offTrackSeconds + Mathf.Max(0f, deltaTime)
                : 0f;

            if (targetIndex != observedTargetIndex)
            {
                observedTargetIndex = targetIndex;
                bestSegmentProgress = segmentProgress;
                progressStallSeconds = 0f;
            }
            else if (segmentProgress > bestSegmentProgress + .01f)
            {
                bestSegmentProgress = segmentProgress;
                progressStallSeconds = 0f;
            }
            else
            {
                progressStallSeconds += Mathf.Max(0f, deltaTime);
            }

            if (stuckSeconds < StuckRecoverySeconds && offTrackSeconds < OffTrackRecoverySeconds &&
                progressStallSeconds < ProgressRecoverySeconds) return;
            if (recovery.TryRecover()) RecoveryCount++;
            ResetDecisionState();
        }

        private void ResetDecisionState()
        {
            stuckSeconds = 0f;
            offTrackSeconds = 0f;
            progressStallSeconds = 0f;
            bestSegmentProgress = 0f;
            observedTargetIndex = -1;
            guidanceTargetIndex = -1;
            turnGuidanceActive = false;
            LastDecision = default;
            if (vehicle == null) return;
            vehicle.ThrottleInput = 0f;
            vehicle.SteerInput = 0f;
            vehicle.BrakeInput = 0f;
        }

        private void OnDisable()
        {
            if (vehicle != null) SetDrivingEnabled(false);
        }
    }
}
