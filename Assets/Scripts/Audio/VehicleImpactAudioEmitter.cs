using System;
using UnityEngine;

namespace AudioPresentation
{
    public readonly struct VehicleImpactPayload
    {
        public VehicleImpactPayload(int playerIndex, float impulse, Vector3 point, Vector3 normal, Collider otherCollider,
            int otherLayer, Vector3 relativeVelocity)
        {
            PlayerIndex = playerIndex;
            Impulse = impulse;
            Point = point;
            Normal = normal;
            OtherCollider = otherCollider;
            OtherLayer = otherLayer;
            RelativeVelocity = relativeVelocity;
        }

        public int PlayerIndex { get; }
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
        private int playerIndex = -1;
        private float minimumImpulse;

        public event Action<int, float> Impacted;
        public event Action<VehicleImpactPayload> ImpactObserved;

        public int PlayerIndex => playerIndex;

        public void Configure(int index, float impulseThreshold)
        {
            playerIndex = index;
            minimumImpulse = Mathf.Max(0f, impulseThreshold);
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impulse = collision.impulse.magnitude;
            if (playerIndex < 0 || impulse < minimumImpulse) return;
            ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
            Collider other = collision.collider;
            VehicleImpactPayload payload = new(playerIndex, impulse, contact.point, contact.normal, other,
                other != null ? other.gameObject.layer : -1, collision.relativeVelocity);
            Impacted?.Invoke(playerIndex, impulse);
            ImpactObserved?.Invoke(payload);
        }
    }
}
