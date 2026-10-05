using System;
using System.Collections.Generic;
using System.Text;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vehicle;

namespace Presentation
{
    /// <summary>Owned-player lobby, split HUD, pause, disconnect, and results presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class Stage3RacePresentation : MonoBehaviour
    {
        private enum PauseReason
        {
            None,
            Manual,
            Disconnect
        }

        private readonly Dictionary<int, TextMeshProUGUI> hudLabels = new();
        private readonly Dictionary<int, string> weaponLabels = new();
        private RaceManager race;
        private VehicleManager vehicles;
        private VisualStyleController visualStyle;
        private Canvas canvas;
        private GameObject lobbyPanel;
        private TextMeshProUGUI lobbyPlayers;
        private GameObject hudRoot;
        private TextMeshProUGUI countdownLabel;
        private GameObject modalPanel;
        private TextMeshProUGUI modalHeading;
        private TextMeshProUGUI modalBody;
        private TextMeshProUGUI modalFooter;
        private PauseReason pauseReason;
        private int pauseInputFrame = -1;
        private float nextHudRefresh;
        private float countdownHideAt = -1f;
        private bool configured;
        private bool subscribed;

        public int HudCount => hudLabels.Count;
        public bool LobbyVisible => lobbyPanel != null && lobbyPanel.activeSelf;
        public bool ModalVisible => modalPanel != null && modalPanel.activeSelf;
        public bool IsPaused => pauseReason != PauseReason.None;
        public Canvas PresentationCanvas => canvas;

        /// <summary>Stage 4 integration point for the existing single-slot weapon inventory.</summary>
        public void SetWeaponSlot(int playerIndex, string weaponName)
        {
            if (string.IsNullOrWhiteSpace(weaponName)) weaponLabels.Remove(playerIndex);
            else weaponLabels[playerIndex] = weaponName.Trim().ToUpperInvariant();
            RefreshHudText();
        }

        public void Configure(RaceManager raceManager, VehicleManager vehicleManager,
            VisualStyleController visualStyleController = null)
        {
            if (raceManager == null) throw new ArgumentNullException(nameof(raceManager));
            if (vehicleManager == null) throw new ArgumentNullException(nameof(vehicleManager));
            if (configured) Unsubscribe();

            race = raceManager;
            vehicles = vehicleManager;
            visualStyle = visualStyleController;
            BuildInterface();
            configured = true;
            Subscribe();
            RefreshPlayerPresentation();
            ApplyRaceState(race.CurrentState);
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
            if (AnyPlayerDisconnected()) return;
            if (race.CurrentState == RaceManager.RaceState.Idle) StartRaceFromLobby();
            else if (race.CurrentState == RaceManager.RaceState.Finished) StartRematch();
        }

        private void OnBackRequested(LocalPlayer player)
        {
            if (race.CurrentState == RaceManager.RaceState.Finished) ResetToLobby();
        }

        private void OnHandlingModelChanged(LocalPlayer player)
        {
            RefreshLobbyText();
            RefreshHudText();
        }

        private void OnVehicleFamilyChanged(LocalPlayer player)
        {
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
            RefreshLobbyText();
            if (pauseReason == PauseReason.Manual) ShowManualPauseModal();
        }

        private void StartRaceFromLobby()
        {
            if (vehicles.Players.Count == 0 || AnyPlayerDisconnected()) return;
            vehicles.AllowControllerJoining = false;
            vehicles.SetAllVehiclesEnabled(false);
            race.StartRace();
        }

        private void StartRematch()
        {
            pauseReason = PauseReason.None;
            Time.timeScale = 1f;
            race.ResetRace();
            StartRaceFromLobby();
        }

        private void ResetToLobby()
        {
            pauseReason = PauseReason.None;
            Time.timeScale = 1f;
            race.ResetRace();
        }

        private void OnPlayerJoined(LocalPlayer player)
        {
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
            ShowModal("RACE PAUSED", "The workshop clock has stopped." + styleLine,
                "ESC / MENU / START   RESUME");
        }

        private void OnControllerConnectionChanged(LocalPlayer player)
        {
            if (player.IsConnected)
            {
                if (!AnyPlayerDisconnected())
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
                    vehicles.AllowControllerJoining = true;
                    vehicles.SetAllVehiclesEnabled(true);
                    lobbyPanel.SetActive(true);
                    hudRoot.SetActive(false);
                    countdownLabel.gameObject.SetActive(false);
                    if (!AnyPlayerDisconnected()) HideModal();
                    RefreshLobbyText();
                    break;
                case RaceManager.RaceState.Countdown:
                    vehicles.AllowControllerJoining = false;
                    lobbyPanel.SetActive(false);
                    hudRoot.SetActive(true);
                    HideModal();
                    break;
                case RaceManager.RaceState.Racing:
                    vehicles.AllowControllerJoining = false;
                    lobbyPanel.SetActive(false);
                    hudRoot.SetActive(true);
                    if (pauseReason == PauseReason.None) vehicles.SetAllVehiclesEnabled(true);
                    break;
                case RaceManager.RaceState.Finished:
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
            RefreshHudText();
            StringBuilder results = new StringBuilder(160);
            for (int i = 0; i < standings.Count; i++)
            {
                RacerState racer = standings[i];
                results.Append(i + 1).Append("  ").Append(racer.DisplayName.ToUpperInvariant())
                    .Append("      ").Append(FormatTime(racer.FinishTime)).AppendLine();
            }
            ShowModal("FINAL BOARD", results.ToString(), "ENTER / A   REMATCH     ESC / B   LOBBY");
        }

        private void RefreshPlayerPresentation()
        {
            if (hudRoot == null) return;
            foreach (TextMeshProUGUI label in hudLabels.Values)
                if (label != null) Destroy(label.transform.parent.parent.gameObject);
            hudLabels.Clear();

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
                badgeRect.sizeDelta = new Vector2(390f, 92f);
                TextMeshProUGUI label = CreateText("RaceData", badge.transform, 28f, Color.white,
                    TextAlignmentOptions.TopLeft, FontStyles.Bold);
                RectTransform textRect = label.rectTransform;
                Stretch(textRect);
                textRect.offsetMin = new Vector2(16f, 10f);
                textRect.offsetMax = new Vector2(-12f, -9f);
                hudLabels[player.PlayerIndex] = label;
            }
            RefreshHudText();
            RefreshLobbyText();
        }

        private void RefreshHudText()
        {
            if (vehicles == null) return;
            int total = vehicles.Players.Count;
            for (int i = 0; i < total; i++)
            {
                LocalPlayer player = vehicles.Players[i];
                if (!hudLabels.TryGetValue(player.PlayerIndex, out TextMeshProUGUI label)) continue;
                RacerState racer = player.Racer;
                string status = player.IsConnected ? string.Empty : "   INPUT LOST";
                string weapon = weaponLabels.TryGetValue(player.PlayerIndex, out string weaponName)
                    ? weaponName
                    : "--";
                label.text = $"P{player.PlayerIndex + 1}   {HandlingModelCatalog.GetPublicLabel(player.Vehicle.SelectedHandlingModel)}   " +
                             $"{GetVehicleFamilyLabel(player)}   " +
                             $"{racer.CurrentPosition}/{total}   LAP {racer.CurrentLap}/{race.TotalLaps}{status}\n" +
                             $"{FormatTime(race.ElapsedTime)}   WPN [{weapon}]";
            }
        }

        private void RefreshLobbyText()
        {
            if (lobbyPlayers == null || vehicles == null) return;
            StringBuilder builder = new StringBuilder(240);
            if (vehicles.Players.Count == 0)
            {
                builder.AppendLine("NO DRIVERS PAIRED");
                builder.AppendLine("ENTER: JOIN KEYBOARD   MENU / START: JOIN GAMEPAD");
            }
            else
            {
                for (int i = 0; i < vehicles.Players.Count; i++)
                {
                    LocalPlayer player = vehicles.Players[i];
                    builder.Append("P").Append(player.PlayerIndex + 1).Append("  ")
                        .Append(player.Input.IsKeyboardPlayer ? "KEYBOARD  " : "GAMEPAD  ")
                        .Append(HandlingModelCatalog.GetPublicLabel(player.Vehicle.SelectedHandlingModel)).Append("  ")
                        .Append(player.IsConnected ? "READY  " : "RECONNECT DEVICE  ")
                        .Append(GetVehicleFamilyLabel(player)).AppendLine();
                }
                builder.AppendLine();
                if (AnyPlayerDisconnected()) builder.Append("RECONNECT BEFORE STARTING");
                else builder.Append("ENTER / A / CROSS   START 3-LAP RACE");
            }
            builder.AppendLine().AppendLine();
            if (visualStyle != null)
                builder.Append("VISUAL STYLE  ").Append(GetVisualStyleLabel(visualStyle.Mode))
                    .AppendLine("  [V / R3]");
            builder.Append("LOBBY CAR: UP/DOWN OR W/S   GAMEPAD DPAD UP/DOWN\n" +
                           "LOBBY MODEL: LEFT/RIGHT OR Q/E   GAMEPAD DPAD OR LB/RB\n" +
                           "WASD / ARROWS DRIVE   SHIFT BRAKE/DRIFT\nSPACE FIRE   R RECOVER   ESC PAUSE / LEAVE LOBBY\n" +
                           "RT/LT DRIVE   STICK STEER   A BRAKE   X FIRE\nY RECOVER   MENU PAUSE   VIEW LEAVE LOBBY");
            lobbyPlayers.text = builder.ToString();
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

            TextMeshProUGUI title = CreateText("Title", lobbyPanel.transform, 62f, new Color(.98f, .7f, .12f),
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            title.text = "AFTER-HOURS RC TEST RUN";
            title.rectTransform.anchorMin = new Vector2(0f, .76f);
            title.rectTransform.anchorMax = new Vector2(1f, .98f);
            title.rectTransform.offsetMin = new Vector2(55f, 0f);
            title.rectTransform.offsetMax = new Vector2(-40f, 0f);

            TextMeshProUGUI subtitle = CreateText("Subtitle", lobbyPanel.transform, 25f, new Color(.72f, .84f, .82f),
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            subtitle.text = "WORKBENCH CIRCUIT  /  3 LAPS  /  1–4 LOCAL DRIVERS";
            subtitle.rectTransform.anchorMin = new Vector2(0f, .68f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, .78f);
            subtitle.rectTransform.offsetMin = new Vector2(58f, 0f);
            subtitle.rectTransform.offsetMax = new Vector2(-40f, 0f);

            lobbyPlayers = CreateText("PlayersAndControls", lobbyPanel.transform, 29f, Color.white,
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            lobbyPlayers.rectTransform.anchorMin = new Vector2(0f, .08f);
            lobbyPlayers.rectTransform.anchorMax = new Vector2(1f, .66f);
            lobbyPlayers.rectTransform.offsetMin = new Vector2(58f, 10f);
            lobbyPlayers.rectTransform.offsetMax = new Vector2(-45f, -10f);

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

        private void OnDestroy()
        {
            Unsubscribe();
            if (pauseReason != PauseReason.None) Time.timeScale = 1f;
        }
    }
}
