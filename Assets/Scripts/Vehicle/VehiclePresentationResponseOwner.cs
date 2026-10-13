using AudioPresentation;
using Core;
using UnityEngine;

namespace Vehicle
{
    /// <summary>Owns only one human camera's composition and generated-family visual pivot.</summary>
    [DisallowMultipleComponent]
    public sealed class VehiclePresentationResponseOwner : MonoBehaviour
    {
        private const float FullFovGain = 4f;
        private const float FullImpulseDistance = .18f;
        private const float FullLeanDegrees = 8f;
        private ChaseCamera chaseCamera;
        private VehicleController vehicle;
        private VehicleRecovery recovery;
        private RaceManager race;
        private VehicleImpactAudioEmitter impactEmitter;
        private int playerIndex = -1;
        private PresentationResponseLevel cameraLevel = PresentationResponseLevel.Full;
        private PresentationResponseLevel vehicleLevel = PresentationResponseLevel.Full;
        private Vector3 impulse;
        private Vector3 pivotBasePosition;
        private Quaternion pivotBaseRotation;
        private Transform cachedPivot;
        private bool initialized;
        private bool suspended;

        public int PlayerIndex => playerIndex;
        public PresentationResponseLevel CameraLevel => cameraLevel;
        public PresentationResponseLevel VehicleLevel => vehicleLevel;

        public void Initialize(int index, ChaseCamera chase, VehicleController controlledVehicle,
            VehicleRecovery vehicleRecovery, RaceManager raceManager, VehicleImpactAudioEmitter emitter)
        {
            ClearResponse();
            Unsubscribe();
            playerIndex = index;
            chaseCamera = chase;
            vehicle = controlledVehicle;
            recovery = vehicleRecovery;
            race = raceManager;
            impactEmitter = emitter;
            CachePivotBaseline();
            if (impactEmitter != null) impactEmitter.ImpactObserved += OnImpactObserved;
            if (recovery != null) recovery.Recovered += OnRecovered;
            if (race != null) race.RaceStateChanged += OnRaceStateChanged;
            initialized = true;
        }

        public void SetLevels(PresentationResponseLevel cameraMotion, PresentationResponseLevel vehicleMotion)
        {
            cameraLevel = Validate(cameraMotion);
            vehicleLevel = Validate(vehicleMotion);
            if (cameraLevel == PresentationResponseLevel.Off && vehicleLevel == PresentationResponseLevel.Off)
                ClearResponse();
        }

        public void SetSuspended(bool isSuspended)
        {
            suspended = isSuspended;
            if (suspended) ClearResponse();
        }

        public void ClearResponse()
        {
            impulse = Vector3.zero;
            if (chaseCamera != null) chaseCamera.ClearPresentationResponse();
            RestorePivotBaseline();
        }

        private void Update()
        {
            if (!initialized || chaseCamera == null || vehicle == null || race == null) return;
            if (suspended || race.CurrentState != RaceManager.RaceState.Racing)
            {
                ClearResponse();
                return;
            }

            float delta = Time.unscaledDeltaTime;
            impulse = Vector3.Lerp(impulse, Vector3.zero, 1f - Mathf.Exp(-11f * delta));
            float speed01 = Mathf.Clamp01(vehicle.SpeedMetersPerSecond / Mathf.Max(.01f, vehicle.MaximumSpeedMetersPerSecond));
            float cameraScale = Scale(cameraLevel);
            Vector3 worldImpulse = vehicle.transform.TransformDirection(impulse) * cameraScale;
            chaseCamera.SetPresentationResponse(worldImpulse, worldImpulse * -.15f,
                chaseCamera.BaselineFieldOfView + FullFovGain * speed01 * cameraScale);

            ApplyVisualMotion(speed01, vehicle.SteerInput, vehicle.IsDrifting, vehicle.GroundedWheelCount);
        }

        /// <summary>Presentation-only sample seam; writes only the generated visual child, never vehicle authority.</summary>
        public void ApplyVisualMotion(float speed01, float steer, bool isDrifting, int groundedWheelCount)
        {
            CachePivotBaseline();
            if (cachedPivot == null) return;
            float vehicleScale = Scale(vehicleLevel);
            float drift = isDrifting ? 1f : 0f;
            float airborne = groundedWheelCount == 0 ? 1f : 0f;
            float lean = (-steer * (2f + 4f * speed01) - drift * steer * 2f) * vehicleScale;
            Vector3 recoil = impulse * vehicleScale;
            cachedPivot.localPosition = pivotBasePosition + new Vector3(recoil.x, -airborne * .015f * vehicleScale, recoil.z) * .35f;
            cachedPivot.localRotation = pivotBaseRotation * Quaternion.Euler(recoil.z * 12f,
                0f, Mathf.Clamp(lean + recoil.x * FullLeanDegrees, -FullLeanDegrees, FullLeanDegrees));
        }

        private void OnImpactObserved(VehicleImpactPayload payload)
        {
            if (!isActiveAndEnabled || suspended || payload.PlayerIndex != playerIndex || race == null ||
                race.CurrentState != RaceManager.RaceState.Racing) return;
            Vector3 localNormal = vehicle.transform.InverseTransformDirection(payload.Normal);
            float intensity = Mathf.Clamp01(payload.Impulse / 16f);
            impulse += new Vector3(-localNormal.x, .25f, -localNormal.z) * (FullImpulseDistance * intensity);
            impulse = Vector3.ClampMagnitude(impulse, FullImpulseDistance);
        }

        private void OnRecovered()
        {
            ClearResponse();
            chaseCamera?.SnapToTarget();
        }

        private void OnRaceStateChanged(RaceManager.RaceState state)
        {
            if (state != RaceManager.RaceState.Racing) ClearResponse();
        }

        private void CachePivotBaseline()
        {
            VehicleFamilyVisual visual = vehicle != null ? vehicle.GetComponent<VehicleFamilyVisual>() : null;
            Transform pivot = visual != null ? visual.PresentationPivot : null;
            if (pivot == cachedPivot || pivot == null) return;
            RestorePivotBaseline();
            cachedPivot = pivot;
            pivotBasePosition = pivot.localPosition;
            pivotBaseRotation = pivot.localRotation;
        }

        private void RestorePivotBaseline()
        {
            if (cachedPivot == null) return;
            cachedPivot.localPosition = pivotBasePosition;
            cachedPivot.localRotation = pivotBaseRotation;
        }

        private void OnDisable() => ClearResponse();

        private void OnDestroy()
        {
            ClearResponse();
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (impactEmitter != null) impactEmitter.ImpactObserved -= OnImpactObserved;
            if (recovery != null) recovery.Recovered -= OnRecovered;
            if (race != null) race.RaceStateChanged -= OnRaceStateChanged;
        }

        private static float Scale(PresentationResponseLevel level) => level switch
        {
            PresentationResponseLevel.Off => 0f,
            PresentationResponseLevel.Reduced => .45f,
            _ => 1f
        };

        private static PresentationResponseLevel Validate(PresentationResponseLevel level) =>
            System.Enum.IsDefined(typeof(PresentationResponseLevel), level) ? level : PresentationResponseLevel.Full;
    }
}
