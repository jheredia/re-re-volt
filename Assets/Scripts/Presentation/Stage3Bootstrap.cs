using Core;
using AudioPresentation;
using AI;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Vehicle;
using Weapons;

namespace Presentation
{
    /// <summary>Composes the local and private online front end at runtime.</summary>
    [DisallowMultipleComponent]
    public sealed class Stage3Bootstrap : MonoBehaviour
    {
        public const string FrontEndSceneName = "FrontEnd";
        public const string PrototypeSceneName = "SampleScene";

        public static bool IsRuntimeEntryScene(string sceneName) =>
            sceneName == FrontEndSceneName || sceneName == PrototypeSceneName;
        private static readonly Color[] PlayerColors =
        {
            new(.95f, .2f, .12f),
            new(.1f, .52f, .95f),
            new(.98f, .72f, .08f),
            new(.3f, .86f, .4f)
        };

        private ITrackRuntime circuit;
        private string activeTrackId;
        private string activeTrackRevision;
        private bool initialized;
        private bool trackLoadPending;
        private CancellationTokenSource trackLoadCancellation;

        public RaceManager RaceManager { get; private set; }
        public VehicleManager VehicleManager { get; private set; }
        public AIManager AIManager { get; private set; }
        public Stage3RacePresentation Presentation { get; private set; }
        public InGameTrackEditorController TrackEditor { get; private set; }
        public WeaponManager WeaponManager { get; private set; }
        public HandlingBakeOffCoordinator HandlingBakeOff { get; private set; }
        public RcAudioPresentation AudioPresentation { get; private set; }
        public Stage9BVfxPresentation VfxPresentation { get; private set; }
        public VisualStyleController VisualStyle { get; private set; }
        public DemoFlowPreferences DemoPreferences { get; private set; }
        public TrackSession TrackSession { get; private set; }
        public GameObject CircuitRoot => circuit?.Root;
        public Transform[] Grid => circuit?.Grid;
        public RaceCheckpoint[] Checkpoints => circuit?.Checkpoints;
        public bool TrackLoadPending => trackLoadPending;
        public string ActiveTrackId => activeTrackId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapRuntimeEntryScene()
        {
            if (!IsRuntimeEntryScene(SceneManager.GetActiveScene().name) ||
                FindAnyObjectByType<Stage3Bootstrap>() != null)
                return;

            GameObject bootstrapObject = new GameObject("Stage3_RuntimeBootstrap");
            bootstrapObject.AddComponent<Stage3Bootstrap>().InitializeNow();
        }

        /// <summary>Public for focused PlayMode integration tests; normal play uses the runtime hook.</summary>
        public bool InitializeNow()
        {
            if (initialized) return true;

            VisualStyleController[] existingStyles = GetComponents<VisualStyleController>();
            if (existingStyles.Length > 1)
            {
                Debug.LogError("[Stage3] Runtime bootstrap requires exactly one VisualStyleController; duplicate components were found.", this);
                return false;
            }
            VisualStyle = existingStyles.Length == 1
                ? existingStyles[0]
                : gameObject.AddComponent<VisualStyleController>();
            Time.timeScale = 1f;
            DisableLegacyPrototypeObjects();
            RaceManager = FindAnyObjectByType<RaceManager>();
            if (RaceManager == null) RaceManager = CreateChild<RaceManager>("RaceManager");
            VehicleManager = FindAnyObjectByType<VehicleManager>();
            if (VehicleManager == null) VehicleManager = CreateChild<VehicleManager>("VehicleManager");
            Camera lobbyCamera = PrepareLobbyCamera();

            DemoPreferences = new DemoFlowPreferences(new PlayerPrefsLocalPreferenceStore());
            DemoPreferences.Load();
            // The accepted generated circuit remains the initial shell while an authored
            // scene saved in preferences is loaded and validated additively.
            TrackSession = new TrackSession(TrackCatalog.BuildLocalCatalog());
            TrackDefinition initialShell = TrackCatalog.Entries[0];

            if (!RaceManager.ConfigureRace(initialShell.LapCount, initialShell.OrderedCheckpointCount))
            {
                Debug.LogError("[Stage3] RaceManager must be idle and empty before the circuit is composed.", this);
                return false;
            }

            circuit = initialShell.Builder(RaceManager, transform, VisualStyle);
            activeTrackId = initialShell.Id;
            activeTrackRevision = initialShell.ContentRevision;
            if (!VehicleManager.Configure(RaceManager, circuit.Grid, null, lobbyCamera, VisualStyle))
            {
                Debug.LogError("[Stage3] VehicleManager could not accept the generated four-place grid.", this);
                return false;
            }

            VehicleManager.PlayerJoined += ConfigureJoinedPlayer;
            VehicleManager.VehicleFamilyChanged += ApplyPlayerColor;
            AIManager = FindAnyObjectByType<AIManager>();
            if (AIManager == null) AIManager = CreateChild<AIManager>("AIManager");
            if (!AIManager.Configure(RaceManager, VehicleManager, circuit.Grid, circuit.Checkpoints,
                    circuit.DrivableMask, VisualStyle))
            {
                Debug.LogError("[Stage3] AIManager could not accept the generated grid and checkpoint guidance.", this);
                return false;
            }
            Presentation = gameObject.AddComponent<Stage3RacePresentation>();
            Presentation.Configure(RaceManager, VehicleManager, VisualStyle, DriverPortraitAtlas.Runtime,
                DemoPreferences, AIManager, TrackSession, TryCycleTrack,
                () => !trackLoadPending && circuit != null &&
                      string.Equals(TrackSession.SelectedTrackId, activeTrackId, StringComparison.Ordinal) &&
                      string.Equals(TrackSession.SelectedTrack.ContentRevision, activeTrackRevision,
                          StringComparison.Ordinal) &&
                      TrackSession.SelectedTrack.IsSceneAuthored == (circuit is SceneAuthoredTrackRuntime));
            TrackEditor = gameObject.AddComponent<InGameTrackEditorController>();
            TrackEditor.Configure(RaceManager, VehicleManager, Presentation, TryPrepareLocalTestDrive,
                RefreshLocalTrackCatalog);
            WeaponManager = FindAnyObjectByType<WeaponManager>();
            if (WeaponManager == null) WeaponManager = CreateChild<WeaponManager>("WeaponManager");
            WeaponManager.Configure(RaceManager, VehicleManager, Presentation, circuit.DrivableMask, VisualStyle);
            WeaponManager.TryReconfigureTrack(circuit.DrivableMask, circuit.PickupPositions);
            VfxPresentation = gameObject.AddComponent<Stage9BVfxPresentation>();
            VfxPresentation.Configure(RaceManager, VehicleManager, AIManager, WeaponManager, VisualStyle,
                DemoPreferences.VfxDensity);
            Presentation.AttachVfxPresentation(VfxPresentation);
            AudioPresentation = gameObject.AddComponent<RcAudioPresentation>();
            AudioPresentation.Configure(RaceManager, VehicleManager, WeaponManager, Presentation,
                circuit.DrivableMask);
            HandlingBakeOff = gameObject.AddComponent<HandlingBakeOffCoordinator>();
            HandlingBakeOff.Configure(RaceManager, VehicleManager);
            ConfigureGameManager();
            initialized = true;
            if (!string.Equals(DemoPreferences.TrackId, activeTrackId, StringComparison.Ordinal))
                TrackSession.TrySelect(DemoPreferences.TrackId);
            if (!string.Equals(TrackSession.SelectedTrackId, activeTrackId, StringComparison.Ordinal) ||
                TrackSession.SelectedTrack.IsSceneAuthored)
                BeginTrackActivation(activeTrackId);
            Debug.Log("[Stage3] Craft-room circuit and controller-first race presentation are ready.", this);
            return true;
        }

        /// <summary>Roster-owned setup command; unavailable previews remain selected but can never start.</summary>
        public bool TryCycleTrack(int direction)
        {
            if (!initialized || trackLoadPending || RaceManager.CurrentState != RaceManager.RaceState.Idle || direction == 0)
                return false;
            string previousSelection = TrackSession.SelectedTrackId;
            if (!TrackSession.TryCycle(direction)) return false;
            if (!TrackSession.CanStart) return true;
            if (string.Equals(activeTrackId, TrackSession.SelectedTrackId, System.StringComparison.Ordinal) &&
                string.Equals(activeTrackRevision, TrackSession.SelectedTrack.ContentRevision,
                    StringComparison.Ordinal) &&
                TrackSession.SelectedTrack.IsSceneAuthored == (circuit is SceneAuthoredTrackRuntime))
            {
                DemoPreferences.SetTrackId(TrackSession.SelectedTrackId);
                return true;
            }
            BeginTrackActivation(previousSelection);
            return true;
        }

        private void RefreshLocalTrackCatalog()
        {
            if (!initialized || trackLoadPending || RaceManager.CurrentState != RaceManager.RaceState.Idle) return;
            string selectedId = TrackSession.SelectedTrackId;
            TrackSession = new TrackSession(TrackCatalog.BuildLocalCatalog(), selectedId);
            Presentation.SetTrackSession(TrackSession);
        }

        private bool TryPrepareLocalTestDrive(InGameTrackDraft draft)
        {
            if (draft == null || trackLoadPending || RaceManager.CurrentState != RaceManager.RaceState.Idle ||
                VehicleManager.Players.Count != 0 || Keyboard.current == null) return false;
            RefreshLocalTrackCatalog();
            if (!TrackSession.TrySelect(TrackCatalog.LocalDraftTrackId(draft.LocalId))) return false;
            if (!VehicleManager.TryJoinKeyboard(Keyboard.current, out _)) return false;
            BeginTrackActivation(activeTrackId);
            return true;
        }

        private void BeginTrackActivation(string previousSelection)
        {
            trackLoadPending = true;
            trackLoadCancellation = new CancellationTokenSource();
            Presentation?.SetTrackSelectionMessage("LOADING SELECTED TRACK — START IS LOCKED");
            _ = ActivateSelectedTrackAsync(previousSelection, trackLoadCancellation);
        }

        private async Task ActivateSelectedTrackAsync(string previousSelection, CancellationTokenSource cancellation)
        {
            TrackDefinition selected = TrackSession.SelectedTrack;
            ITrackRuntime candidate = null;
            bool rulesChanged = false;
            bool committed = false;
            string error = string.Empty;
            try
            {
                if (selected.IsSceneAuthored)
                {
                    if (!RaceManager.TryReconfigureIdleRace(selected.LapCount, selected.OrderedCheckpointCount))
                        throw new InvalidOperationException("Race rules rejected the authored track.");
                    rulesChanged = true;
                    candidate = await SceneAuthoredTrackRuntime.LoadAsync(selected.ScenePath, selected.Id,
                        selected.ContentRevision,
                        RaceManager, VisualStyle, SceneTrackMode.Authoritative, cancellation.Token);
                }
                else candidate = TrackSession.BuildSelected(RaceManager, transform, VisualStyle);

                cancellation.Token.ThrowIfCancellationRequested();
                if (!this) throw new OperationCanceledException();
                if (!IsValidRuntime(candidate, selected))
                    throw new InvalidOperationException("Track runtime contract is incomplete.");
                if (!rulesChanged)
                {
                    if (!RaceManager.TryReconfigureIdleRace(selected.LapCount, selected.OrderedCheckpointCount))
                        throw new InvalidOperationException("Race rules rejected the selected track.");
                    rulesChanged = true;
                }
                if (!VehicleManager.TryReconfigureTrack(candidate.Grid, candidate.DrivableMask) ||
                    !AIManager.TryReconfigureTrack(candidate.Grid, candidate.Checkpoints, candidate.DrivableMask) ||
                    !WeaponManager.TryReconfigureTrack(candidate.DrivableMask, candidate.PickupPositions))
                    throw new InvalidOperationException("Runtime owners rejected the selected track.");
                AudioPresentation.SetTrackSurfaceMask(candidate.DrivableMask);
                ITrackRuntime previousRuntime = circuit;
                circuit = candidate;
                candidate = null;
                activeTrackId = selected.Id;
                activeTrackRevision = selected.ContentRevision;
                DemoPreferences.SetTrackId(selected.Id);
                committed = true;
                ReleaseTrackRuntime(previousRuntime);
                if (previousRuntime is SceneAuthoredTrackRuntime sceneRuntime && sceneRuntime.UnloadOperation != null)
                    while (!sceneRuntime.UnloadOperation.isDone) await Awaitable.NextFrameAsync();
            }
            catch (OperationCanceledException)
            {
                error = "TRACK LOAD CANCELED — PREVIOUS TRACK IS ACTIVE";
            }
            catch (Exception exception)
            {
                error = $"TRACK COULD NOT LOAD — {exception.Message}";
                if (this) Debug.LogError($"[Stage9C] Track '{selected.Id}' could not be activated: {exception.Message}", this);
            }
            if (candidate != null)
            {
                ReleaseTrackRuntime(candidate);
                if (candidate is SceneAuthoredTrackRuntime failedScene && failedScene.UnloadOperation != null)
                    while (!failedScene.UnloadOperation.isDone) await Awaitable.NextFrameAsync();
            }
            if (this && !committed)
            {
                if (rulesChanged && TrackSession.TryGetDefinition(previousSelection, out TrackDefinition previous))
                    RaceManager.TryReconfigureIdleRace(previous.LapCount, previous.OrderedCheckpointCount);
                VehicleManager.TryReconfigureTrack(circuit.Grid, circuit.DrivableMask);
                AIManager.TryReconfigureTrack(circuit.Grid, circuit.Checkpoints, circuit.DrivableMask);
                WeaponManager.TryReconfigureTrack(circuit.DrivableMask, circuit.PickupPositions);
                AudioPresentation.SetTrackSurfaceMask(circuit.DrivableMask);
                TrackSession.TrySelect(previousSelection);
                DemoPreferences.SetTrackId(previousSelection);
            }
            if (this)
            {
                trackLoadPending = false;
                if (ReferenceEquals(trackLoadCancellation, cancellation)) trackLoadCancellation = null;
                Presentation.SetTrackSelectionMessage(error);
            }
            cancellation.Dispose();
        }

        private static bool IsValidRuntime(ITrackRuntime runtime, TrackDefinition definition)
        {
            if (runtime?.Root == null || runtime.Grid == null || runtime.Grid.Length < 4 ||
                runtime.Checkpoints == null || runtime.Checkpoints.Length != definition.OrderedCheckpointCount ||
                runtime.PickupPositions == null || runtime.PickupPositions.Count != 4)
                return false;
            for (int i = 0; i < 4; i++) if (runtime.Grid[i] == null) return false;
            for (int i = 0; i < runtime.Checkpoints.Length; i++) if (runtime.Checkpoints[i] == null) return false;
            return true;
        }

        private static void ReleaseTrackRuntime(ITrackRuntime runtime)
        {
            if (runtime == null) return;
            GameObject root = runtime.Root;
            if (root != null) root.SetActive(false);
            runtime.Dispose();
            // New track runtimes may own and clear their root during Dispose. The accepted workshop runtime
            // leaves root destruction to this composition boundary, so preserve that fallback without double-destroying.
            if (runtime.Root == null || root == null) return;
            if (Application.isPlaying) Destroy(root);
            else DestroyImmediate(root);
        }

        private T CreateChild<T>(string objectName) where T : Component
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            return child.AddComponent<T>();
        }

        private Camera PrepareLobbyCamera()
        {
            Camera lobby = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (lobby == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                lobby = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            lobby.enabled = true;
            lobby.clearFlags = CameraClearFlags.SolidColor;
            lobby.backgroundColor = new Color(.045f, .065f, .07f);
            lobby.fieldOfView = 54f;
            lobby.nearClipPlane = .1f;
            lobby.farClipPlane = 200f;
            lobby.transform.position = new Vector3(0f, 45f, -39f);
            lobby.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, 1.5f) - lobby.transform.position, Vector3.up);

            AudioListener lobbyListener = lobby.GetComponent<AudioListener>();
            if (lobbyListener == null) lobbyListener = lobby.gameObject.AddComponent<AudioListener>();
            lobbyListener.enabled = true;

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
            for (int i = 0; i < cameras.Length; i++)
                if (cameras[i] != lobby) cameras[i].enabled = false;
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i] != lobbyListener) listeners[i].enabled = false;
            return lobby;
        }

        private void DisableLegacyPrototypeObjects()
        {
            VehicleController[] demoVehicles = FindObjectsByType<VehicleController>(FindObjectsInactive.Include);
            for (int i = 0; i < demoVehicles.Length; i++)
            {
                GameObject vehicle = demoVehicles[i].gameObject;
                if (!vehicle.scene.IsValid()) continue;
                vehicle.SetActive(false);
                Destroy(vehicle);
            }

            GameObject legacyPlane = GameObject.Find("Plane");
            if (legacyPlane != null && legacyPlane.transform.parent == null)
            {
                legacyPlane.SetActive(false);
                Destroy(legacyPlane);
            }
        }

        private void ConfigureJoinedPlayer(LocalPlayer player)
        {
            player.Recovery.ConfigureGroundMask(circuit.DrivableMask);
            ApplyPlayerColor(player);
        }

        private static void ApplyPlayerColor(LocalPlayer player)
        {
            if (player == null || player.PlayerIndex < 0 || player.PlayerIndex >= PlayerColors.Length) return;
            VehicleFamilyVisual familyVisual = player.FamilyVisual;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            if (familyVisual != null && familyVisual.ColorableBodyRenderers.Count > 0)
            {
                for (int i = 0; i < familyVisual.ColorableBodyRenderers.Count; i++)
                    ApplyPlayerColor(familyVisual.ColorableBodyRenderers[i], PlayerColors[player.PlayerIndex], properties);
                return;
            }

            // Authored and legacy prefabs may predate VehicleFamilyVisual. Preserve their established Body seam.
            Renderer body = player.Vehicle.transform.Find("Body")?.GetComponent<Renderer>();
            if (body != null) ApplyPlayerColor(body, PlayerColors[player.PlayerIndex], properties);
        }

        private static void ApplyPlayerColor(Renderer renderer, Color color, MaterialPropertyBlock properties)
        {
            properties.Clear();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        private void ConfigureGameManager()
        {
            GameManager manager = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
            if (manager == null)
            {
                GameObject managerObject = new GameObject("GameManager");
                manager = managerObject.AddComponent<GameManager>();
            }
            manager.RaceManager = RaceManager;
            manager.VehicleManager = VehicleManager;
            manager.AIManager = AIManager;
            manager.WeaponManager = WeaponManager;
            manager.UIManager = null;
            manager.ReturnToMainMenu();
        }

        private void OnDestroy()
        {
            trackLoadCancellation?.Cancel();
            if (VehicleManager != null)
            {
                VehicleManager.PlayerJoined -= ConfigureJoinedPlayer;
                VehicleManager.VehicleFamilyChanged -= ApplyPlayerColor;
            }
            ReleaseTrackRuntime(circuit);
        }
    }
}
