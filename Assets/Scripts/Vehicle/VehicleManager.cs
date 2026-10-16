using System;
using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using Presentation;
using AudioPresentation;

namespace Vehicle
{
    /// <summary>Owns the local roster, device pairings, registered cars, and split-screen cameras.</summary>
    public class VehicleManager : MonoBehaviour
    {
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private GameObject playerVehiclePrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Camera lobbyCamera;
        [SerializeField] private bool allowControllerJoining = true;

        private readonly List<LocalPlayer> players = new();
        private readonly List<VehicleController> allVehicles = new();
        private IReadOnlyList<LocalPlayer> playersView;
        private IReadOnlyList<VehicleController> vehiclesView;
        private AudioListener lobbyListener;
        private bool lobbyCameraWasEnabled;
        private bool lobbyListenerWasEnabled;
        private bool gameplaySuspended;
        private bool subscribed;
        private VisualStyleController visualStyle;
        private PresentationResponseLevel cameraMotionLevel = PresentationResponseLevel.Full;
        private PresentationResponseLevel vehicleMotionLevel = PresentationResponseLevel.Full;

        public IReadOnlyList<LocalPlayer> Players => playersView ??= players.AsReadOnly();
        public IReadOnlyList<VehicleController> AllVehicles => vehiclesView ??= allVehicles.AsReadOnly();
        public VehicleController PlayerVehicle => players.Count > 0 ? players[0].Vehicle : null;
        public bool AllowControllerJoining { get => allowControllerJoining; set => allowControllerJoining = value; }
        public PresentationResponseLevel CameraMotionLevel => cameraMotionLevel;
        public PresentationResponseLevel VehicleMotionLevel => vehicleMotionLevel;
        public event Action<LocalPlayer> PlayerJoined;
        public event Action<int> PlayerLeft;
        public event Action<LocalPlayer> ControllerConnectionChanged;
        public event Action<LocalPlayer> FireRequested;
        public event Action<LocalPlayer> PauseRequested;
        public event Action<LocalPlayer> ConfirmRequested;
        public event Action<LocalPlayer> BackRequested;
        public event Action<LocalPlayer> HandlingModelChanged;
        public event Action<LocalPlayer> VehicleFamilyChanged;
        public event Action<LocalPlayer> VisualStyleCycleRequested;

        private void Awake()
        {
            if (raceManager == null) raceManager = FindFirstObjectByType<RaceManager>();
            if (lobbyCamera == null) lobbyCamera = Camera.main;
        }

        private void OnEnable() => Subscribe();

        /// <summary>Configure before joining. Null prefab uses the code-built prototype car.</summary>
        public bool Configure(RaceManager race, Transform[] grid, GameObject prefab = null, Camera backgroundCamera = null,
            VisualStyleController visualStyleController = null)
        {
            if (players.Count != 0 || race == null || race.CurrentState != RaceManager.RaceState.Idle) return false;
            Unsubscribe();
            raceManager = race;
            spawnPoints = grid == null ? null : (Transform[])grid.Clone();
            playerVehiclePrefab = prefab;
            // A null override keeps the camera discovered during Awake (or a prior explicit configuration).
            // Losing that ownership leaves its listener enabled when the first player listener takes over.
            if (backgroundCamera != null) lobbyCamera = backgroundCamera;
            else if (lobbyCamera == null) lobbyCamera = Camera.main;
            visualStyle = visualStyleController;
            if (isActiveAndEnabled) Subscribe();
            return true;
        }

        private void Subscribe()
        {
            if (raceManager == null || subscribed) return;
            raceManager.RaceStateChanged += OnRaceStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (raceManager != null && subscribed) raceManager.RaceStateChanged -= OnRaceStateChanged;
            subscribed = false;
        }

        private void Update()
        {
            for (int i = players.Count - 1; i >= 0; i--)
            {
                // Connection listeners may remove lobby players through TryLeavePlayer.
                if (i >= players.Count) continue;
                LocalPlayer player = players[i];
                bool connected = player.IsConnected;
                if (connected == player.WasConnected) continue;
                player.WasConnected = connected;
                if (!connected) player.Vehicle.SetInputEnabled(false);
                if (!connected) player.PresentationResponse?.ClearResponse();
                player.PresentationResponse?.SetSuspended(!connected || gameplaySuspended);
                ControllerConnectionChanged?.Invoke(player);
            }

            if (!allowControllerJoining || raceManager == null || raceManager.CurrentState != RaceManager.RaceState.Idle) return;
            foreach (Gamepad pad in Gamepad.all)
                if (!InputUser.FindUserPairedToDevice(pad).HasValue && pad.startButton.wasPressedThisFrame)
                    TryJoinGamepad(pad, out _);
            foreach (InputDevice device in InputSystem.devices)
                if (device is Keyboard keyboard && keyboard.enterKey.wasPressedThisFrame)
                    TryJoinKeyboard(keyboard, out _);
        }

        public bool TryJoinGamepad(Gamepad pad, out LocalPlayer player) => TryJoinDevice(pad, out player);

        public bool TryJoinKeyboard(Keyboard keyboard, out LocalPlayer player)
        {
            player = null;
            // Retain the keyboard slot during disconnect, just like a gamepad's racer/viewport.
            foreach (LocalPlayer existing in players)
                if (existing.Input.IsKeyboardPlayer) return false;
            return TryJoinDevice(keyboard, out player);
        }

        private bool TryJoinDevice(InputDevice device, out LocalPlayer player)
        {
            player = null;
            if (!isActiveAndEnabled || !allowControllerJoining || raceManager == null ||
                raceManager.CurrentState != RaceManager.RaceState.Idle || device == null || !device.added || !device.enabled ||
                players.Count >= LocalPlayerRules.MaximumPlayers || raceManager.Racers.Count >= LocalPlayerRules.MaximumPlayers ||
                InputUser.FindUserPairedToDevice(device).HasValue) return false;

            // PlayerInput auto-pairing would create a second input authority on the same car.
            if (playerVehiclePrefab != null && playerVehiclePrefab.GetComponentInChildren<PlayerInput>(true) != null)
            {
                Debug.LogError("[VehicleManager] Local car prefabs must not contain PlayerInput; this manager owns pairing.", this);
                return false;
            }

            int index = FindFreeSlot();
            if (index < 0) return false;
            Pose spawn = GetSpawnPose(index);
            GameObject car = playerVehiclePrefab != null
                ? Instantiate(playerVehiclePrefab, spawn.position, spawn.rotation)
                : PrototypeVehicleFactory.Create(spawn);
            car.name = $"PlayerVehicle_{index + 1}";
            VehicleController vehicle = car.GetComponent<VehicleController>();
            if (vehicle == null || !vehicle.IsConfigured)
            {
                Debug.LogError("[VehicleManager] A car needs a VehicleController and four configured wheels.", this);
                Destroy(car);
                return false;
            }
            vehicle.SetInputEnabled(false);
            RacerState racer = raceManager.RegisterRacer($"player-{index + 1}", car, $"Player {index + 1}", true, index);
            if (racer == null)
            {
                Destroy(car);
                return false;
            }

            // The stable slot marker is presentation-only and remains outside family-owned geometry, but it must
            // join the same visual-style registration transaction as the rest of the car so every style mode and
            // release path retains one coherent renderer owner.
            PrototypeVehicleFactory.CreatePlayerIdentityMarker(car, index);

            if (!TryRegisterVehicleRenderers(car, out string registrationError))
            {
                raceManager.UnregisterRacer(car);
                car.SetActive(false);
                Destroy(car);
                Debug.LogError($"[VehicleManager] Car visual-style registration failed and was rolled back: {registrationError}", this);
                return false;
            }

            InputUser user = InputUser.PerformPairingWithDevice(device);
            InputHandler input = car.GetComponent<InputHandler>() ?? car.AddComponent<InputHandler>();
            input.Initialize(user, raceManager, racer);
            input.GameplaySuspended = gameplaySuspended;
            input.enabled = true;
            VehicleRecovery recovery = car.GetComponent<VehicleRecovery>() ?? car.AddComponent<VehicleRecovery>();
            recovery.Initialize(spawn, raceManager);
            VehicleImpactAudioEmitter impactEmitter = car.GetComponent<VehicleImpactAudioEmitter>() ??
                                                     car.AddComponent<VehicleImpactAudioEmitter>();
            impactEmitter.Configure(index, 0f);

            GameObject cameraObject = new GameObject($"PlayerCamera_{index + 1}");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 500f;
            camera.fieldOfView = 65f;
            camera.depth = 10 + index;
            AudioListener listener = cameraObject.AddComponent<AudioListener>();
            listener.enabled = false;
            ChaseCamera chase = cameraObject.AddComponent<ChaseCamera>();
            chase.Initialize(car.transform);
            NearCameraPresentationOcclusion nearCameraOcclusion =
                cameraObject.AddComponent<NearCameraPresentationOcclusion>();
            nearCameraOcclusion.Initialize(car.transform);
            VehiclePresentationResponseOwner response = cameraObject.AddComponent<VehiclePresentationResponseOwner>();
            response.Initialize(index, chase, vehicle, recovery, raceManager, impactEmitter);
            response.SetLevels(cameraMotionLevel, vehicleMotionLevel);
            response.SetSuspended(gameplaySuspended);
            LocalPlayer joined = new LocalPlayer(index, user, vehicle, input, recovery, chase, listener, racer, response);
            input.FireRequested += () => FireRequested?.Invoke(joined);
            input.PauseRequested += () => PauseRequested?.Invoke(joined);
            input.ConfirmRequested += () => ConfirmRequested?.Invoke(joined);
            input.BackRequested += () => BackRequested?.Invoke(joined);
            input.VisualStyleCycleRequested += () => VisualStyleCycleRequested?.Invoke(joined);
            input.HandlingModelCycleRequested += direction => TryCycleHandlingModel(joined.PlayerIndex, direction);
            input.VehicleFamilyCycleRequested += direction => TryCycleVehicleFamily(joined.PlayerIndex, direction);
            input.LeaveRequested += () => TryLeavePlayer(joined.PlayerIndex);
            recovery.Recovered += chase.SnapToTarget;

            if (players.Count == 0) HideLobbyCamera();
            players.Add(joined);
            players.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            allVehicles.Add(vehicle);
            RefreshViewports();
            player = joined;
            PlayerJoined?.Invoke(joined);
            return true;
        }

        public bool TryLeavePlayer(int playerIndex)
        {
            if (raceManager == null || raceManager.CurrentState != RaceManager.RaceState.Idle) return false;
            LocalPlayer player = players.Find(candidate => candidate.PlayerIndex == playerIndex);
            if (player == null || !raceManager.UnregisterRacer(player.Vehicle.gameObject)) return false;
            ReleasePlayer(player);
            RefreshViewports();
            PlayerLeft?.Invoke(playerIndex);
            return true;
        }

        /// <summary>Cycles one player's authoritative vehicle model while the lobby is idle.</summary>
        public bool TryCycleHandlingModel(int playerIndex, int direction)
        {
            if (raceManager == null || raceManager.CurrentState != RaceManager.RaceState.Idle || direction == 0)
                return false;
            LocalPlayer player = players.Find(candidate => candidate.PlayerIndex == playerIndex);
            if (player == null || !player.IsConnected) return false;

            int first = (int)HandlingModelId.A;
            int count = (int)HandlingModelId.D - first + 1;
            int step = direction < 0 ? -1 : 1;
            int current = (int)player.Vehicle.SelectedHandlingModel - first;
            int wrapped = (current + step + count) % count;
            if (!player.Vehicle.TrySelectHandlingModel((HandlingModelId)(first + wrapped))) return false;
            HandlingModelChanged?.Invoke(player);
            return true;
        }

        /// <summary>Replaces only one connected lobby player's generated silhouette.</summary>
        public bool TryCycleVehicleFamily(int playerIndex, int direction)
        {
            if (raceManager == null || raceManager.CurrentState != RaceManager.RaceState.Idle || direction == 0)
                return false;
            for (int i = 0; i < players.Count; i++)
                if (!players[i].IsConnected) return false;
            LocalPlayer player = players.Find(candidate => candidate.PlayerIndex == playerIndex);
            if (player == null || !player.IsConnected || player.FamilyVisual == null) return false;

            VehicleFamilyVisual familyVisual = player.FamilyVisual;
            VehicleFamilyId nextFamily = VehicleFamilyCatalog.Wrap(familyVisual.FamilyId, direction < 0 ? -1 : 1);
            Renderer[] oldSourceRenderers = CopyRenderers(familyVisual.SourceRenderers);
            MaterialPropertyBlock playerProperties = new MaterialPropertyBlock();
            if (familyVisual.ColorableBodyRenderers.Count > 0)
                familyVisual.ColorableBodyRenderers[0].GetPropertyBlock(playerProperties);

            UnregisterRenderers(oldSourceRenderers);
            VehicleFamilyVisualReplacement replacement;
            try
            {
                replacement = PrototypeVehicleFactory.PrepareFamilyVisualReplacement(
                    player.Vehicle.gameObject, nextFamily);
            }
            catch (Exception exception)
            {
                RestoreRendererRegistrations(oldSourceRenderers);
                Debug.LogError($"[VehicleManager] Vehicle-family rebuild failed and was rolled back: " +
                               $"{exception.GetType().Name}: {exception.Message}", this);
                return false;
            }

            if (!TryRegisterRenderers(replacement.NewSourceRenderers, out string registrationError))
            {
                replacement.Dispose();
                RestoreRendererRegistrations(oldSourceRenderers);
                Debug.LogError($"[VehicleManager] Vehicle-family registration failed and was rolled back: " +
                               registrationError, this);
                return false;
            }

            try
            {
                replacement.Commit();
            }
            catch (Exception exception)
            {
                UnregisterRenderers(replacement.NewSourceRenderers);
                replacement.Dispose();
                RestoreRendererRegistrations(oldSourceRenderers);
                Debug.LogError($"[VehicleManager] Vehicle-family commit failed and was rolled back: " +
                               $"{exception.GetType().Name}: {exception.Message}", this);
                return false;
            }

            ApplyPropertyBlock(replacement.NewColorableBodyRenderers, playerProperties);
            VehicleFamilyChanged?.Invoke(player);
            return true;
        }

        /// <summary>Explicit UI action only: replace a lost pad without changing racer identity or progress.</summary>
        public bool TryReplaceDisconnectedController(int playerIndex, Gamepad replacement)
        {
            LocalPlayer player = players.Find(candidate => candidate.PlayerIndex == playerIndex);
            if (player == null || player.Input.IsKeyboardPlayer || player.IsConnected || replacement == null || !replacement.added || !replacement.enabled ||
                InputUser.FindUserPairedToDevice(replacement).HasValue) return false;
            player.User.UnpairDevices();
            InputUser.PerformPairingWithDevice(replacement, player.User);
            player.Input.Initialize(player.User, raceManager, player.Racer);
            player.WasConnected = true;
            player.PresentationResponse?.SetSuspended(gameplaySuspended);
            ControllerConnectionChanged?.Invoke(player);
            return true;
        }

        public void SetAllVehiclesEnabled(bool enabled)
        {
            gameplaySuspended = !enabled;
            foreach (LocalPlayer player in players)
            {
                player.Input.GameplaySuspended = !enabled;
                if (!enabled) player.Vehicle.SetInputEnabled(false);
                player.PresentationResponse?.SetSuspended(!enabled);
            }
        }

        public void ResetPlayersToGrid()
        {
            foreach (LocalPlayer player in players)
            {
                player.Recovery.ResetToGrid(GetSpawnPose(player.PlayerIndex));
                player.PresentationResponse?.ClearResponse();
                player.ChaseCamera.SnapToTarget();
            }
        }

        /// <summary>Explicit settings seam; no duplicate global input polling is introduced for this optional response.</summary>
        public void SetPresentationResponseLevels(PresentationResponseLevel cameraMotion,
            PresentationResponseLevel vehicleMotion)
        {
            if (!System.Enum.IsDefined(typeof(PresentationResponseLevel), cameraMotion) ||
                !System.Enum.IsDefined(typeof(PresentationResponseLevel), vehicleMotion))
                throw new ArgumentOutOfRangeException();
            cameraMotionLevel = cameraMotion;
            vehicleMotionLevel = vehicleMotion;
            foreach (LocalPlayer player in players)
                player.PresentationResponse?.SetLevels(cameraMotionLevel, vehicleMotionLevel);
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < LocalPlayerRules.MaximumPlayers; index++)
            {
                bool occupied = false;
                foreach (RacerState racer in raceManager.Racers)
                    if (racer.PlayerIndex == index || racer.RacerId == $"player-{index + 1}") occupied = true;
                if (!occupied) return index;
            }
            return -1;
        }

        private Pose GetSpawnPose(int index)
        {
            if (spawnPoints != null && index < spawnPoints.Length && spawnPoints[index] != null)
                return new Pose(spawnPoints[index].position, spawnPoints[index].rotation);
            return new Pose(transform.TransformPoint(new Vector3((index % 2) * 2.5f, .7f, -(index / 2) * 3f)), transform.rotation);
        }

        private void OnRaceStateChanged(RaceManager.RaceState state)
        {
            if (state == RaceManager.RaceState.Idle)
            {
                SetAllVehiclesEnabled(true);
                ResetPlayersToGrid();
            }
            if (state != RaceManager.RaceState.Racing)
                foreach (LocalPlayer player in players) player.Vehicle.SetInputEnabled(false);
        }

        private void HideLobbyCamera()
        {
            if (lobbyCamera == null) return;
            lobbyCameraWasEnabled = lobbyCamera.enabled;
            lobbyCamera.enabled = false;
            lobbyListener = lobbyCamera.GetComponent<AudioListener>();
            if (lobbyListener == null) return;
            lobbyListenerWasEnabled = lobbyListener.enabled;
            lobbyListener.enabled = false;
        }

        private void RefreshViewports()
        {
            for (int i = 0; i < players.Count; i++)
            {
                Viewport viewport = LocalPlayerRules.GetViewport(players.Count, i);
                players[i].Camera.rect = new Rect(viewport.X, viewport.Y, viewport.Width, viewport.Height);
                players[i].Listener.enabled = i == 0;
            }
            if (players.Count != 0) return;
            if (lobbyCamera != null) lobbyCamera.enabled = lobbyCameraWasEnabled;
            if (lobbyListener != null) lobbyListener.enabled = lobbyListenerWasEnabled;
        }

        private void ReleasePlayer(LocalPlayer player)
        {
            player.PresentationResponse?.ClearResponse();
            if (player.Input != null) player.Input.enabled = false;
            if (player.User.valid) player.User.UnpairDevicesAndRemoveUser();
            players.Remove(player);
            allVehicles.Remove(player.Vehicle);
            Camera playerCamera = player.Camera;
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(false);
                Destroy(playerCamera.gameObject);
            }
            VehicleController playerVehicle = player.Vehicle;
            if (playerVehicle != null)
            {
                UnregisterVehicleRenderers(playerVehicle.gameObject);
                playerVehicle.gameObject.SetActive(false);
                Destroy(playerVehicle.gameObject);
            }
        }

        private bool TryRegisterVehicleRenderers(GameObject vehicle, out string error)
        {
            error = null;
            if (visualStyle == null || vehicle == null) return true;
            Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>(true);
            try
            {
                for (int i = 0; i < renderers.Length; i++)
                    visualStyle.RegisterRenderer(renderers[i], renderers[i] is MeshRenderer);
                return true;
            }
            catch (Exception exception)
            {
                // Registration is a transaction: remove both completed and partially attempted entries.
                for (int i = 0; i < renderers.Length; i++) visualStyle.UnregisterRenderer(renderers[i]);
                error = $"{exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        private bool TryRegisterRenderers(IReadOnlyList<Renderer> renderers, out string error)
        {
            error = null;
            if (visualStyle == null) return true;
            try
            {
                for (int i = 0; i < renderers.Count; i++)
                    visualStyle.RegisterRenderer(renderers[i], renderers[i] is MeshRenderer);
                return true;
            }
            catch (Exception exception)
            {
                UnregisterRenderers(renderers);
                error = $"{exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        private void RestoreRendererRegistrations(IReadOnlyList<Renderer> renderers)
        {
            if (TryRegisterRenderers(renderers, out string error)) return;
            Debug.LogError($"[VehicleManager] Vehicle-family rollback could not restore visual-style registration: {error}",
                this);
        }

        private void UnregisterRenderers(IReadOnlyList<Renderer> renderers)
        {
            if (visualStyle == null) return;
            for (int i = 0; i < renderers.Count; i++) visualStyle.UnregisterRenderer(renderers[i]);
        }

        private static Renderer[] CopyRenderers(IReadOnlyList<Renderer> renderers)
        {
            Renderer[] copy = new Renderer[renderers.Count];
            for (int i = 0; i < renderers.Count; i++) copy[i] = renderers[i];
            return copy;
        }

        private static void ApplyPropertyBlock(IReadOnlyList<Renderer> renderers, MaterialPropertyBlock properties)
        {
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null) renderers[i].SetPropertyBlock(properties);
        }

        private void UnregisterVehicleRenderers(GameObject vehicle)
        {
            if (visualStyle == null || vehicle == null) return;
            Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) visualStyle.UnregisterRenderer(renderers[i]);
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (players.Count == 0) return;
            // Disabling the roster owner ends its session; no dangling racers or InputUsers survive it.
            if (raceManager != null && raceManager.CurrentState != RaceManager.RaceState.Idle) raceManager.ResetRace();
            while (players.Count > 0)
            {
                LocalPlayer player = players[players.Count - 1];
                VehicleController playerVehicle = player.Vehicle;
                if (raceManager != null && playerVehicle != null)
                    raceManager.UnregisterRacer(playerVehicle.gameObject);
                ReleasePlayer(player);
            }
            RefreshViewports();
        }
    }
}
