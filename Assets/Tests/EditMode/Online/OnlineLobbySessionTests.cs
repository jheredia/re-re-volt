using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ReReVolt.Online.Unity;

namespace ReReVolt.Tests.Online
{
    public sealed class OnlineLobbySessionTests
    {
        private sealed class FakeConnection : OnlineLobbySession.IConnection
        {
            public string JoinCode { get; set; } = "ABC123";
            public bool IsHost { get; set; }
            public int Calls;
            public string EnteredCode;
            public bool Fail;
            public TaskCompletionSource<bool> Pending;
            public Task HostAsync() { Calls++; return Pending != null ? (Task)Pending.Task : Complete(); }
            public Task JoinAsync(string code) { Calls++; EnteredCode = code; return Complete(); }
            public Task LeaveAsync() { Calls++; return Complete(); }
            private Task Complete() => Fail ? Task.FromException(new InvalidOperationException()) : Task.CompletedTask;
        }

        [Test]
        public async Task HostShowsCodeAndLeaveRestoresIdle()
        {
            var connection = new FakeConnection { IsHost = true };
            var lobby = new OnlineLobbySession(connection);
            await lobby.HostAsync();
            Assert.That(lobby.State, Is.EqualTo(OnlineLobbySession.LobbyState.Joined));
            Assert.That(lobby.JoinCode, Is.EqualTo("ABC123"));
            Assert.That(lobby.Message, Does.Contain("not implemented"));
            await lobby.LeaveAsync();
            Assert.That(lobby.State, Is.EqualTo(OnlineLobbySession.LobbyState.Idle));
            Assert.That(lobby.JoinCode, Is.Null);
        }

        [Test]
        public async Task BlankCodeAndServiceFailureGiveRecoverableStatus()
        {
            var connection = new FakeConnection { Fail = true };
            var lobby = new OnlineLobbySession(connection);
            await lobby.JoinAsync("  ");
            Assert.That(connection.Calls, Is.Zero);
            Assert.That(lobby.Message, Does.Contain("join code"));
            await lobby.JoinAsync(" ABC123 ");
            Assert.That(connection.EnteredCode, Is.EqualTo("ABC123"));
            Assert.That(lobby.State, Is.EqualTo(OnlineLobbySession.LobbyState.Idle));
            Assert.That(lobby.Message, Does.Contain("Could not connect"));
        }

        [Test]
        public async Task ConnectingIgnoresOverlappingOperations()
        {
            var connection = new FakeConnection { Pending = new TaskCompletionSource<bool>() };
            var lobby = new OnlineLobbySession(connection);
            Task first = lobby.HostAsync();
            await lobby.JoinAsync("ABC123");
            await lobby.LeaveAsync();
            Assert.That(connection.Calls, Is.EqualTo(1));
            Assert.That(lobby.State, Is.EqualTo(OnlineLobbySession.LobbyState.Connecting));
            connection.Pending.SetResult(true);
            await first;
            Assert.That(lobby.State, Is.EqualTo(OnlineLobbySession.LobbyState.Joined));
        }
    }
}
