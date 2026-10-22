using System.Threading.Tasks;
using Core;
using Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReReVolt.Online.Unity
{
    /// <summary>Opt-in connection screen. It deliberately does not start or replicate a race.</summary>
    public sealed class OnlineLobbyEntry : MonoBehaviour
    {
        private Stage3Bootstrap bootstrap;
        private OnlineLobbySession lobby;
        private GameObject panel;
        private TextMeshProUGUI text;
        private Keyboard typingKeyboard;
        private bool visible;
        private bool enteringCode;
        private bool priorJoining;
        private bool disposed;
        private bool releaseOnCompletion;
        private string code = string.Empty;
        private string lastText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name != Stage3Bootstrap.PrototypeSceneName ||
                FindAnyObjectByType<OnlineLobbyEntry>() != null) return;
            new GameObject("Online Connection Entry").AddComponent<OnlineLobbyEntry>();
        }

        private void Awake() => lobby = new OnlineLobbySession(new RelayLobbyConnection());

        private void Update()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<Stage3Bootstrap>();
            if (bootstrap == null || bootstrap.VehicleManager == null || bootstrap.RaceManager == null) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!visible)
            {
                if (keyboard.f8Key.wasPressedThisFrame && bootstrap.RaceManager.CurrentState == RaceManager.RaceState.Idle &&
                    bootstrap.VehicleManager.Players.Count == 0) Open(keyboard);
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (enteringCode) enteringCode = false;
                else if (lobby.State == OnlineLobbySession.LobbyState.Idle) Close();
            }
            else if (enteringCode)
            {
                if (keyboard.backspaceKey.wasPressedThisFrame && code.Length > 0) code = code.Substring(0, code.Length - 1);
                if (keyboard.enterKey.wasPressedThisFrame)
                {
                    enteringCode = false;
                    RunConnect(lobby.JoinAsync(code));
                }
            }
            else if (lobby.State == OnlineLobbySession.LobbyState.Idle)
            {
                if (keyboard.hKey.wasPressedThisFrame) RunConnect(lobby.HostAsync());
                else if (keyboard.jKey.wasPressedThisFrame) { code = string.Empty; enteringCode = true; }
            }
            else if (lobby.State == OnlineLobbySession.LobbyState.Joined && keyboard.lKey.wasPressedThisFrame)
                _ = lobby.LeaveAsync();
            Refresh();
        }

        private void Open(Keyboard keyboard)
        {
            priorJoining = bootstrap.VehicleManager.AllowControllerJoining;
            bootstrap.VehicleManager.AllowControllerJoining = false;
            typingKeyboard = keyboard;
            typingKeyboard.onTextInput += OnTextInput;
            visible = true;
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
            if (!visible || !enteringCode || code.Length >= 12 || !char.IsLetterOrDigit(character)) return;
            code += char.ToUpperInvariant(character);
        }

        private async void RunConnect(Task operation)
        {
            await operation;
            // If the scene disappeared while the service was connecting, release the eventual session.
            if ((disposed || releaseOnCompletion) && lobby.State == OnlineLobbySession.LobbyState.Joined)
                await lobby.LeaveAsync();
            releaseOnCompletion = false;
        }

        private void Refresh()
        {
            if (text == null) return;
            string details = enteringCode
                ? "JOIN CODE: " + code + "_\nENTER: JOIN   BACKSPACE: DELETE   ESC: CANCEL"
                : lobby.State == OnlineLobbySession.LobbyState.Idle
                    ? "H: HOST PRIVATE SESSION   J: ENTER JOIN CODE\nESC: RETURN TO LOCAL PLAY"
                    : lobby.State == OnlineLobbySession.LobbyState.Joined
                        ? (lobby.IsHost ? "SHARE CODE: " + lobby.JoinCode : "CONNECTED TO HOST") + "\nL: LEAVE SESSION"
                        : "PLEASE WAIT...";
            string next = "ONLINE CONNECTION TEST\n\n" + lobby.Message + "\n\n" + details +
                          "\n\nOnline race replication is not available yet. Local play is unchanged.";
            if (next == lastText) return;
            lastText = next;
            text.text = next;
        }

        private void BuildPanel()
        {
            if (panel != null) return;
            panel = new GameObject("Online Connection Panel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            rect.anchorMin = new Vector2(.12f, .15f); rect.anchorMax = new Vector2(.88f, .85f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(.025f, .04f, .045f, .98f);
            var label = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(background.transform, false);
            rect = label.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(55, 35); rect.offsetMax = new Vector2(-55, -35);
            text = label.GetComponent<TextMeshProUGUI>();
            text.fontSize = 32; text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        private void OnDisable()
        {
            if (visible) Close();
            if (lobby == null) return;
            if (lobby.State == OnlineLobbySession.LobbyState.Connecting) releaseOnCompletion = true;
            else if (lobby.State == OnlineLobbySession.LobbyState.Joined) _ = lobby.LeaveAsync();
        }

        private void OnDestroy()
        {
            disposed = true;
            if (visible) Close();
            if (lobby != null && lobby.State == OnlineLobbySession.LobbyState.Joined) _ = lobby.LeaveAsync();
        }
    }
}
