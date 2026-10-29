using System;
using NUnit.Framework;
using ReReVolt.Online.Contracts;
using Weapons;

namespace ReReVolt.Tests.Online
{
    public sealed class OnlineDrivingSnapshotTests
    {
        private OnlineLobbyAuthority authority;
        private OnlineDrivingSnapshotReceiver receiver;
        private uint human, bot;
        [SetUp] public void SetUp()
        {
            authority = new OnlineLobbyAuthority("host", 12, new[] { "a" }, "a");
            Apply(new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 1 });
            var roster = authority.Snapshot(); human = roster.Racers[0].Id; bot = roster.Racers[1].Id;
            Apply(new LobbyCommand { Action = LobbyAction.SetReady, RacerId = human, Ready = true });
            Apply(new LobbyCommand { Action = LobbyAction.Start });
            receiver = new OnlineDrivingSnapshotReceiver(authority.Snapshot(), 3, 8);
        }
        private void Apply(LobbyCommand command)
        {
            command.Epoch = authority.Epoch; command.Revision = authority.Revision;
            Assert.That(authority.Apply("host", command, true), Is.EqualTo(OnlineError.None));
        }
        private DrivingWorldSnapshot Snapshot(ulong sequence = 1)
        {
            return new DrivingWorldSnapshot
            {
                Epoch = 12, Round = authority.Round, Sequence = sequence, Tick = sequence,
                TrackId = "a", Phase = DrivingPhase.Racing, Elapsed = 10,
                Racers = new[] {
                    new DrivingRacerState { RacerId = human, PositionInRace = 1, FinishTime = -1, Rotation = new WorldRotation { W = 1 } },
                    new DrivingRacerState { RacerId = bot, PositionInRace = 2, FinishTime = -1, Rotation = new WorldRotation { W = 1 } }
                }
            };
        }
        [Test] public void ReorderedDroppedAndDuplicateSnapshotsDoNotRollbackState()
        {
            Assert.That(receiver.Accept(Snapshot(1)), Is.EqualTo(SnapshotError.None));
            Assert.That(receiver.Accept(Snapshot(4)), Is.EqualTo(SnapshotError.None));
            Assert.That(receiver.Accept(Snapshot(3)), Is.EqualTo(SnapshotError.Stale));
            Assert.That(receiver.Accept(Snapshot(4)), Is.EqualTo(SnapshotError.Stale));
            var candidate = Snapshot(5); candidate.Tick = 3;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.Stale));
            Assert.That(receiver.Latest.Sequence, Is.EqualTo(4));
        }
        [Test] public void SnapshotInputAndOutputCannotMutateRepairBaseline()
        {
            var candidate = Snapshot();
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.None));
            candidate.Racers[0].FinishPosition = 2;
            candidate.Sequence = 500;
            var copy = receiver.Latest; copy.Racers[0].FinishTime = 99; copy.Sequence = 600;
            Assert.That(receiver.Latest.Sequence, Is.EqualTo(1));
            Assert.That(receiver.Latest.Racers[0].FinishPosition, Is.Zero);
            Assert.That(receiver.Latest.Racers[0].FinishTime, Is.EqualTo(-1));
        }
        [Test] public void RejectsForeignSessionRoundAndTrack()
        {
            var candidate = Snapshot(); candidate.Epoch++;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.WrongEpoch));
            candidate = Snapshot(); candidate.Round++;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.WrongRound));
            candidate = Snapshot(); candidate.TrackId = "b";
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.WrongTrack));
            Assert.That(receiver.Latest, Is.Null);
        }
        [Test] public void RejectsPartialDuplicateAndForeignRacersAtomically()
        {
            var candidate = Snapshot(); candidate.Racers = new[] { candidate.Racers[0] };
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidRoster));
            candidate = Snapshot(); candidate.Racers[1].RacerId = human;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidRoster));
            candidate = Snapshot(); candidate.Racers[1].RacerId = 999;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidRoster));
            Assert.That(receiver.Latest, Is.Null);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void RejectsNonfinitePhysics(float invalid)
        {
            var candidate = Snapshot(); candidate.Racers[0].Position.X = invalid;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
            candidate = Snapshot(); candidate.Racers[0].Velocity.Z = invalid;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
            candidate = Snapshot(); candidate.Racers[0].AngularVelocity.Y = invalid;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
            candidate = Snapshot(); candidate.Racers[0].Rotation.W = invalid;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
        }
        [Test] public void RejectsBadRotationProgressAndStandings()
        {
            var candidate = Snapshot(); candidate.Racers[0].Rotation.W = 0;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
            candidate = Snapshot(); candidate.Racers[0].NextCheckpoint = 8;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
            candidate = Snapshot(); candidate.Racers[0].PositionInRace = 2;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
            candidate = Snapshot(); candidate.Racers[0].CompletedLaps = 3;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
            candidate = Snapshot(); candidate.Racers[0].HeldWeapon = (WeaponKind)99;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
        }
        [Test] public void AcceptsHostHeldWeaponButNotInventoryDuringCountdown()
        {
            var candidate = Snapshot(); candidate.Racers[0].HeldWeapon = WeaponKind.Bolt;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.None));
            Assert.That(receiver.Latest.Racers[0].HeldWeapon, Is.EqualTo(WeaponKind.Bolt));
            var countdownReceiver = new OnlineDrivingSnapshotReceiver(authority.Snapshot(), 3, 8);
            candidate.Phase = DrivingPhase.Countdown; candidate.Elapsed = 0; candidate.Countdown = 3;
            Assert.That(countdownReceiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
        }
        [Test] public void AcceptsMissedCheckpointUpdatesAndRecoveryCorrectionsWithoutReversingProgress()
        {
            var candidate = Snapshot(); candidate.Racers[0].NextCheckpoint = 4;
            candidate.Racers[0].AcknowledgedInput = 20; candidate.Racers[0].RecoveryRevision = 1;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.None));
            candidate = Snapshot(2); candidate.Racers[0].CompletedLaps = 1; candidate.Racers[0].NextCheckpoint = 1;
            candidate.Racers[0].AcknowledgedInput = 30; candidate.Racers[0].RecoveryRevision = 2;
            candidate.Racers[0].Position = new WorldVector(-15, .7f, 2);
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.None));
            candidate.Sequence++; candidate.Racers[0].AcknowledgedInput = 29;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
            candidate.Racers[0].AcknowledgedInput = 30; candidate.Racers[0].RecoveryRevision = 1;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
            candidate.Racers[0].RecoveryRevision = 2; candidate.Racers[0].NextCheckpoint = 0;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
        }
        [Test] public void ResultsRequireACompleteConsistentFinishOrder()
        {
            var candidate = Snapshot(); candidate.Phase = DrivingPhase.Results;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
            for (int i = 0; i < candidate.Racers.Length; i++)
            {
                candidate.Racers[i].CompletedLaps = 3; candidate.Racers[i].FinishPosition = i + 1;
                candidate.Racers[i].FinishTime = 9 + i;
            }
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.None));
            candidate.Sequence++; candidate.Racers[0].FinishTime = 8;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
        }
        [Test] public void CountdownCannotContainRaceProgressOrReappearAfterStart()
        {
            var candidate = Snapshot(); candidate.Phase = DrivingPhase.Countdown; candidate.Countdown = 3;
            candidate.Elapsed = 0;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.None));
            Assert.That(receiver.Accept(Snapshot(2)), Is.EqualTo(SnapshotError.None));
            candidate.Sequence = 3; candidate.Tick = 3; candidate.Elapsed = 10;
            Assert.That(receiver.Accept(candidate), Is.EqualTo(SnapshotError.InvalidState));
        }
        [Test] public void AbortingCountdownUnlocksRosterAndRequiresRenewedReadiness()
        {
            uint id = authority.Snapshot().Racers[0].Id;
            Assert.That(authority.AbortRound(), Is.True);
            Assert.That(authority.AbortRound(), Is.False);
            Assert.That(authority.Phase, Is.EqualTo(OnlinePhase.Lobby));
            Assert.That(authority.Snapshot().Racers[0].Ready, Is.False);
            Assert.That(authority.Snapshot().Racers[0].Id, Is.EqualTo(id));
            Assert.That(authority.AdmitPeer("friend"), Is.EqualTo(OnlineError.None));
        }
    }
}
