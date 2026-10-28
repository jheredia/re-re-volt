using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Core;
using Presentation;
using ReReVolt.Online.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReReVolt.Online.Unity
{
    /// <summary>Opt-in, controller-navigable lobby. Online world simulation remains a separate integration.</summary>
    public sealed class OnlineLobbyEntry : MonoBehaviour
    {
        private Stage3Bootstrap bootstrap;
        private RelayLobbyConnection connection;
        private OnlineLobbySession lobby;
        private OnlineLobbyProtocol protocol;
        private GameObject panel;
        private TextMeshProUGUI text;
        private GameObject titleHint;
        private Keyboard typingKeyboard;
        private bool visible;
        private bool enteringCode;
        private bool priorJoining;
        private bool disposed;
        private string code = string.Empty;
        private string lastText;
        private string failure;
        private int selection;
        private int localSlot;
        private int codeCharacter;
        private double connectStarted;
        private double nextRefresh;
        private OnlineLobbySession.LobbyState menuState = (OnlineLobbySession.LobbyState)(-1);
        private LobbySnapshot menuSnapshot;
        private int menuSlot = -1;
        private const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private readonly List<string> labels = new List<string>(10);
        private readonly List<Action> actions = new List<Action>(10);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name != Stage3Bootstrap.PrototypeSceneName ||
                FindAnyObjectByType<OnlineLobbyEntry>() != null) return;
            new GameObject("Online Private Lobby").AddComponent<OnlineLobbyEntry>();
        }

        private void Awake()
        {
            connection = new RelayLobbyConnection();
            lobby = new OnlineLobbySession(connection);
        }

        private void Update()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<Stage3Bootstrap>();
            if (bootstrap == null || bootstrap.VehicleManager == null || bootstrap.RaceManager == null) return;
            bool emptyTitle = bootstrap.RaceManager.CurrentState == RaceManager.RaceState.Idle &&
                bootstrap.VehicleManager.Players.Count == 0;
            if (titleHint == null) BuildTitleHint();
            titleHint.SetActive(!visible && emptyTitle);
            Keyboard keyboard = Keyboard.current;
            bool open = keyboard != null && keyboard.f8Key.wasPressedThisFrame;
            bool up = keyboard != null && keyboard.upArrowKey.wasPressedThisFrame;
            bool down = keyboard != null && keyboard.downArrowKey.wasPressedThisFrame;
            bool confirm = keyboard != null && keyboard.enterKey.wasPressedThisFrame;
            bool back = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool append = false;
            bool delete = keyboard != null && keyboard.backspaceKey.wasPressedThisFrame;
            foreach (Gamepad pad in Gamepad.all)
            {
                open |= pad.selectButton.wasPressedThisFrame;
                up |= pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame;
                down |= pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame;
                confirm |= pad.buttonSouth.wasPressedThisFrame;
                back |= pad.buttonEast.wasPressedThisFrame;
                append |= pad.buttonWest.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame;
                delete |= pad.dpad.left.wasPressedThisFrame;
            }
            if (!visible)
            {
                if (open && bootstrap.RaceManager.CurrentState == RaceManager.RaceState.Idle &&
                    bootstrap.VehicleManager.Players.Count == 0) Open();
                return;
            }
            if (lobby.State == OnlineLobbySession.LobbyState.Connecting &&
                Time.realtimeSinceStartupAsDouble - connectStarted >= 30) lobby.Cancel();
            if (protocol != null)
            {
                protocol.Tick(Time.realtimeSinceStartupAsDouble);
                if (protocol.TerminalMessage != null)
                {
                    failure = protocol.TerminalMessage;
                    DisposeProtocol();
                    _ = lobby.ConnectionLostAsync();
                }
            }
            if (back)
            {
                if (enteringCode) enteringCode = false;
                else if (lobby.State == OnlineLobbySession.LobbyState.Joined) Leave();
                else { lobby.Cancel(); Close(); }
                selection = 0;
            }
            else if (enteringCode)
            {
                if (up) codeCharacter = (codeCharacter + 1) % CodeAlphabet.Length;
                if (down) codeCharacter = (codeCharacter + CodeAlphabet.Length - 1) % CodeAlphabet.Length;
                if (append && code.Length < 12) code += CodeAlphabet[codeCharacter];
                if (delete && code.Length > 0) code = code.Substring(0, code.Length - 1);
                if (confirm) { enteringCode = false; Connect(false); }
            }
            else
            {
                BuildActions();
                if (actions.Count > 0)
                {
                    if (up) selection = (selection + actions.Count - 1) % actions.Count;
                    if (down) selection = (selection + 1) % actions.Count;
                    selection = Mathf.Clamp(selection, 0, actions.Count - 1);
                    if (confirm) actions[selection]();
                }
            }
            if (up || down || confirm || back || append || delete) nextRefresh = 0;
            if (Time.realtimeSinceStartupAsDouble >= nextRefresh)
            { nextRefresh = Time.realtimeSinceStartupAsDouble + .1; Refresh(); }
        }

        private void Open()
        {
            priorJoining = bootstrap.VehicleManager.AllowControllerJoining;
            bootstrap.VehicleManager.AllowControllerJoining = false;
            typingKeyboard = Keyboard.current;
            if (typingKeyboard != null) typingKeyboard.onTextInput += OnTextInput;
            visible = true;
            selection = 0;
            BuildPanel();
            panel.SetActive(true);
            Refresh();
        }

        private void Close()
        {
            if (typingKeyboard != null) typingKeyboard.onTextInput -= OnTextInput;
            typingKeyboard = null;
            if (bootstrap != null && bootstrap.VehicleManager != null)
                bootstrap.VehicleManager.AllowControllerJoining = priorJoining;
            visible = false;
            enteringCode = false;
            if (panel != null) panel.SetActive(false);
        }

        private void OnTextInput(char character)
        {
            if (!visible || !enteringCode || code.Length >= 12) return;
            char value = char.ToUpperInvariant(character);
            if (CodeAlphabet.IndexOf(value) >= 0) { code += value; nextRefresh = 0; }
        }

        private async void Connect(bool host)
        {
            failure = null;
            selection = 0;
            connectStarted = Time.realtimeSinceStartupAsDouble;
            Task operation = host ? lobby.HostAsync() : lobby.JoinAsync(code);
            await operation;
            if (disposed || !visible || !isActiveAndEnabled)
            {
                if (lobby.State == OnlineLobbySession.LobbyState.Joined) await lobby.LeaveAsync();
                return;
            }
            if (lobby.State != OnlineLobbySession.LobbyState.Joined) return;
            try { protocol = new OnlineLobbyProtocol(connection.Connection.Network, Time.realtimeSinceStartupAsDouble); }
            catch (Exception)
            {
                failure = "Lobby protocol could not start. Retry after using the same build on both peers.";
                await lobby.LeaveAsync();
            }
        }

        private void Leave()
        {
            DisposeProtocol();
            _ = lobby.LeaveAsync();
            selection = 0;
        }

        private void BuildActions()
        {
            LobbySnapshot currentSnapshot = protocol?.Snapshot;
            if (menuState == lobby.State && ReferenceEquals(menuSnapshot, currentSnapshot) && menuSlot == localSlot) return;
            menuState = lobby.State;
            menuSnapshot = currentSnapshot;
            menuSlot = localSlot;
            labels.Clear(); actions.Clear();
            if (lobby.State == OnlineLobbySession.LobbyState.Idle)
            {
                Add("HOST PRIVATE SESSION", () => Connect(true));
                Add("JOIN BY CODE", () => { code = string.Empty; enteringCode = true; });
                Add("RETURN TO LOCAL PLAY", Close);
            }
            else if (lobby.State == OnlineLobbySession.LobbyState.Joined)
            {
                LobbySnapshot snapshot = protocol?.Snapshot;
                if (snapshot != null)
                {
                    var racer = FindLocal(snapshot, localSlot);
                    Add("SELECT LOCAL SLOT: " + (localSlot + 1), () => localSlot = (localSlot + 1) % 4);
                    if (racer == null)
                        Add("ADD HUMAN IN SLOT " + (localSlot + 1), () => Submit(new LobbyCommand { Action = LobbyAction.AddHuman, Slot = (byte)localSlot }));
                    else
                    {
                        Add(racer.Ready ? "MARK SELECTED HUMAN NOT READY" : "MARK SELECTED HUMAN READY",
                            () => Submit(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = racer.Id, Ready = !racer.Ready }));
                        Add("REMOVE SELECTED HUMAN (KEEP AT LEAST ONE)",
                            () => Submit(new LobbyCommand { Action = LobbyAction.RemoveHuman, RacerId = racer.Id }));
                    }
                    if (protocol.IsHost)
                    {
                        Add("NEXT TRACK", CycleTrack);
                        Add("AI OPPONENTS: " + CountBots(snapshot) + " (CYCLE 0–3)",
                            () => Submit(new LobbyCommand { Action = LobbyAction.SetBots, BotCount = (CountBots(snapshot) + 1) % 4 }));
                    }
                }
                Add("LEAVE SESSION", Leave);
            }
            else Add("CANCEL / RETURN TO LOCAL PLAY", () => { lobby.Cancel(); Close(); });
            selection = Mathf.Clamp(selection, 0, Mathf.Max(0, actions.Count - 1));
        }

        private void Add(string label, Action action) { labels.Add(label); actions.Add(action); }
        private void Submit(LobbyCommand command) => protocol?.Submit(command);
        private void CycleTrack()
        {
            var entries = TrackCatalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id != protocol.Snapshot.TrackId) continue;
                Submit(new LobbyCommand { Action = LobbyAction.SelectTrack, TrackId = entries[(i + 1) % entries.Count].Id });
                return;
            }
        }
        private LobbyRacer FindLocal(LobbySnapshot snapshot, int slot)
        {
            foreach (var racer in snapshot.Racers)
                if (!racer.Bot && racer.Peer == protocol.LocalPeer && racer.Slot == slot) return racer;
            return null;
        }
        private static int CountBots(LobbySnapshot snapshot)
        {
            int count = 0;
            foreach (var racer in snapshot.Racers) if (racer.Bot) count++;
            return count;
        }

        private void Refresh()
        {
            if (text == null) return;
            var content = new StringBuilder("PRIVATE ONLINE LOBBY — DEVELOPMENT\n\n");
            content.Append(failure ?? lobby.Message).Append('\n');
            if (enteringCode)
                content.Append("\nJOIN CODE: ").Append(code).Append("_\nPAD: UP/DOWN CHOOSES [")
                    .Append(CodeAlphabet[codeCharacter]).Append("]   RIGHT / X APPENDS   LEFT DELETES\nA / ENTER: JOIN   B / ESC: CANCEL\nKeyboard typing is also supported.");
            else
            {
                if (lobby.State == OnlineLobbySession.LobbyState.Joined)
                {
                    content.Append(lobby.IsHost ? "SHARE CODE: " + lobby.JoinCode : "CONNECTED TO HOST").Append('\n');
                    var snapshot = protocol?.Snapshot;
                    if (snapshot != null)
                    {
                        content.Append("TRACK: ").Append(TrackCatalog.TryGet(snapshot.TrackId, out var track) ? track.DisplayName : snapshot.TrackId).Append('\n');
                        foreach (var racer in snapshot.Racers)
                            content.Append("R").Append(racer.Id).Append(racer.Bot ? "  HOST AI" :
                                (racer.Peer == protocol.LocalPeer ? "  YOU / SLOT " : "  FRIEND / SLOT ") + (racer.Slot + 1))
                                .Append(racer.Ready ? "  READY\n" : "  NOT READY\n");
                        content.Append(OnlineLobbyProtocol.Describe(protocol.LastError)).Append('\n');
                        if (protocol.RequestPending) content.Append("WAITING FOR HOST...\n");
                    }
                }
                BuildActions();
                content.Append('\n');
                for (int i = 0; i < labels.Count; i++) content.Append(i == selection ? "> " : "   ").Append(labels[i]).Append('\n');
                content.Append("\nUP/DOWN: SELECT   A / ENTER: CONFIRM   B / ESC: BACK");
            }
            content.Append("\n\nRace start is unavailable: online world simulation is not integrated yet.");
            string next = content.ToString();
            if (next == lastText) return;
            lastText = next;
            text.text = next;
        }

        private void BuildTitleHint()
        {
            titleHint = new GameObject("Private Online Hint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            titleHint.transform.SetParent(transform, false);
            var canvas = titleHint.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = titleHint.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var label = new GameObject("Online Entry", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(titleHint.transform, false);
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, .065f);
            rect.offsetMin = new Vector2(35, 8); rect.offsetMax = new Vector2(-35, 0);
            var hint = label.GetComponent<TextMeshProUGUI>();
            hint.fontSize = 22; hint.color = Color.white;
            hint.alignment = TextAlignmentOptions.Center;
            hint.text = "PRIVATE ONLINE LOBBY (DEVELOPMENT): F8 / PAD SELECT — RACING NOT YET AVAILABLE";
            hint.raycastTarget = false;
        }

        private void BuildPanel()
        {
            if (panel != null) return;
            panel = new GameObject("Private Online Lobby", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            panel.transform.SetParent(transform, false);
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = panel.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(panel.transform, false);
            var rect = background.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.06f, .06f); rect.anchorMax = new Vector2(.94f, .94f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(.025f, .04f, .045f, .99f);
            var label = new GameObject("Lobby Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(background.transform, false);
            rect = label.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(45, 30); rect.offsetMax = new Vector2(-45, -30);
            text = label.GetComponent<TextMeshProUGUI>();
            text.fontSize = 26; text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        private void DisposeProtocol() { protocol?.Dispose(); protocol = null; }
        private void OnDisable()
        {
            lobby?.Cancel();
            DisposeProtocol();
            if (visible) Close();
            if (titleHint != null) titleHint.SetActive(false);
            if (lobby != null && lobby.State == OnlineLobbySession.LobbyState.Joined) _ = lobby.LeaveAsync();
        }
        private void OnDestroy()
        {
            disposed = true;
            OnDisable();
        }
    }
}
