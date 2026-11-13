using UnityEngine;

namespace Vehicle
{
    /// <summary>Explicit wheel-grip data on a road collider; visual theme never changes this value.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RoadGripSurface : MonoBehaviour
    {
        [SerializeField, Range(.5f, 1.3f)] private float multiplier = 1f;
        public float Multiplier => multiplier;

        public void Configure(float gripMultiplier) => multiplier = Mathf.Clamp(gripMultiplier, .5f, 1.3f);
    }
}
