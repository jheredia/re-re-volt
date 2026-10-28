using System;
using System.Threading.Tasks;

namespace ReReVolt.Online.Unity
{
    public enum OnlineConnectionError { None, InvalidCode, ServiceUnavailable, CleanupFailed, Cancelled, ConnectionLost }

    /// <summary>Serializes service operations, including cleanup of a cancelled operation's late result.</summary>
    public sealed class OnlineLobbySession
    {
        public interface IConnection
        {
            string JoinCode { get; }
            bool IsHost { get; }
            Task HostAsync();
            Task JoinAsync(string code);
            Task LeaveAsync();
        }

        public enum LobbyState { Idle, Connecting, Cancelling, Joined, Leaving }
        private readonly IConnection connection;
        private bool cancelRequested;
        public LobbyState State { get; private set; }
        public OnlineConnectionError Error { get; private set; }
        public string Message { get; private set; } = "Host a private session or enter a join code.";
        public string JoinCode => State == LobbyState.Joined ? connection.JoinCode : null;
        public bool IsHost => State == LobbyState.Joined && connection.IsHost;

        public OnlineLobbySession(IConnection connection) =>
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));

        public Task HostAsync() => ConnectAsync(null);
        public Task JoinAsync(string code) => ConnectAsync(code ?? string.Empty);

        private async Task ConnectAsync(string code)
        {
            if (State != LobbyState.Idle) return;
            if (code != null && !IsJoinCode(code.Trim()))
            {
                Error = OnlineConnectionError.InvalidCode;
                Message = "Enter the host's join code (letters and numbers, up to 12 characters).";
                return;
            }
            State = LobbyState.Connecting;
            Error = OnlineConnectionError.None;
            cancelRequested = false;
            Message = "Connecting to Unity services... Back cancels.";
            try
            {
                if (code == null) await connection.HostAsync();
                else await connection.JoinAsync(code.Trim().ToUpperInvariant());
                if (cancelRequested)
                {
                    await ReleaseAsync("Connection cancelled.");
                    Error = Error == OnlineConnectionError.CleanupFailed ? Error : OnlineConnectionError.Cancelled;
                    return;
                }
                State = LobbyState.Joined;
                Message = "Transport connected. Negotiating the private lobby...";
            }
            catch (Exception)
            {
                // The adapter owns cleanup on connection failure. Do not overlap a second operation.
                State = LobbyState.Idle;
                Error = cancelRequested ? OnlineConnectionError.Cancelled : OnlineConnectionError.ServiceUnavailable;
                Message = cancelRequested ? "Connection cancelled." :
                    "Could not connect. Check the code and internet. The owner must link and enable Unity Services before Relay can be tested. Retry or Back.";
            }
        }

        public void Cancel()
        {
            if (State != LobbyState.Connecting && State != LobbyState.Cancelling) return;
            cancelRequested = true;
            State = LobbyState.Cancelling;
            Message = "Cancelling. You can return to local play while the service operation cleans up.";
        }

        public async Task LeaveAsync()
        {
            if (State != LobbyState.Joined) return;
            await ReleaseAsync("Session left.");
        }

        public async Task ConnectionLostAsync()
        {
            if (State != LobbyState.Joined) return;
            await ReleaseAsync("Connection lost. Host migration is unavailable. Host or join a new session.");
            if (Error != OnlineConnectionError.CleanupFailed) Error = OnlineConnectionError.ConnectionLost;
        }

        private async Task ReleaseAsync(string message)
        {
            State = LobbyState.Leaving;
            Message = "Leaving session...";
            try { await connection.LeaveAsync(); Message = message; }
            catch (Exception)
            {
                Error = OnlineConnectionError.CleanupFailed;
                Message = "Local transport closed, but service cleanup failed. Check the Unity Dashboard before retrying.";
            }
            finally { State = LobbyState.Idle; }
        }

        private static bool IsJoinCode(string code)
        {
            if (code.Length == 0 || code.Length > 12) return false;
            foreach (char c in code)
                if (!(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9')) return false;
            return true;
        }
    }

}
