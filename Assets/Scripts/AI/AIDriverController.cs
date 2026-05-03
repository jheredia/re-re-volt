using UnityEngine;
using Vehicle;

namespace AI {
    /// <summary>
    /// Stub for the AI driver. Phase 3 will implement waypoint navigation,
    /// weapon logic, and recovery. For now it just exists so AIManager compiles.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class AIDriverController : MonoBehaviour
    {
        private VehicleController vehicle;
    
        private void Awake()
        {
            vehicle = GetComponent<VehicleController>();
        }
    
        /// <summary>Called every frame by AIManager while the race is running.</summary>
        public void Tick()
        {
            // Phase 3 implementation goes here
        }
    }
}