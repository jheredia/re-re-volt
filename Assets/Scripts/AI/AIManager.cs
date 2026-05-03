using System.Collections.Generic;
using Core;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// Owns all AIDriverController instances. Registers them at spawn time
    /// and ticks their decision-making each frame while the race runs.
    /// Phase 3 will flesh this out fully.
    /// </summary>
    public class AIManager : MonoBehaviour
    {
        private readonly List<AIDriverController> drivers = new();

        public void RegisterDriver(AIDriverController driver)
        {
            if (!drivers.Contains(driver))
            {
                drivers.Add(driver);
            }
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsRacing) return;

            foreach (var driver in drivers)
                driver.Tick();
        }
    }
}
