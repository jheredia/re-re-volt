using System;
using Core;
using NUnit.Framework;

namespace Tests.EditMode
{
    public sealed class OnlineRacerRosterTests
    {
        private const ulong Epoch = 12;
        private OnlineRacerRoster roster;

        [SetUp]
        public void SetUp() => roster = new OnlineRacerRoster("host", Epoch);

        [Test]
        public void TwoPeersOwnTwoHumansEachAndCannotExceedGrid()
        {
            Assert.That(roster.AddPeer("host", "friend", Epoch, roster.Revision), Is.EqualTo(OnlineRosterError.None));
            uint[] ids = new uint[4];
            Assert.That(roster.AddHuman("host", 0, Epoch, roster.Revision, out ids[0]), Is.EqualTo(OnlineRosterError.None));
            Assert.That(roster.AddHuman("host", 1, Epoch, roster.Revision, out ids[1]), Is.EqualTo(OnlineRosterError.None));
            Assert.That(roster.AddHuman("friend", 0, Epoch, roster.Revision, out ids[2]), Is.EqualTo(OnlineRosterError.None));
            Assert.That(roster.AddHuman("friend", 1, Epoch, roster.Revision, out ids[3]), Is.EqualTo(OnlineRosterError.None));
            CollectionAssert.AllItemsAreUnique(ids);
            Assert.That(roster.OwnsHuman("friend", ids[2]), Is.True);
            Assert.That(roster.OwnsHuman("host", ids[2]), Is.False);
            ulong revision = roster.Revision;
            Assert.That(roster.AddHuman("friend", 2, Epoch, revision, out _), Is.EqualTo(OnlineRosterError.RacerCapacity));
            Assert.That(roster.SetBotCount("host", 1, Epoch, revision), Is.EqualTo(OnlineRosterError.RacerCapacity));
            Assert.That(roster.Count, Is.EqualTo(4));
            Assert.That(roster.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void BotsAreHostSelectedWithoutLocalSlotAndRetainIdsWhenCountChanges()
        {
            Assert.That(roster.AddHuman("host", 0, Epoch, roster.Revision, out uint human), Is.EqualTo(OnlineRosterError.None));
            Assert.That(roster.SetBotCount("host", 3, Epoch, roster.Revision), Is.EqualTo(OnlineRosterError.None));
            var original = roster.Snapshot();
            for (int i = 1; i < 4; i++)
            {
                Assert.That(original[i].IsBot, Is.True);
                Assert.That(original[i].LocalSlot, Is.Null);
                Assert.That(roster.OwnsHuman("host", original[i].Id), Is.False);
            }
            Assert.That(roster.SetBotCount("host", 1, Epoch, roster.Revision), Is.EqualTo(OnlineRosterError.None));
            Assert.That(roster.Snapshot()[1].Id, Is.EqualTo(original[1].Id));
            Assert.That(roster.SetBotCount("host", 3, Epoch, roster.Revision), Is.EqualTo(OnlineRosterError.None));
            var updated = roster.Snapshot();
            Assert.That(updated[0].Id, Is.EqualTo(human));
            Assert.That(updated[1].Id, Is.EqualTo(original[1].Id));
            Assert.That(updated[2].Id, Is.Not.EqualTo(original[2].Id));
        }

        [Test]
        public void RejectsUnauthorizedStaleAndInvalidRequestsWithoutChangingState()
        {
            Assert.That(roster.AddPeer("host", "friend", Epoch, roster.Revision), Is.EqualTo(OnlineRosterError.None));
            Assert.That(roster.AddHuman("host", 0, Epoch, roster.Revision, out uint hostRacer), Is.EqualTo(OnlineRosterError.None));
            ulong revision = roster.Revision;
            Assert.That(roster.SetBotCount("friend", 1, Epoch, revision), Is.EqualTo(OnlineRosterError.NotHost));
            Assert.That(roster.RemoveHuman("friend", hostRacer, Epoch, revision), Is.EqualTo(OnlineRosterError.NotOwner));
            Assert.That(roster.AddHuman("friend", 4, Epoch, revision, out _), Is.EqualTo(OnlineRosterError.InvalidSlot));
            Assert.That(roster.AddHuman("host", 0, Epoch, revision, out _), Is.EqualTo(OnlineRosterError.SlotOccupied));
            Assert.That(roster.AddHuman("friend", 0, Epoch + 1, revision, out _), Is.EqualTo(OnlineRosterError.WrongEpoch));
            Assert.That(roster.AddHuman("friend", 0, Epoch, revision - 1, out _), Is.EqualTo(OnlineRosterError.StaleRevision));
            Assert.That(roster.AddHuman("intruder", 0, Epoch, revision, out _), Is.EqualTo(OnlineRosterError.UnknownPeer));
            Assert.That(roster.SetBotCount("host", 4, Epoch, revision), Is.EqualTo(OnlineRosterError.InvalidBotCount));
            Assert.That(roster.Revision, Is.EqualTo(revision));
            Assert.That(roster.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisconnectRemovesOnlyPeerHumansInOneRevisionAndIdsNeverReused()
        {
            roster.AddPeer("host", "friend", Epoch, roster.Revision);
            roster.AddHuman("host", 0, Epoch, roster.Revision, out uint host);
            roster.AddHuman("friend", 0, Epoch, roster.Revision, out uint departed);
            roster.AddHuman("friend", 1, Epoch, roster.Revision, out _);
            roster.SetBotCount("host", 1, Epoch, roster.Revision);
            ulong revision = roster.Revision;
            Assert.That(roster.RemovePeer("host", "friend", Epoch, revision), Is.EqualTo(OnlineRosterError.None));
            Assert.That(roster.Revision, Is.EqualTo(revision + 1));
            Assert.That(roster.Count, Is.EqualTo(2));
            Assert.That(roster.OwnsHuman("host", host), Is.True);
            Assert.That(roster.OwnsHuman("friend", departed), Is.False);
            roster.AddPeer("host", "friend", Epoch, roster.Revision);
            roster.AddHuman("friend", 0, Epoch, roster.Revision, out uint replacement);
            Assert.That(replacement, Is.GreaterThan(departed));
        }

        [Test]
        public void LockFreezesRosterAndSnapshotsAreIndependent()
        {
            roster.AddHuman("host", 0, Epoch, roster.Revision, out uint racer);
            var snapshot = roster.Snapshot();
            Assert.That(roster.SetLocked("host", true, Epoch, roster.Revision), Is.EqualTo(OnlineRosterError.None));
            Assert.That(roster.AddHuman("host", 1, Epoch, roster.Revision, out _), Is.EqualTo(OnlineRosterError.Locked));
            Assert.That(roster.SetLocked("friend", false, Epoch, roster.Revision), Is.EqualTo(OnlineRosterError.NotHost));
            Assert.That(roster.SetLocked("host", false, Epoch, roster.Revision), Is.EqualTo(OnlineRosterError.None));
            roster.RemoveHuman("host", racer, Epoch, roster.Revision);
            Assert.That(snapshot.Length, Is.EqualTo(1));
            Assert.That(snapshot[0].Id, Is.EqualTo(racer));
            Assert.That(roster.Count, Is.Zero);
        }

        [Test]
        public void EnforcesFourPeerLimitIndependentlyOfRacerLimit()
        {
            foreach (string peer in new[] { "one", "two", "three" })
                Assert.That(roster.AddPeer("host", peer, Epoch, roster.Revision), Is.EqualTo(OnlineRosterError.None));
            Assert.That(roster.AddPeer("host", "four", Epoch, roster.Revision), Is.EqualTo(OnlineRosterError.PeerCapacity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OnlineRacerRoster("host", 0));
        }
    }
}
