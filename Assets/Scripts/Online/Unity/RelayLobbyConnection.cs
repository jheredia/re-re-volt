using System.Threading.Tasks;

namespace ReReVolt.Online.Unity
{
    internal sealed class RelayLobbyConnection : OnlineLobbySession.IConnection
    {
        internal readonly RelaySessionConnection Connection = new RelaySessionConnection();
        public string JoinCode => Connection.JoinCode;
        public bool IsHost => Connection.IsHost;
        public Task HostAsync() => Connection.HostAsync();
        public Task JoinAsync(string code) => Connection.JoinAsync(code);
        public Task LeaveAsync() => Connection.LeaveAsync();
    }
}
