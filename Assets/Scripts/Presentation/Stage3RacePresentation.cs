using System;
using System.Collections.Generic;
using System.Text;
using AI;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vehicle;

namespace Presentation
{
    /// <summary>Owned-player lobby, split HUD, pause, disconnect, and results presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class Stage3RacePresentation : MonoBehaviour
    {
        public enum DemoFlowPage
        {
            Title,
            Setup,
            Race,
            Results
        }

        private sealed class WeaponHudIdentity
        {
            internal GameObject BoltIcon;
            internal GameObject OilIcon;
            internal TextMeshProUGUI Label;
        }

        private static readonly Color BoltHudColor = new(1f, .43f, .04f, 1f);
        private static readonly Color OilHudColor = new(.72f, .28f, 1f, 1f);
        private static readonly Color EmptyHudColor = new(.42f, .48f, .48f, 1f);

        private enum PauseReason
        {
            None,
            Manual,
            Disconnect
        }

        private readonly Dictionary<int, TextMeshProUGUI> hudLabels = new();
        private readonly Dictionary<int, RawImage> hudPortraits = new();
        private readonly List<RawImage> lobbyPortraits = new();
        private readonly Dictionary<int, string> weaponLabels = new();
        private readonly Dictionary<int, WeaponHudIdentity> weaponIdentities = new();
        private RaceManager race;
        private VehicleManager vehicles;
        private AIManager ai;
        private VisualStyleController visualStyle;
        private Canvas canvas;
        private GameObject lobbyPanel;
        private GameObject lobbyPortraitRoot;
        private TextMeshProUGUI lobbyTitle;
        private TextMeshProUGUI lobbySubtitle;
        private TextMeshProUGUI lobbyPlayers;
        private GameObject hudRoot;
        private TextMeshProUGUI countdownLabel;
        private GameObject modalPanel;
        private TextMeshProUGUI modalHeading;
        private TextMeshProUGUI modalBody;
        private TextMeshProUGUI modalFooter;
        private PauseReason pauseReason;
        private int pauseInputFrame = -1;
        private int selectedSettingsRow;
        private float nextHudRefresh;
        private float countdownHideAt = -1f;
        private string latestResultsBody;
        private bool configured;
        private bool subscribed;
        private DriverPortraitAtlas portraitAtlas;
        private DemoFlowPreferences preferences;
        private Stage9BVfxPresentation vfxPresentation;
        private bool applyingPreferences;

        public int HudCount => hudLabels.Count;
        public int HudPortraitCount => hudPortraits.Count;
        public int LobbyPortraitCount => lobbyPortraits.Count;
        public bool PortraitAtlasAvailable =>
            portraitAtlas?.GetTexture(DriverPortraitSheet.Founders) != null;
        public bool LobbyVisible => lobbyPanel != null && lobbyPanel.activeSelf;
        public bool ModalVisible => modalPanel != null && modalPanel.activeSelf;
        public bool IsPaused => pauseReason != PauseReason.None;
        public Canvas PresentationCanvas => canvas;
        public DemoFlowPage CurrentPage { get; private set; } = DemoFlowPage.Title;
        public string TitleText => lobbyTitle != null ? lobbyTitle.text : string.Empty;
        public string LobbyText => lobbyPlayers != null ? lobbyPlayers.text : string.Empty;
        public string ModalHeadingText => modalHeading != null ? modalHeading.text : string.Empty;
        public string ModalBodyText => modalBody != null ? modalBody.text : string.Empty;
        public string ModalFooterText => modalFooter != null ? modalFooter.text : string.Empty;
        public string PreferenceRecoveryMessage => preferences?.RecoveryMessage ?? string.Empty;
        public int SelectedBotCount => ai?.SelectedBotCount ?? 0;
        public PresentationResponseLevel CameraMotionSetting => preferences?.CameraMotion ?? PresentationResponseLevel.Full;
        public PresentationResponseLevel VehicleMotionSetting => preferences?.VehicleMotion ?? PresentationResponseLevel.Full;
        public VfxDensityLevel VfxDensitySetting => preferences?.VfxDensity ?? VfxDensityLevel.Full;

        /// <summary>Stage 4 integration point for the existing single-slot weapon inventory.</summary>
        public void SetWeaponSlot(int playerIndex, string weaponName)
        {
            if (string.IsNullOrWhiteSpace(weaponName)) weaponLabels.Remove(playerIndex);
            else weaponLabels[playerIndex] = weaponName.Trim().ToUpperInvariant();
            RefreshWeaponIdentity(playerIndex);
            RefreshHudText();
        }

        public void Configure(RaceManager raceManager, VehicleManager vehicleManager,
            VisualStyleController visualStyleController = null, DriverPortraitAtlas portraitAtlasSource = null,
            DemoFlowPreferences preferenceSource = null, AIManager aiManager = null)
        {
            if (raceManager == null) throw new ArgumentNullException(nameof(raceManager));
            if (vehicleManager == null) throw new ArgumentNullException(nameof(vehicleManager));
            if (configured) Unsubscribe();

            race = raceManager;
            vehicles = vehicleManager;
            visualStyle = visualStyleController;
            ai = aiManager;
            portraitAtlas = portraitAtlasSource;
            preferences = preferenceSource;
            ApplySavedVisualStyle();
            ApplyPresentationResponseSettings();
            BuildInterface();
            configured = true;
            Subscribe();
            RefreshPlayerPresentation();
            ApplyRaceState(race.CurrentState);
        }

        /// <summary>
        /// Explicit accessibility settings seam shared by the manual pause UI and automated tests.
        /// </summary>
        public bool SetPresentationResponseSettings(PresentationResponseLevel cameraMotion,
            PresentationResponseLevel vehicleMotion)
        {
            if (preferences == null) return false;
            bool saved = preferences.SetPresentationResponseLevels(cameraMotion, vehicleMotion);
            ApplyPresentationResponseSettings();
            RefreshLobbyText();
            if (pauseReason == PauseReason.Manual) ShowManualPauseModal();
            return saved;
        }

        /// <summary>Attaches the optional pooled world-VFX owner after combat composition.</summary>
        public void AttachVfxPresentation(Stage9BVfxPresentation owner)
        {
            vfxPresentation = owner;
            vfxPresentation?.SetDensity(VfxDensitySetting);
            RefreshLobbyText();
            if (pauseReason == PauseReason.Manual) ShowManualPauseModal();
        }

        /// <summary>Explicit settings API; no new InputHandler binding is introduced.</summary>
        public bool SetVfxDensitySetting(VfxDensityLevel level)
        {
            if (preferences == null) return false;
            bool saved = preferences.SetVfxDensity(level);
            vfxPresentation?.SetDensity(VfxDensitySetting);
            RefreshLobbyText();
            if (pauseReason == PauseReason.Manual) ShowManualPauseModal();
            return saved;
        }

        private void ApplyPresentationResponseSettings()
        {
            if (vehicles == null) return;
            vehicles.SetPresentationResponseLevels(CameraMotionSetting, VehicleMotionSetting);
        }

        private void Subscribe()
        {
            if (subscribed) return;
            race.RaceStateChanged += ApplyRaceState;
            race.CountdownChanged += OnCountdownChanged;
            race.RaceCompleted += OnRaceCompleted;
            vehicles.PlayerJoined += OnPlayerJoined;
            vehicles.PlayerLeft += OnPlayerLeft;
            vehicles.PauseRequested += OnPauseRequested;
            vehicles.ConfirmRequested += OnConfirmRequested;
            vehicles.BackRequested += OnBackRequested;
            vehicles.HandlingModelChanged += OnHandlingModelChanged;
            vehicles.VehicleFamilyChanged += OnVehicleFamilyChanged;
            vehicles.VisualStyleCycleRequested += OnVisualStyleCycleRequested;
            vehicles.ControllerConnectionChanged += OnControllerConnectionChanged;
            if (ai != null) ai.SelectedBotCountChanged += OnSelectedBotCountChanged;
            if (visualStyle != null) visualStyle.StyleChanged += OnVisualStyleChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (race != null)
            {
                race.RaceStateChanged -= ApplyRaceState;
                race.CountdownChanged -= OnCountdownChanged;
                race.RaceCompleted -= OnRaceCompleted;
            }
            if (vehicles != null)
            {
                vehicles.PlayerJoined -= OnPlayerJoined;
                vehicles.PlayerLeft -= OnPlayerLeft;
                vehicles.PauseRequested -= OnPauseRequested;
                vehicles.ConfirmRequested -= OnConfirmRequested;
                vehicles.BackRequested -= OnBackRequested;
                vehicles.HandlingModelChanged -= OnHandlingModelChanged;
                vehicles.VehicleFamilyChanged -= OnVehicleFamilyChanged;
                vehicles.VisualStyleCycleRequested -= OnVisualStyleCycleRequested;
                vehicles.ControllerConnectionChanged -= OnControllerConnectionChanged;
            }
            if (visualStyle != null) visualStyle.StyleChanged -= OnVisualStyleChanged;
            if (ai != null) ai.SelectedBotCountChanged -= OnSelectedBotCountChanged;
            subscribed = false;
        }

        private void OnEnable()
        {
            if (!configured) return;
            Subscribe();
            RefreshPlayerPresentation();
            ApplyRaceState(race.CurrentState);
        }

        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (!configured) return;

            ReadPausedSettingsInput();

            if (countdownHideAt > 0f && Time.unscaledTime >= countdownHideAt)
            {
                countdownHideAt = -1f;
                countdownLabel.gameObject.SetActive(false);
            }

            if (Time.unscaledTime >= nextHudRefresh)
            {
                nextHudRefresh = Time.unscaledTime + .1f;
                RefreshHudText();
            }

        }

        private void OnConfirmRequested(LocalPlayer player)
        {
            // Paused settings have one UI-owned input path, even if gameplay Confirm becomes available.
            if (pauseReason == PauseReason.Manual) return;
            if (AnyPlayerDisconnected()) return;
            if (race.CurrentState == RaceManager.RaceState.Idle) StartRaceFromLobby();
            else if (race.CurrentState == RaceManager.RaceState.Finished) StartRematch();
        }

        private void ReadPausedSettingsInput()
        {
            if (pauseReason != PauseReason.Manual || race.CurrentState != RaceManager.RaceState.Racing ||
                preferences == null || AnyPlayerDisconnected()) return;

            for (int i = 0; i < vehicles.Players.Count; i++)
            {
                LocalPlayer player = vehicles.Players[i];
                if (!CanEditPausedSettings(player) || !player.User.valid) continue;
                foreach (InputDevice device in player.User.pairedDevices)
                {
                    if (!device.added || !device.enabled) continue;
                    bool pressed = device is Keyboard keyboard
                        ? keyboard.enterKey.wasPressedThisFrame
                        : device is Gamepad pad && pad.buttonSouth.wasPressedThisFrame;
                    if (!pressed) continue;

                    CycleSelectedPausedSetting();
                    selectedSettingsRow = (selectedSettingsRow + 1) % 3;
                    ShowManualPauseModal();
                }
            }
        }

        private void OnBackRequested(LocalPlayer player)
        {
            if (race.CurrentState == RaceManager.RaceState.Finished) ResetToLobby();
        }

        private bool CanEditPausedSettings(LocalPlayer player)
        {
            if (!isActiveAndEnabled || preferences == null || player == null || !player.IsConnected ||
                race.CurrentState != RaceManager.RaceState.Racing || pauseReason != PauseReason.Manual ||
                AnyPlayerDisconnected()) return false;
            for (int i = 0; i < vehicles.Players.Count; i++)
                if (ReferenceEquals(vehicles.Players[i], player)) return true;
            return false;
        }

        private void CycleSelectedPausedSetting()
        {
            switch (selectedSettingsRow)
            {
                case 0:
                    SetPresentationResponseSettings((PresentationResponseLevel)(((int)CameraMotionSetting + 1) % 3),
                        VehicleMotionSetting);
                    break;
                case 1:
                    SetPresentationResponseSettings(CameraMotionSetting,
                        (PresentationResponseLevel)(((int)VehicleMotionSetting + 1) % 3));
                    break;
                default:
                    SetVfxDensitySetting((VfxDensityLevel)(((int)VfxDensitySetting + 1) % 3));
                    break;
            }
        }

        private void OnHandlingModelChanged(LocalPlayer player)
        {
            SavePlayerPreferences(player);
            RefreshLobbyText();
            RefreshHudText();
        }

        private void OnVehicleFamilyChanged(LocalPlayer player)
        {
            SavePlayerPreferences(player);
            RefreshLobbyText();
            RefreshHudText();
        }

        private void OnVisualStyleCycleRequested(LocalPlayer player)
        {
            if (visualStyle == null) return;
            bool allowed = race.CurrentState == RaceManager.RaceState.Idle ||
                           race.CurrentState == RaceManager.RaceState.Racing && pauseReason == PauseReason.Manual;
            if (!allowed) return;
            visualStyle.CycleMode();
        }

        private void OnVisualStyleChanged(VisualStyleMode mode)
        {
            if (!applyingPreferences && preferences != null) preferences.SetVisualStyle(mode);
            RefreshLobbyText();
            if (pauseReason == PauseReason.Manual) ShowManualPauseModal();
        }

        private void StartRaceFromLobby()
        {
            if (vehicles.Players.Count == 0 || AnyPlayerDisconnected()) return;
            if (ai != null && !ai.PrepareRaceRoster())
            {
                RefreshLobbyText();
                return;
            }
            vehicles.AllowControllerJoining = false;
            vehicles.SetAllVehiclesEnabled(false);
            if (!race.StartRace())
            {
                ai?.ReleaseBotsForLobby();
                vehicles.AllowControllerJoining = true;
                RefreshLobbyText();
            }
        }

        private void StartRematch()
        {
            latestResultsBody = null;
            pauseReason = PauseReason.None;
            Time.timeScale = 1f;
            race.ResetRace();
            StartRaceFromLobby();
        }

        private void ResetToLobby()
        {
            latestResultsBody = null;
            pauseReason = PauseReason.None;
            Time.timeScale = 1f;
            race.ResetRace();
            ai?.ReleaseBotsForLobby();
        }

        private void OnSelectedBotCountChanged(int count) => RefreshLobbyText();

        private void OnPlayerJoined(LocalPlayer player)
        {
            ApplySavedPlayerPreferences(player);
            RefreshPlayerPresentation();
        }

        private void OnPlayerLeft(int playerIndex)
        {
            RefreshPlayerPresentation();
        }

        private void OnPauseRequested(LocalPlayer player)
        {
            if (Time.frameCount == pauseInputFrame || race.CurrentState != RaceManager.RaceState.Racing ||
                pauseReason == PauseReason.Disconnect)
                return;

            pauseInputFrame = Time.frameCount;
            if (pauseReason == PauseReason.Manual)
            {
                pauseReason = PauseReason.None;
                Time.timeScale = 1f;
                vehicles.SetAllVehiclesEnabled(true);
                if (GameManager.Instance != null) GameManager.Instance.ResumeGame();
                HideModal();
            }
            else
            {
                pauseReason = PauseReason.Manual;
                selectedSettingsRow = 0;
                vehicles.SetAllVehiclesEnabled(false);
                if (GameManager.Instance != null) GameManager.Instance.PauseGame();
                else Time.timeScale = 0f;
                ShowManualPauseModal();
            }
        }

        private void ShowManualPauseModal()
        {
            string styleLine = visualStyle == null
                ? string.Empty
                : $"\nVISUAL STYLE  {GetVisualStyleLabel(visualStyle.Mode)}  [V / R3]";
            string recoveryLine = string.IsNullOrEmpty(PreferenceRecoveryMessage)
                ? string.Empty
                : $"\nNOTICE  {PreferenceRecoveryMessage}";
            string responseLine = $"\n{(selectedSettingsRow == 0 ? "> " : "  ")}CAMERA MOTION  {GetResponseLabel(CameraMotionSetting)}" +
                $"\n{(selectedSettingsRow == 1 ? "> " : "  ")}VEHICLE MOTION  {GetResponseLabel(VehicleMotionSetting)}";
            string vfxLine = $"\n{(selectedSettingsRow == 2 ? "> " : "  ")}VFX DENSITY  {GetVfxDensityLabel(VfxDensitySetting)}";
            ShowModal("RACE PAUSED / SETTINGS",
                "DRIVING AND THE WORKSHOP CLOCK ARE SUSPENDED." + styleLine + responseLine + vfxLine + recoveryLine,
                "ENTER / A / CROSS   CYCLE VALUE, THEN NEXT ROW\nVALUES   OFF / REDUCED / FULL\nV / R3   CHANGE DISPLAY STYLE\nESC / MENU / START   RESUME");
        }

        private void OnControllerConnectionChanged(LocalPlayer player)
        {
            if (player.IsConnected)
            {
                if (AnyPlayerDisconnected())
                {
                    ShowDisconnectPrompt();
                    if (race.CurrentState == RaceManager.RaceState.Idle) RefreshLobbyText();
                }
                else
                {
                    if (pauseReason == PauseReason.Disconnect && race.CurrentState == RaceManager.RaceState.Racing)
                    {
                        pauseReason = PauseReason.None;
                        Time.timeScale = 1f;
                        vehicles.SetAllVehiclesEnabled(true);
                        if (GameManager.Instance != null) GameManager.Instance.ResumeGame();
                        HideModal();
                    }
                    else if (race.CurrentState == RaceManager.RaceState.Idle)
                    {
                        pauseReason = PauseReason.None;
                        HideModal();
                        RefreshLobbyText();
                    }
                    else if (race.CurrentState == RaceManager.RaceState.Finished)
                    {
                        pauseReason = PauseReason.None;
                        ShowResultsModal();
                    }
                }
                return;
            }

            if (race.CurrentState == RaceManager.RaceState.Countdown)
            {
                race.ResetRace();
            }
            if (race.CurrentState == RaceManager.RaceState.Racing)
            {
                pauseReason = PauseReason.Disconnect;
                vehicles.SetAllVehiclesEnabled(false);
                if (GameManager.Instance != null) GameManager.Instance.PauseGame();
                else Time.timeScale = 0f;
            }
            ShowDisconnectPrompt();
            RefreshLobbyText();
        }

        private bool AnyPlayerDisconnected()
        {
            for (int i = 0; i < vehicles.Players.Count; i++)
                if (!vehicles.Players[i].IsConnected) return true;
            return false;
        }

        private void ApplyRaceState(RaceManager.RaceState state)
        {
            if (!configured) return;
            switch (state)
            {
                case RaceManager.RaceState.Idle:
                    latestResultsBody = null;
                    CurrentPage = vehicles.Players.Count == 0 ? DemoFlowPage.Title : DemoFlowPage.Setup;
                    vehicles.AllowControllerJoining = true;
                    vehicles.SetAllVehiclesEnabled(true);
                    lobbyPanel.SetActive(true);
                    hudRoot.SetActive(false);
                    countdownLabel.gameObject.SetActive(false);
                    if (!AnyPlayerDisconnected()) HideModal();
                    RefreshLobbyText();
                    break;
                case RaceManager.RaceState.Countdown:
                    CurrentPage = DemoFlowPage.Race;
                    vehicles.AllowControllerJoining = false;
                    lobbyPanel.SetActive(false);
                    hudRoot.SetActive(true);
                    HideModal();
                    break;
                case RaceManager.RaceState.Racing:
                    CurrentPage = DemoFlowPage.Race;
                    vehicles.AllowControllerJoining = false;
                    lobbyPanel.SetActive(false);
                    hudRoot.SetActive(true);
                    if (pauseReason == PauseReason.None) vehicles.SetAllVehiclesEnabled(true);
                    break;
                case RaceManager.RaceState.Finished:
                    CurrentPage = DemoFlowPage.Results;
                    vehicles.AllowControllerJoining = false;
                    vehicles.SetAllVehiclesEnabled(false);
                    Time.timeScale = 0f;
                    countdownLabel.gameObject.SetActive(false);
                    break;
            }
        }

        private void OnCountdownChanged(int number)
        {
            if (number < 0)
            {
                countdownLabel.gameObject.SetActive(false);
                return;
            }
            countdownLabel.gameObject.SetActive(true);
            countdownLabel.text = number > 0 ? number.ToString() : "GO!";
            if (number == 0) countdownHideAt = Time.unscaledTime + .9f;
        }

        private void OnRaceCompleted(IReadOnlyList<RacerState> standings)
        {
            pauseReason = PauseReason.None;
            CurrentPage = DemoFlowPage.Results;
            RefreshHudText();
            StringBuilder results = new StringBuilder(160);
            for (int i = 0; i < standings.Count; i++)
            {
                RacerState racer = standings[i];
                results.Append(i + 1).Append("  ").Append(racer.DisplayName.ToUpperInvariant())
                    .Append("      ").Append(FormatTime(racer.FinishTime)).AppendLine();
            }
            latestResultsBody = results.ToString();
            ShowResultsModal();
        }

        private void ShowResultsModal()
        {
            if (string.IsNullOrEmpty(latestResultsBody)) return;
            ShowModal("FINAL BOARD", latestResultsBody, "ENTER / A   REMATCH     ESC / B   LOBBY");
        }

        private void RefreshPlayerPresentation()
        {
            if (hudRoot == null) return;
            foreach (TextMeshProUGUI label in hudLabels.Values)
                if (label != null) Destroy(label.transform.parent.parent.gameObject);
            hudLabels.Clear();
            hudPortraits.Clear();
            weaponIdentities.Clear();

            for (int displayIndex = 0; displayIndex < vehicles.Players.Count; displayIndex++)
            {
                LocalPlayer player = vehicles.Players[displayIndex];
                Viewport viewport = LocalPlayerRules.GetViewport(vehicles.Players.Count, displayIndex);
                GameObject viewportRoot = CreateUiObject($"Player_{player.PlayerIndex + 1}_HUD", hudRoot.transform);
                RectTransform viewportRect = viewportRoot.GetComponent<RectTransform>();
                viewportRect.anchorMin = new Vector2(viewport.X, viewport.Y);
                viewportRect.anchorMax = new Vector2(viewport.X + viewport.Width, viewport.Y + viewport.Height);
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;

                GameObject badge = CreatePanel("WorkshopLabel", viewportRoot.transform,
                    new Color(.045f, .055f, .055f, .86f));
                RectTransform badgeRect = badge.GetComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(0f, 1f);
                badgeRect.anchorMax = new Vector2(0f, 1f);
                badgeRect.pivot = new Vector2(0f, 1f);
                badgeRect.anchoredPosition = new Vector2(18f, -18f);
                badgeRect.sizeDelta = new Vector2(540f, 108f);

                float textInset = 16f;
                DriverPersonaDefinition persona = DriverPersonaCatalog.GetForPlayerSlot(player.PlayerIndex);
                Texture portraitTexture = GetPortraitTexture(persona);
                if (portraitTexture != null)
                {
                    RawImage portrait = CreatePortrait($"HudPortrait_P{player.PlayerIndex + 1}", badge.transform,
                        portraitTexture, persona.PortraitUv);
                    RectTransform portraitRect = portrait.rectTransform;
                    portraitRect.anchorMin = new Vector2(0f, 1f);
                    portraitRect.anchorMax = new Vector2(0f, 1f);
                    portraitRect.pivot = new Vector2(0f, 1f);
                    portraitRect.anchoredPosition = new Vector2(10f, -10f);
                    portraitRect.sizeDelta = new Vector2(88f, 88f);
                    hudPortraits[player.PlayerIndex] = portrait;
                    textInset = 108f;
                }

                TextMeshProUGUI label = CreateText("RaceData", badge.transform, 22f, Color.white,
                    TextAlignmentOptions.TopLeft, FontStyles.Bold);
                RectTransform textRect = label.rectTransform;
                Stretch(textRect);
                textRect.offsetMin = new Vector2(textInset, 8f);
                textRect.offsetMax = new Vector2(-112f, -9f);
                hudLabels[player.PlayerIndex] = label;
                weaponIdentities[player.PlayerIndex] = CreateWeaponIdentity(badge.transform);
                RefreshWeaponIdentity(player.PlayerIndex);
            }
            RebuildLobbyPortraits();
            RefreshHudText();
            RefreshLobbyText();
        }

        private void RefreshHudText()
        {
            if (vehicles == null) return;
            int total = race?.Racers.Count ?? vehicles.Players.Count;
            for (int i = 0; i < vehicles.Players.Count; i++)
            {
                LocalPlayer player = vehicles.Players[i];
                if (!hudLabels.TryGetValue(player.PlayerIndex, out TextMeshProUGUI label)) continue;
                RacerState racer = player.Racer;
                string status = player.IsConnected ? string.Empty : "   INPUT LOST";
                string weapon = weaponLabels.TryGetValue(player.PlayerIndex, out string weaponName)
                    ? weaponName
                    : "--";
                DriverPersonaDefinition persona = DriverPersonaCatalog.GetForPlayerSlot(player.PlayerIndex);
                label.text = $"P{player.PlayerIndex + 1}   {HandlingModelCatalog.GetPublicLabel(player.Vehicle.SelectedHandlingModel)}   " +
                             $"{GetVehicleFamilyLabel(player)}   " +
                             $"{racer.CurrentPosition}/{total}\n" +
                             $"{persona.Callsign}   LAP {racer.CurrentLap}/{race.TotalLaps}{status}\n" +
                             $"{FormatTime(race.ElapsedTime)}   WPN [{weapon}]";
            }
        }

        private WeaponHudIdentity CreateWeaponIdentity(Transform parent)
        {
            GameObject root = CreatePanel("WeaponIdentity", parent, new Color(.015f, .02f, .02f, .82f));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, .5f);
            rootRect.anchorMax = new Vector2(1f, .5f);
            rootRect.pivot = new Vector2(1f, .5f);
            rootRect.anchoredPosition = new Vector2(-8f, 0f);
            rootRect.sizeDelta = new Vector2(94f, 90f);

            GameObject bolt = CreateUiObject("BoltIcon", root.transform);
            ConfigureIconRoot(bolt.GetComponent<RectTransform>());
            CreateIconPart("BoltUpper", bolt.transform, BoltHudColor,
                new Vector2(8f, 25f), new Vector2(7f, 10f), -28f);
            CreateIconPart("BoltMiddle", bolt.transform, Color.white,
                new Vector2(8f, 26f), new Vector2(-1f, 0f), 32f);
            CreateIconPart("BoltLower", bolt.transform, BoltHudColor,
                new Vector2(8f, 25f), new Vector2(-8f, -11f), -28f);

            GameObject oil = CreateUiObject("OilIcon", root.transform);
            ConfigureIconRoot(oil.GetComponent<RectTransform>());
            CreateIconPart("OilDropTop", oil.transform, OilHudColor,
                new Vector2(18f, 18f), new Vector2(0f, 10f), 45f);
            CreateIconPart("OilDropBody", oil.transform, OilHudColor,
                new Vector2(25f, 25f), new Vector2(0f, -1f), 45f);
            CreateIconPart("OilPuddle", oil.transform, Color.white,
                new Vector2(38f, 6f), new Vector2(0f, -18f), 0f);

            TextMeshProUGUI slotLabel = CreateText("WeaponName", root.transform, 16f, EmptyHudColor,
                TextAlignmentOptions.Center, FontStyles.Bold);
            slotLabel.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform labelRect = slotLabel.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 4f);
            labelRect.sizeDelta = new Vector2(-4f, 20f);

            return new WeaponHudIdentity { BoltIcon = bolt, OilIcon = oil, Label = slotLabel };
        }

        private static void ConfigureIconRoot(RectTransform rect)
        {
            rect.anchorMin = new Vector2(.5f, 1f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -4f);
            rect.sizeDelta = new Vector2(56f, 58f);
        }

        private static void CreateIconPart(string name, Transform parent, Color color,
            Vector2 size, Vector2 position, float rotation)
        {
            GameObject part = CreatePanel(name, parent, color);
            RectTransform rect = part.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void RefreshWeaponIdentity(int playerIndex)
        {
            if (!weaponIdentities.TryGetValue(playerIndex, out WeaponHudIdentity identity)) return;
            bool hasLabel = weaponLabels.TryGetValue(playerIndex, out string weaponName);
            bool bolt = hasLabel && string.Equals(weaponName, "BOLT", StringComparison.Ordinal);
            bool oil = hasLabel && string.Equals(weaponName, "OIL", StringComparison.Ordinal);
            identity.BoltIcon.SetActive(bolt);
            identity.OilIcon.SetActive(oil);
            identity.Label.text = bolt ? "BOLT" : oil ? "OIL" : "EMPTY";
            identity.Label.color = bolt ? BoltHudColor : oil ? OilHudColor : EmptyHudColor;
        }

        private void RefreshLobbyText()
        {
            if (lobbyPlayers == null || vehicles == null) return;
            bool titlePage = vehicles.Players.Count == 0;
            if (race.CurrentState == RaceManager.RaceState.Idle)
                CurrentPage = titlePage ? DemoFlowPage.Title : DemoFlowPage.Setup;
            lobbyTitle.text = titlePage ? "AFTER-HOURS RC CLUB" : "SET UP YOUR TEST RUN";
            lobbySubtitle.text = titlePage
                ? "WORKBENCH CIRCUIT  /  1–4 LOCAL DRIVERS"
                : "CHOOSE EACH DRIVER'S CAR AND HANDLING  /  3 LAPS";
            StringBuilder builder = new StringBuilder(240);
            if (!string.IsNullOrEmpty(PreferenceRecoveryMessage))
                builder.Append("NOTICE: ").AppendLine(PreferenceRecoveryMessage).AppendLine();
            if (titlePage)
            {
                builder.AppendLine("A NEIGHBORHOOD TEST NIGHT IS READY.");
                builder.AppendLine("ENTER   JOIN WITH KEYBOARD");
                builder.AppendLine("MENU / START   JOIN WITH A GAMEPAD");
                builder.AppendLine("ADD UP TO FOUR LOCAL DRIVERS, THEN PICK A CAR AND HANDLING MODEL.");
                lobbyPlayers.text = builder.ToString();
                return;
            }
            else
            {
                for (int i = 0; i < vehicles.Players.Count; i++)
                {
                    LocalPlayer player = vehicles.Players[i];
                    DriverPersonaDefinition persona = DriverPersonaCatalog.GetForPlayerSlot(player.PlayerIndex);
                    if (GetPortraitTexture(persona) != null) builder.Append("    ");
                    builder.Append("P").Append(player.PlayerIndex + 1).Append("  ")
                        .Append(player.Input.IsKeyboardPlayer ? "KEYBOARD  " : "GAMEPAD  ")
                        .Append(HandlingModelCatalog.GetPublicLabel(player.Vehicle.SelectedHandlingModel)).Append("  ")
                        .Append(player.IsConnected ? "READY  " : "RECONNECT DEVICE  ")
                        .Append(GetVehicleFamilyLabel(player)).Append("   ")
                        .Append(persona.Callsign).AppendLine();
                }
                builder.AppendLine();
                if (AnyPlayerDisconnected()) builder.Append("RECONNECT BEFORE STARTING");
                else builder.Append("ENTER / A / CROSS   START 3-LAP RACE");
                if (ai != null)
                {
                    builder.AppendLine().Append("AI TEST CARS  ").Append(ai.SelectedBotCount)
                        .Append(" / ").Append(ai.MaximumSelectableBots)
                        .Append("   [B / L3 CYCLE]");
                    if (!string.IsNullOrEmpty(ai.LastRosterError))
                        builder.AppendLine().Append("NOTICE: ").Append(ai.LastRosterError);
                }
            }
            builder.AppendLine();
            if (visualStyle != null)
                builder.Append("VISUAL STYLE  ").Append(GetVisualStyleLabel(visualStyle.Mode))
                    .AppendLine("  [V / R3]");
            builder.Append("FEEDBACK  CAMERA ").Append(GetResponseLabel(CameraMotionSetting)).Append("   VEHICLE ")
                .Append(GetResponseLabel(VehicleMotionSetting)).Append("   VFX ")
                .Append(GetVfxDensityLabel(VfxDensitySetting)).AppendLine("  [PAUSE MENU]");
            builder.Append("LOBBY CAR: UP/DOWN OR W/S   GAMEPAD DPAD UP/DOWN\n" +
                           "LOBBY MODEL: LEFT/RIGHT OR Q/E   GAMEPAD DPAD OR LB/RB\n" +
                           "AI TEST CARS: B / L3   STYLE: V / R3   LEAVE LOBBY: ESC / VIEW\n" +
                           "DRIVE: WASD/ARROWS, SHIFT BRAKE   GAMEPAD RT/LT, STICK, A BRAKE\n" +
                           "FIRE: SPACE / X   RECOVER: R / Y   PAUSE: ESC / MENU");
            lobbyPlayers.text = builder.ToString();
        }

        private static string GetResponseLabel(PresentationResponseLevel level) => level switch
        {
            PresentationResponseLevel.Off => "OFF",
            PresentationResponseLevel.Reduced => "REDUCED",
            _ => "FULL"
        };

        private static string GetVfxDensityLabel(VfxDensityLevel level) => level switch
        {
            VfxDensityLevel.Off => "OFF",
            VfxDensityLevel.Reduced => "REDUCED",
            _ => "FULL"
        };

        private void RebuildLobbyPortraits()
        {
            for (int i = 0; i < lobbyPortraits.Count; i++)
                if (lobbyPortraits[i] != null) Destroy(lobbyPortraits[i].gameObject);
            lobbyPortraits.Clear();
            if (portraitAtlas == null || lobbyPortraitRoot == null || vehicles == null) return;

            for (int i = 0; i < vehicles.Players.Count; i++)
            {
                LocalPlayer player = vehicles.Players[i];
                DriverPersonaDefinition persona = DriverPersonaCatalog.GetForPlayerSlot(player.PlayerIndex);
                Texture portraitTexture = GetPortraitTexture(persona);
                if (portraitTexture == null) continue;
                RawImage portrait = CreatePortrait($"LobbyPortrait_P{player.PlayerIndex + 1}",
                    lobbyPortraitRoot.transform, portraitTexture, persona.PortraitUv);
                RectTransform portraitRect = portrait.rectTransform;
                portraitRect.anchorMin = new Vector2(0f, 1f);
                portraitRect.anchorMax = new Vector2(0f, 1f);
                portraitRect.pivot = new Vector2(0f, 1f);
                portraitRect.anchoredPosition = new Vector2(0f, -i * 35f);
                portraitRect.sizeDelta = new Vector2(32f, 32f);
                lobbyPortraits.Add(portrait);
            }
        }

        private Texture GetPortraitTexture(DriverPersonaDefinition persona)
        {
            return persona == null || portraitAtlas == null
                ? null
                : portraitAtlas.GetTexture(persona.PortraitSheet);
        }

        private static string GetVehicleFamilyLabel(LocalPlayer player)
        {
            return player?.FamilyVisual == null
                ? "VEHICLE ?"
                : VehicleFamilyCatalog.GetPublicLabel(player.FamilyVisual.FamilyId);
        }

        private static string GetVisualStyleLabel(VisualStyleMode mode) => mode switch
        {
            VisualStyleMode.Clean3D => "CLEAN 3D",
            VisualStyleMode.InkLite => "INK LITE",
            VisualStyleMode.FullInk => "FULL INK",
            _ => mode.ToString().ToUpperInvariant()
        };

        private void ShowDisconnectPrompt()
        {
            StringBuilder lost = new StringBuilder();
            for (int i = 0; i < vehicles.Players.Count; i++)
                if (!vehicles.Players[i].IsConnected)
                    lost.Append("PLAYER ").Append(vehicles.Players[i].PlayerIndex + 1).AppendLine(" INPUT DEVICE DISCONNECTED");
            ShowModal("INPUT CHECK", lost.ToString(), "RECONNECT THE SAME DEVICE TO CONTINUE");
        }

        private void ShowModal(string heading, string body, string footer)
        {
            modalHeading.text = heading;
            modalBody.text = body;
            modalFooter.text = footer;
            modalPanel.SetActive(true);
        }

        private void HideModal()
        {
            if (modalPanel != null) modalPanel.SetActive(false);
        }

        private void BuildInterface()
        {
            if (canvas != null) return;
            GameObject canvasObject = new GameObject("Stage3_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            hudRoot = CreateUiObject("SplitHUD", canvas.transform);
            Stretch(hudRoot.GetComponent<RectTransform>());

            lobbyPanel = CreatePanel("Lobby", canvas.transform, new Color(.035f, .045f, .045f, .92f));
            RectTransform lobbyRect = lobbyPanel.GetComponent<RectTransform>();
            lobbyRect.anchorMin = new Vector2(.18f, .17f);
            lobbyRect.anchorMax = new Vector2(.82f, .83f);
            lobbyRect.offsetMin = Vector2.zero;
            lobbyRect.offsetMax = Vector2.zero;
            CreateTapeStrip(lobbyPanel.transform, new Color(.96f, .64f, .08f, 1f));

            lobbyTitle = CreateText("Title", lobbyPanel.transform, 62f, new Color(.98f, .7f, .12f),
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            lobbyTitle.text = "AFTER-HOURS RC CLUB";
            lobbyTitle.rectTransform.anchorMin = new Vector2(0f, .76f);
            lobbyTitle.rectTransform.anchorMax = new Vector2(1f, .98f);
            lobbyTitle.rectTransform.offsetMin = new Vector2(55f, 0f);
            lobbyTitle.rectTransform.offsetMax = new Vector2(-40f, 0f);

            lobbySubtitle = CreateText("Subtitle", lobbyPanel.transform, 25f, new Color(.72f, .84f, .82f),
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            lobbySubtitle.text = "WORKBENCH CIRCUIT  /  1–4 LOCAL DRIVERS";
            lobbySubtitle.rectTransform.anchorMin = new Vector2(0f, .68f);
            lobbySubtitle.rectTransform.anchorMax = new Vector2(1f, .78f);
            lobbySubtitle.rectTransform.offsetMin = new Vector2(58f, 0f);
            lobbySubtitle.rectTransform.offsetMax = new Vector2(-40f, 0f);

            lobbyPlayers = CreateText("PlayersAndControls", lobbyPanel.transform, 29f, Color.white,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            lobbyPlayers.enableAutoSizing = true;
            lobbyPlayers.fontSizeMin = 22f;
            lobbyPlayers.fontSizeMax = 29f;
            lobbyPlayers.rectTransform.anchorMin = new Vector2(0f, .06f);
            lobbyPlayers.rectTransform.anchorMax = new Vector2(1f, .66f);
            lobbyPlayers.rectTransform.offsetMin = new Vector2(58f, 10f);
            lobbyPlayers.rectTransform.offsetMax = new Vector2(-45f, -10f);

            lobbyPortraitRoot = CreateUiObject("LobbyPortraits", lobbyPanel.transform);
            RectTransform lobbyPortraitRect = lobbyPortraitRoot.GetComponent<RectTransform>();
            lobbyPortraitRect.anchorMin = new Vector2(0f, .06f);
            lobbyPortraitRect.anchorMax = new Vector2(1f, .66f);
            lobbyPortraitRect.offsetMin = new Vector2(58f, 10f);
            lobbyPortraitRect.offsetMax = new Vector2(-45f, -10f);

            countdownLabel = CreateText("Countdown", canvas.transform, 142f, new Color(.98f, .7f, .12f),
                TextAlignmentOptions.Center, FontStyles.Bold);
            countdownLabel.rectTransform.anchorMin = new Vector2(.3f, .32f);
            countdownLabel.rectTransform.anchorMax = new Vector2(.7f, .75f);
            countdownLabel.rectTransform.offsetMin = Vector2.zero;
            countdownLabel.rectTransform.offsetMax = Vector2.zero;
            countdownLabel.gameObject.SetActive(false);

            modalPanel = CreatePanel("Modal", canvas.transform, new Color(.035f, .045f, .045f, .96f));
            RectTransform modalRect = modalPanel.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(.25f, .25f);
            modalRect.anchorMax = new Vector2(.75f, .75f);
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;
            CreateTapeStrip(modalPanel.transform, new Color(.76f, .12f, .1f, 1f));

            modalHeading = CreateText("Heading", modalPanel.transform, 54f, new Color(.98f, .7f, .12f),
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            modalHeading.rectTransform.anchorMin = new Vector2(0f, .72f);
            modalHeading.rectTransform.anchorMax = new Vector2(1f, .98f);
            modalHeading.rectTransform.offsetMin = new Vector2(48f, 0f);
            modalHeading.rectTransform.offsetMax = new Vector2(-35f, 0f);
            modalBody = CreateText("Body", modalPanel.transform, 32f, Color.white,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            modalBody.rectTransform.anchorMin = new Vector2(0f, .22f);
            modalBody.rectTransform.anchorMax = new Vector2(1f, .72f);
            modalBody.rectTransform.offsetMin = new Vector2(48f, 0f);
            modalBody.rectTransform.offsetMax = new Vector2(-35f, 0f);
            modalFooter = CreateText("Footer", modalPanel.transform, 24f, new Color(.72f, .84f, .82f),
                TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            modalFooter.rectTransform.anchorMin = new Vector2(0f, .04f);
            modalFooter.rectTransform.anchorMax = new Vector2(1f, .23f);
            modalFooter.rectTransform.offsetMin = new Vector2(48f, 0f);
            modalFooter.rectTransform.offsetMax = new Vector2(-35f, 0f);
            modalPanel.SetActive(false);
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            return item;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float size, Color color,
            TextAlignmentOptions alignment, FontStyles style)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            item.transform.SetParent(parent, false);
            TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static RawImage CreatePortrait(string name, Transform parent, Texture texture, Rect uvRect)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            item.transform.SetParent(parent, false);
            RawImage portrait = item.GetComponent<RawImage>();
            portrait.texture = texture;
            portrait.uvRect = uvRect;
            portrait.color = Color.white;
            portrait.raycastTarget = false;
            return portrait;
        }

        private static void CreateTapeStrip(Transform parent, Color color)
        {
            GameObject strip = CreatePanel("TapeStrip", parent, color);
            RectTransform rect = strip.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 14f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int minutes = Mathf.FloorToInt(seconds / 60f);
            return $"{minutes:00}:{seconds % 60f:00.00}";
        }

        private void ApplySavedVisualStyle()
        {
            if (preferences == null || visualStyle == null || visualStyle.Mode == preferences.VisualStyle) return;
            applyingPreferences = true;
            try
            {
                visualStyle.SetMode(preferences.VisualStyle);
            }
            catch (InvalidOperationException)
            {
                preferences.ReportRecovery("SAVED DISPLAY STYLE WAS UNAVAILABLE — CLEAN 3D IS ACTIVE");
                if (visualStyle.Mode != VisualStyleMode.Clean3D) visualStyle.SetMode(VisualStyleMode.Clean3D);
            }
            finally
            {
                applyingPreferences = false;
            }
        }

        private void ApplySavedPlayerPreferences(LocalPlayer player)
        {
            if (preferences == null || player == null) return;
            applyingPreferences = true;
            try
            {
                HandlingModelId desiredHandling = preferences.GetHandlingModel(player.PlayerIndex);
                for (int attempt = 0; attempt < 4 && player.Vehicle.SelectedHandlingModel != desiredHandling; attempt++)
                    if (!vehicles.TryCycleHandlingModel(player.PlayerIndex, 1)) break;

                VehicleFamilyId desiredFamily = preferences.GetVehicleFamily(player.PlayerIndex);
                for (int attempt = 0; attempt < VehicleFamilyCatalog.Count &&
                     player.FamilyVisual != null && player.FamilyVisual.FamilyId != desiredFamily; attempt++)
                    if (!vehicles.TryCycleVehicleFamily(player.PlayerIndex, 1)) break;

                if (player.Vehicle.SelectedHandlingModel != desiredHandling || player.FamilyVisual == null ||
                    player.FamilyVisual.FamilyId != desiredFamily)
                    preferences.ReportRecovery("A SAVED PLAYER SETUP COULD NOT BE RESTORED — CURRENT VALUES ARE ACTIVE");
            }
            finally
            {
                applyingPreferences = false;
            }
        }

        private void SavePlayerPreferences(LocalPlayer player)
        {
            if (applyingPreferences || preferences == null || player?.FamilyVisual == null) return;
            preferences.SetPlayerSelection(player.PlayerIndex, player.Vehicle.SelectedHandlingModel,
                player.FamilyVisual.FamilyId);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (pauseReason != PauseReason.None) Time.timeScale = 1f;
        }
    }
}
