using System;
using UnityEngine;

namespace AudioPresentation
{
    /// <summary>Presentation-only collision adapter; it never changes physics state.</summary>
    [DisallowMultipleComponent]
    public sealed class VehicleImpactAudioEmitter : MonoBehaviour
    {
        private int playerIndex = -1;
        private float minimumImpulse;

        public event Action<int, float> Impacted;

        public int PlayerIndex => playerIndex;

        public void Configure(int index, float impulseThreshold)
        {
            playerIndex = index;
            minimumImpulse = Mathf.Max(0f, impulseThreshold);
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impulse = collision.impulse.magnitude;
            if (playerIndex >= 0 && impulse >= minimumImpulse)
                Impacted?.Invoke(playerIndex, impulse);
        }
    }
}
