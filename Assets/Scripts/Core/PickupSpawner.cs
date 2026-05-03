using UnityEngine;

namespace Core {
    /// <summary>
    /// Stub for pickup spawn points. Phase 4 will implement the full
    /// spawn/respawn cycle and weapon assignment logic.
    /// </summary>
    public class PickupSpawner : MonoBehaviour
    {
        [SerializeField] private float respawnCooldown = 5f;
    
        private void Start()
        {
            GameManager.Instance.WeaponManager.RegisterSpawner(this);
        }
    }
}