using System;
using System.Collections.Generic;
using Core;
using AudioPresentation;
using Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using Vehicle;

namespace AI
{
    /// <summary>World-presentation references for a bot; carries no local-player or viewport ownership.</summary>
    public readonly struct AIVehiclePresentationHandle
    {
        public AIVehiclePresentationHandle(int botIndex, VehicleController vehicle, VehicleRecovery recovery,
            VehicleImpactAudioEmitter impactEmitter)
        {
            BotIndex = botIndex;
            Vehicle = vehicle;
            Recovery = recovery;
            ImpactEmitter = impactEmitter;
        }

        public int BotIndex { get; }
        public VehicleController Vehicle { get; }
        public VehicleRecovery Recovery { get; }
        public VehicleImpactAudioEmitter ImpactEmitter { get; }
    }

    /// <summary>
    /// Owns the non-human roster independently from VehicleManager. Bots are created only when
    /// a race is submitted, so setup remains free for up to four human device owners.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AIManager : MonoBehaviour
    {
        private sealed class BotEntry
        {
            internal int BotIndex;
            internal int GridIndex;
            internal GameObject VehicleObject;
            internal VehicleController Vehicle;
            internal AIDriverController Driver;
            internal Renderer[] Renderers;
            internal VehicleRecovery Recovery;
            internal VehicleImpactAudioEmitter ImpactEmitter;
        }

        private static readonly Color[] BotColors =
        {
            new(.72f, .28f, .92f),
            new(.08f, .8f, .78f),
            new(.95f, .45f, .1f)
        };

        private readonly List<BotEntry> bots = new();
        private readonly List<AIDriverController> drivers = new();
        private IReadOnlyList<AIDriverController> driversView;
        private RaceManager race;
        private VehicleManager humans;
        private Transform[] grid;
        private Transform[] checkpoints;
        private LayerMask drivableMask;
        private VisualStyleController visualStyle;
        private bool configured;
        private bool subscribed;
        private int selectedBotCount;

        public event Action<int> SelectedBotCountChanged;
        public event Action<int> ActiveBotCountChanged;
        public event Action<AIVehiclePresentationHandle> BotPresentationAdded;
        public event Action<AIVehiclePresentationHandle> BotPresentationRemoving;

        public int SelectedBotCount => selectedBotCount;
        public int MaximumSelectableBots => humans == null ? 0 : Mathf.Clamp(4 - humans.Players.Count, 0, 3);
        public int ActiveBotCount => bots.Count;
        public IReadOnlyList<AIDriverController> Drivers => driversView ??= drivers.AsReadOnly();
        public string LastRosterError { get; private set; }

        public bool Configure(RaceManager raceManager, VehicleManager humanRoster, Transform[] gridPoints,
            RaceCheckpoint[] orderedCheckpoints, LayerMask roadMask,
            VisualStyleController visualStyleController = null)
        {
            if (raceManager == null || humanRoster == null || gridPoints == null || gridPoints.Length < 4 ||
                orderedCheckpoints == null || orderedCheckpoints.Length != raceManager.CheckpointCount ||
                raceManager.CurrentState != RaceManager.RaceState.Idle || bots.Count != 0)
                return false;
            for (int i = 0; i < 4; i++) if (gridPoints[i] == null) return false;
            for (int i = 0; i < orderedCheckpoints.Length; i++) if (orderedCheckpoints[i] == null) return false;

            Unsubscribe();
            race = raceManager;
            humans = humanRoster;
            grid = (Transform[])gridPoints.Clone();
            checkpoints = new Transform[orderedCheckpoints.Length];
            for (int i = 0; i < orderedCheckpoints.Length; i++) checkpoints[i] = orderedCheckpoints[i].transform;
            drivableMask = roadMask;
            visualStyle = visualStyleController;
            configured = true;
            LastRosterError = null;
            if (isActiveAndEnabled) Subscribe();
            return true;
        }

        /// <summary>Rebinds retained AI setup state to a newly selected idle-session track.</summary>
        public bool TryReconfigureTrack(Transform[] gridPoints, RaceCheckpoint[] orderedCheckpoints,
            LayerMask roadMask)
        {
            if (!configured || race.CurrentState != RaceManager.RaceState.Idle || bots.Count != 0 ||
                gridPoints == null || gridPoints.Length < LocalPlayerRules.MaximumPlayers ||
                orderedCheckpoints == null || orderedCheckpoints.Length != race.CheckpointCount)
                return false;
            for (int i = 0; i < LocalPlayerRules.MaximumPlayers; i++) if (gridPoints[i] == null) return false;
            for (int i = 0; i < orderedCheckpoints.Length; i++) if (orderedCheckpoints[i] == null) return false;
            grid = (Transform[])gridPoints.Clone();
            checkpoints = new Transform[orderedCheckpoints.Length];
            for (int i = 0; i < orderedCheckpoints.Length; i++) checkpoints[i] = orderedCheckpoints[i].transform;
            drivableMask = roadMask;
            LastRosterError = null;
            return true;
        }

        private void OnEnable()
        {
            if (configured) Subscribe();
        }

        private void Subscribe()
        {
            if (subscribed || race == null || humans == null) return;
            race.RaceStateChanged += OnRaceStateChanged;
            humans.PlayerJoined += OnHumanRosterChanged;
            humans.PlayerLeft += OnHumanLeft;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (race != null) race.RaceStateChanged -= OnRaceStateChanged;
            if (humans != null)
            {
                humans.PlayerJoined -= OnHumanRosterChanged;
                humans.PlayerLeft -= OnHumanLeft;
            }
            subscribed = false;
        }

        private void Update()
        {
            if (!configured) return;
            if (race.RaceRunning && Time.timeScale <= 0f)
            {
                for (int i = 0; i < drivers.Count; i++)
                    if (drivers[i] != null) drivers[i].SetDrivingEnabled(false);
                return;
            }
            if (race.CurrentState != RaceManager.RaceState.Idle || humans.Players.Count == 0 ||
                bots.Count != 0)
                return;

            LocalPlayer owner = humans.Players[0];
            InputDevice device = owner.Input?.PairedDevice;
            bool cycle = device is Keyboard keyboard && keyboard.bKey.wasPressedThisFrame ||
                         device is Gamepad pad && pad.leftStickButton.wasPressedThisFrame;
            if (cycle) TryCycleSelectedBotCount();
        }

        private void FixedUpdate()
        {
            bool canDrive = configured && race.RaceRunning && Time.timeScale > 0f;
            for (int i = 0; i < drivers.Count; i++)
            {
                AIDriverController driver = drivers[i];
                if (driver == null) continue;
                driver.SetDrivingEnabled(canDrive);
                if (canDrive) driver.Tick(Time.fixedDeltaTime);
            }
        }

        public bool TrySetSelectedBotCount(int count)
        {
            if (!configured || race.CurrentState != RaceManager.RaceState.Idle || bots.Count != 0 ||
                humans.Players.Count == 0 || count < 0 || count > MaximumSelectableBots)
                return false;
            if (selectedBotCount == count) return true;
            selectedBotCount = count;
            LastRosterError = null;
            SelectedBotCountChanged?.Invoke(selectedBotCount);
            return true;
        }

        public bool TryCycleSelectedBotCount()
        {
            if (!configured || humans.Players.Count == 0 || MaximumSelectableBots < 0) return false;
            int next = (selectedBotCount + 1) % (MaximumSelectableBots + 1);
            return TrySetSelectedBotCount(next);
        }

        /// <summary>Builds and registers the selected non-human roster immediately before countdown.</summary>
        public bool PrepareRaceRoster()
        {
            LastRosterError = null;
            if (!configured || race.CurrentState != RaceManager.RaceState.Idle || humans.Players.Count == 0)
            {
                LastRosterError = "AI ROSTER IS NOT READY";
                return false;
            }

            int targetCount = Mathf.Min(selectedBotCount, MaximumSelectableBots);
            if (bots.Count == targetCount)
            {
                ResetBotsToGrid();
                return true;
            }
            if (bots.Count != 0) ReleaseBotsForLobby();

            for (int botIndex = 0; botIndex < targetCount; botIndex++)
            {
                int gridIndex = FindFreeGridIndex();
                if (gridIndex < 0 || !TrySpawnBot(botIndex, gridIndex))
                {
                    LastRosterError ??= "AN AI CAR COULD NOT BE PREPARED — TRY AGAIN";
                    ReleaseBotsForLobby();
                    return false;
                }
            }
            ActiveBotCountChanged?.Invoke(bots.Count);
            return true;
        }

        /// <summary>Releases every bot registration and generated object while retaining setup selection.</summary>
        public bool ReleaseBotsForLobby()
        {
            if (race != null && race.CurrentState != RaceManager.RaceState.Idle) return false;
            bool hadBots = bots.Count > 0;
            for (int i = bots.Count - 1; i >= 0; i--)
            {
                BotEntry bot = bots[i];
                PublishBotPresentationRemoving(new AIVehiclePresentationHandle(bot.BotIndex,
                    bot.Vehicle, bot.Recovery, bot.ImpactEmitter));
                if (bot.VehicleObject != null && race != null) race.UnregisterRacer(bot.VehicleObject);
                UnregisterRenderers(bot.Renderers);
                if (bot.VehicleObject != null)
                {
                    bot.VehicleObject.SetActive(false);
                    DestroyOwnedObject(bot.VehicleObject);
                }
            }
            bots.Clear();
            drivers.Clear();
            if (hadBots) ActiveBotCountChanged?.Invoke(0);
            return true;
        }

        private bool TrySpawnBot(int botIndex, int gridIndex)
        {
            Pose pose = new(grid[gridIndex].position, grid[gridIndex].rotation);
            GameObject car = null;
            Renderer[] renderers = Array.Empty<Renderer>();
            bool racerRegistered = false;
            try
            {
                VehicleFamilyId family = VehicleFamilyCatalog.Wrap(VehicleFamilyCatalog.DefaultId, botIndex + 1);
                car = PrototypeVehicleFactory.Create(pose, family);
                car.name = $"AIVehicle_{botIndex + 1}";
                VehicleController vehicle = car.GetComponent<VehicleController>();
                vehicle.SetInputEnabled(false);
                vehicle.TrySelectHandlingModel(HandlingModelId.A);
                VehicleRecovery recovery = car.AddComponent<VehicleRecovery>();
                recovery.ConfigureGroundMask(drivableMask);
                recovery.Initialize(pose, race);
                VehicleImpactAudioEmitter impactEmitter = car.AddComponent<VehicleImpactAudioEmitter>();
                impactEmitter.ConfigureWorldOnly(LocalPlayerRules.MaximumPlayers + botIndex, 0f);

                string racerId = $"bot-{botIndex + 1}";
                RacerState racer = race.RegisterRacer(racerId, car, $"CPU {botIndex + 1}", false);
                if (racer == null) throw new InvalidOperationException("The authoritative race rejected the bot.");
                racerRegistered = true;

                AIDriverController driver = car.AddComponent<AIDriverController>();
                float laneOffset = AIDrivingPolicy.CalculateSpawnLaneOffset(pose.position,
                    checkpoints[0].position, checkpoints[0].right);
                driver.Initialize(race, racer, checkpoints, recovery, drivableMask, laneOffset);
                renderers = car.GetComponentsInChildren<Renderer>(true);
                RegisterRenderers(renderers);
                ApplyBotColor(car, BotColors[botIndex % BotColors.Length]);
                BotEntry entry = new()
                {
                    BotIndex = botIndex,
                    GridIndex = gridIndex,
                    VehicleObject = car,
                    Vehicle = vehicle,
                    Driver = driver,
                    Renderers = renderers,
                    Recovery = recovery,
                    ImpactEmitter = impactEmitter
                };
                bots.Add(entry);
                drivers.Add(driver);
                PublishBotPresentationAdded(new AIVehiclePresentationHandle(botIndex, vehicle, recovery,
                    impactEmitter));
                return true;
            }
            catch (Exception exception)
            {
                LastRosterError = $"AI CAR {botIndex + 1} FAILED: {exception.Message}";
                UnregisterRenderers(renderers);
                if (racerRegistered && car != null) race.UnregisterRacer(car);
                if (car != null)
                {
                    car.SetActive(false);
                    DestroyOwnedObject(car);
                }
                return false;
            }
        }

        private int FindFreeGridIndex()
        {
            for (int gridIndex = 0; gridIndex < 4; gridIndex++)
            {
                bool occupied = false;
                for (int humanIndex = 0; humanIndex < humans.Players.Count; humanIndex++)
                    if (humans.Players[humanIndex].PlayerIndex == gridIndex) occupied = true;
                for (int botIndex = 0; botIndex < bots.Count; botIndex++)
                    if (bots[botIndex].GridIndex == gridIndex) occupied = true;
                if (!occupied) return gridIndex;
            }
            return -1;
        }

        private void OnHumanRosterChanged(LocalPlayer player) => ClampSelectionToCapacity();
        private void OnHumanLeft(int playerIndex) => ClampSelectionToCapacity();

        private void ClampSelectionToCapacity()
        {
            int clamped = Mathf.Min(selectedBotCount, MaximumSelectableBots);
            if (clamped == selectedBotCount) return;
            selectedBotCount = clamped;
            SelectedBotCountChanged?.Invoke(selectedBotCount);
        }

        private void OnRaceStateChanged(RaceManager.RaceState state)
        {
            bool racing = state == RaceManager.RaceState.Racing;
            for (int i = 0; i < drivers.Count; i++)
                if (drivers[i] != null) drivers[i].SetDrivingEnabled(racing && Time.timeScale > 0f);
            if (state == RaceManager.RaceState.Idle && bots.Count > 0) ResetBotsToGrid();
        }

        private void ResetBotsToGrid()
        {
            for (int i = 0; i < bots.Count; i++)
            {
                BotEntry bot = bots[i];
                Pose pose = new(grid[bot.GridIndex].position, grid[bot.GridIndex].rotation);
                bot.Driver.ResetToGrid(pose);
                bot.Driver.SetDrivingEnabled(false);
            }
        }

        private void RegisterRenderers(IReadOnlyList<Renderer> renderers)
        {
            if (visualStyle == null) return;
            try
            {
                for (int i = 0; i < renderers.Count; i++)
                    visualStyle.RegisterRenderer(renderers[i], renderers[i] is MeshRenderer);
            }
            catch
            {
                for (int i = 0; i < renderers.Count; i++) visualStyle.UnregisterRenderer(renderers[i]);
                throw;
            }
        }

        private void UnregisterRenderers(IReadOnlyList<Renderer> renderers)
        {
            if (visualStyle == null || renderers == null) return;
            for (int i = 0; i < renderers.Count; i++) visualStyle.UnregisterRenderer(renderers[i]);
        }

        private void PublishBotPresentationAdded(AIVehiclePresentationHandle handle)
        {
            try { BotPresentationAdded?.Invoke(handle); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void PublishBotPresentationRemoving(AIVehiclePresentationHandle handle)
        {
            try { BotPresentationRemoving?.Invoke(handle); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private static void ApplyBotColor(GameObject car, Color color)
        {
            VehicleFamilyVisual family = car.GetComponent<VehicleFamilyVisual>();
            if (family == null) return;
            MaterialPropertyBlock properties = new();
            for (int i = 0; i < family.ColorableBodyRenderers.Count; i++)
            {
                Renderer renderer = family.ColorableBodyRenderers[i];
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                renderer.SetPropertyBlock(properties);
                properties.Clear();
            }
        }

        private static void DestroyOwnedObject(GameObject target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (bots.Count == 0) return;
            if (race != null && race.CurrentState != RaceManager.RaceState.Idle) race.ResetRace();
            ReleaseBotsForLobby();
        }
    }
}
