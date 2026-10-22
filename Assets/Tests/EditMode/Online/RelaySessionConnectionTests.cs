using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ReReVolt.Online.Unity;

namespace ReReVolt.Tests.Online
{
    public sealed class RelaySessionConnectionTests
    {
        [Test]
        public async Task EmptyJoinCodeDoesNotInitializeServicesOrChangeState()
        {
            var connection = new RelaySessionConnection();
            Assert.ThrowsAsync<ArgumentException>(async () => await connection.JoinAsync("  "));
            Assert.That(connection.State, Is.EqualTo(RelaySessionConnection.ConnectionState.Idle));
            Assert.That(connection.JoinCode, Is.Null);
            Assert.That(connection.IsHost, Is.False);
            await connection.LeaveAsync();
            Assert.That(connection.State, Is.EqualTo(RelaySessionConnection.ConnectionState.Idle));
        }
    }
}
