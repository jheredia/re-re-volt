using System.Collections.Generic;
using Core;
using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Tracks all active pickup spawners and live weapon projectiles/traps.
    /// Phase 4 will flesh this out.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
       private readonly List<PickupSpawner> spawners = new();
       private readonly List<GameObject> liveWeapons = new();

       public void RegisterSpawner(PickupSpawner spawner)
       {
           if (!spawners.Contains(spawner))
           {
               spawners.Add(spawner);
           }
       }

       public void RegisterLiveWeapon(GameObject weapon)
       {
            liveWeapons.Add(weapon);   
       }

       public void DeregisterLiveWeapon(GameObject weapon)
       {
            liveWeapons.Remove(weapon);
       }

        /// <summary>
        /// Called by GameManager on race reset to clean up any remaining pickups or projectiles in the scene.
        /// </summary>
       public void ClearAllWeapons()
        {
            foreach (var weapon in liveWeapons)
                if (weapon != null) Destroy(weapon);
            liveWeapons.Clear();
        }
    }
}
