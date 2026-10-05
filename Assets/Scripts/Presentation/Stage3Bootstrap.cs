using Core;
using AudioPresentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vehicle;
using Weapons;

namespace Presentation
{
    /// <summary>Composes the Stage 3 vertical slice when the enabled prototype scene launches.</summary>
    [DisallowMultipleComponent]
    public sealed class Stage3Bootstrap : MonoBehaviour
    {
        public const string PrototypeSceneName = "SampleScene";
        private static readonly Color[] PlayerColors =
        {
            new(.95f, .2f, .12f),
            new(.1f, .52f, .95f),
            new(.98f, .72f, .08f),
            new(.3f, .86f, .4f)
        };

        private PrototypeCircuitBuilder.Result circuit;
        private bool initialized;

        public RaceManager RaceManager { get; private set; }
        public VehicleManager VehicleManager { get; private set; }
        public Stage3RacePresentation Presentation { get; private set; }
        public WeaponManager WeaponManager { get; private set; }
        public HandlingBakeOffCoordinator HandlingBakeOff { get; private set; }
        public RcAudioPresentation AudioPresentation { get; private set; }
        public VisualStyleController VisualStyle { get; private set; }
        public GameObject CircuitRoot => circuit?.Root;
        public Transform[] Grid => circuit?.Grid;
        public RaceCheckpoint[] Checkpoints => circuit?.Checkpoints;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapEnabledPrototypeScene()
        {
            if (SceneManager.GetActiveScene().name != PrototypeSceneName || FindAnyObjectByType<Stage3Bootstrap>() != null)
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

            if (!RaceManager.ConfigureRace(3, PrototypeCircuitBuilder.OrderedCheckpointCount))
            {
                Debug.LogError("[Stage3] RaceManager must be idle and empty before the circuit is composed.", this);
                return false;
            }

            circuit = PrototypeCircuitBuilder.Build(RaceManager, transform, VisualStyle);
            if (!VehicleManager.Configure(RaceManager, circuit.Grid, null, lobbyCamera, VisualStyle))
            {
                Debug.LogError("[Stage3] VehicleManager could not accept the generated four-place grid.", this);
                return false;
            }

            VehicleManager.PlayerJoined += ConfigureJoinedPlayer;
            VehicleManager.VehicleFamilyChanged += ApplyPlayerColor;
            Presentation = gameObject.AddComponent<Stage3RacePresentation>();
            Presentation.Configure(RaceManager, VehicleManager, VisualStyle);
            WeaponManager = FindAnyObjectByType<WeaponManager>();
            if (WeaponManager == null) WeaponManager = CreateChild<WeaponManager>("WeaponManager");
            WeaponManager.Configure(RaceManager, VehicleManager, Presentation, circuit.DrivableMask, VisualStyle);
            AudioPresentation = gameObject.AddComponent<RcAudioPresentation>();
            AudioPresentation.Configure(RaceManager, VehicleManager, WeaponManager);
            HandlingBakeOff = gameObject.AddComponent<HandlingBakeOffCoordinator>();
            HandlingBakeOff.Configure(RaceManager, VehicleManager);
            ConfigureGameManager();
            initialized = true;
            Debug.Log("[Stage3] Craft-room circuit and controller-first race presentation are ready.", this);
            return true;
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
            manager.WeaponManager = WeaponManager;
            manager.UIManager = null;
            manager.ReturnToMainMenu();
        }

        private void OnDestroy()
        {
            if (VehicleManager != null)
            {
                VehicleManager.PlayerJoined -= ConfigureJoinedPlayer;
                VehicleManager.VehicleFamilyChanged -= ApplyPlayerColor;
            }
            circuit?.Dispose();
        }
    }
}
