using System;
using System.Threading.Tasks;

namespace ReReVolt.Online.Unity
{
    /// <summary>Connection-only UI state; a joined service session is not a playable race.</summary>
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

        public enum LobbyState { Idle, Connecting, Joined, Leaving }
        private readonly IConnection connection;
        public LobbyState State { get; private set; }
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
            if (code != null && string.IsNullOrWhiteSpace(code))
            {
                Message = "Enter the host's join code.";
                return;
            }
            State = LobbyState.Connecting;
            Message = "Connecting to Unity services...";
            try
            {
                if (code == null) await connection.HostAsync();
                else await connection.JoinAsync(code.Trim());
                State = LobbyState.Joined;
                Message = "Session joined. Online racing is not implemented yet.";
            }
            catch (Exception)
            {
                State = LobbyState.Idle;
                Message = "Could not connect. Check the code, internet, and Unity Services configuration; try again.";
            }
        }

        public async Task LeaveAsync()
        {
            if (State != LobbyState.Joined) return;
            State = LobbyState.Leaving;
            Message = "Leaving session...";
            try { await connection.LeaveAsync(); Message = "Session left."; }
            catch (Exception) { Message = "Session could not be cleanly left; local connection was closed."; }
            finally { State = LobbyState.Idle; }
        }
    }

    internal sealed class RelayLobbyConnection : OnlineLobbySession.IConnection
    {
        private readonly RelaySessionConnection connection = new RelaySessionConnection();
        public string JoinCode => connection.JoinCode;
        public bool IsHost => connection.IsHost;
        public Task HostAsync() => connection.HostAsync();
        public Task JoinAsync(string code) => connection.JoinAsync(code);
        public Task LeaveAsync() => connection.LeaveAsync();
    }
}
