using System;
using UnityEngine;

namespace AudioPresentation
{
    public readonly struct VehicleImpactPayload
    {
        public VehicleImpactPayload(int playerIndex, float impulse, Vector3 point, Vector3 normal, Collider otherCollider,
            int otherLayer, Vector3 relativeVelocity)
            : this(playerIndex, playerIndex, null, impulse, point, normal, otherCollider, otherLayer, relativeVelocity)
        {
        }

        public VehicleImpactPayload(int presentationSubjectId, int playerIndex, GameObject vehicleObject,
            float impulse, Vector3 point, Vector3 normal, Collider otherCollider, int otherLayer,
            Vector3 relativeVelocity)
        {
            PresentationSubjectId = presentationSubjectId;
            PlayerIndex = playerIndex;
            VehicleObject = vehicleObject;
            Impulse = impulse;
            Point = point;
            Normal = normal;
            OtherCollider = otherCollider;
            OtherLayer = otherLayer;
            RelativeVelocity = relativeVelocity;
        }

        public int PresentationSubjectId { get; }
        public int PlayerIndex { get; }
        public bool HasLocalPlayer => PlayerIndex >= 0;
        public GameObject VehicleObject { get; }
        public float Impulse { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public Collider OtherCollider { get; }
        public int OtherLayer { get; }
        public Vector3 RelativeVelocity { get; }
    }

    /// <summary>Presentation-only collision adapter; it never changes physics state.</summary>
    [DisallowMultipleComponent]
    public sealed class VehicleImpactAudioEmitter : MonoBehaviour
    {
        private int presentationSubjectId = -1;
        private int playerIndex = -1;
        private float minimumImpulse;

        public event Action<int, float> Impacted;
        public event Action<VehicleImpactPayload> ImpactObserved;

        public int PlayerIndex => playerIndex;
        public int PresentationSubjectId => presentationSubjectId;

        public void Configure(int index, float impulseThreshold)
        {
            presentationSubjectId = index;
            playerIndex = index;
            minimumImpulse = Mathf.Max(0f, impulseThreshold);
        }

        /// <summary>Configures a world-only AI subject without creating local HUD, camera, or audio ownership.</summary>
        public void ConfigureWorldOnly(int subjectId, float impulseThreshold)
        {
            if (subjectId < 0) throw new ArgumentOutOfRangeException(nameof(subjectId));
            presentationSubjectId = subjectId;
            playerIndex = -1;
            minimumImpulse = Mathf.Max(0f, impulseThreshold);
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impulse = collision.impulse.magnitude;
            if (presentationSubjectId < 0 || impulse < minimumImpulse) return;
            ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
            Collider other = collision.collider;
            VehicleImpactPayload payload = new(presentationSubjectId, playerIndex, gameObject, impulse,
                contact.point, contact.normal, other, other != null ? other.gameObject.layer : -1,
                collision.relativeVelocity);
            if (playerIndex >= 0) Impacted?.Invoke(playerIndex, impulse);
            try { ImpactObserved?.Invoke(payload); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }
}
