using System.Collections.Generic;
using Core;
using UnityEngine;

namespace Vehicle
{
    /// <summary>
    /// Spawns player and AI vehicles at race start, holds references to all
    /// active VehicleControllers, and relays enable/disable signals from
    /// GameManager state changes.
    /// </summary>

    public class VehicleManager : MonoBehaviour
    {
        #region Config
        [Header("Vehicle Prefabs")]
        [SerializeField] private GameObject playerVehiclePrefab;
        [SerializeField] private GameObject aiVehiclePrefab;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;
        #endregion

        #region Runtime
        private VehicleController playerVehicle;
        private readonly List<VehicleController> allVehicles = new();

        public VehicleController PlayerVehicle => playerVehicle;
        public IReadOnlyList<VehicleController> AllVehicles => allVehicles;

        #endregion


        /// <summary>
        /// Spawn vehicles. Call from RaceManager before countdown starts.
        /// </summary>
        public void SpawnVehicles(int aiCount)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("[VehicleManager]: No spawn points assigned.");
                return;
            }

            SpawnPlayer(spawnPoints[0]);

            for (int i = 1; i < aiCount && i < spawnPoints.Length; i++)
            {
                SpawnAI(spawnPoints[i], i);
            }
        }

        public void SetAllVehiclesEnabled(bool enabled)
        {
            foreach (var vehicle in allVehicles)
            {
                vehicle.SetInputEnabled(enabled);
            }
        }

        #region Helpers

        private void SpawnPlayer(Transform spawnPoint)
        {
            if (playerVehiclePrefab == null)
            {
                Debug.LogError("[VehicleManager]: Player vehicle prefab not assigned.");
                return;
            }

            GameObject playerObj = Instantiate(playerVehiclePrefab, spawnPoint.position, spawnPoint.rotation);
            playerObj.name = "PlayerVehicle";
            playerVehicle = playerObj.GetComponent<VehicleController>();

            if (playerVehicle == null) playerVehicle = playerObj.AddComponent<VehicleController>();

            allVehicles.Add(playerVehicle);

            // Register player with RaceManager
            RacerData racerData = new() { DisplayName = "Player", IsPlayer = true, VehicleObject = playerObj };

            GameManager.Instance.RaceManager.RegisterRacer(racerData);
        }

        private void SpawnAI(Transform spawnPoint, int index)
        {
            if (aiVehiclePrefab == null)
            {
                Debug.LogError("[VehicleManager]: AI vehicle prefab not assigned.");
                return;
            }

            GameObject aiObj = Instantiate(aiVehiclePrefab, spawnPoint.position, spawnPoint.rotation);
            aiObj.name = $"AIVehicle_{index}";
            aiObj.TryGetComponent(out VehicleController aiVehicle);

            if (aiVehicle == null) aiVehicle = aiObj.AddComponent<VehicleController>();

            allVehicles.Add(aiVehicle);

            // Register AI with RaceManager
            RacerData racerData = new() { DisplayName = $"AI {index}", IsPlayer = false, VehicleObject = aiObj };

            GameManager.Instance.RaceManager.RegisterRacer(racerData);
        }

        #endregion 
    }
}