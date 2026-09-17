using Core;
using NUnit.Framework;

namespace Tests.EditMode
{
    public sealed class RaceSessionTests
    {
        [Test]
        public void RegistersFourHumanRacersWithUniquePlayerSlots()
        {
            RaceSession session = new(3, 4);

            for (int index = 0; index < 4; index++)
            {
                Assert.That(session.TryRegisterRacer(
                    $"player-{index + 1}",
                    $"Player {index + 1}",
                    true,
                    index,
                    out RacerState racer), Is.True);
                Assert.That(racer.PlayerIndex, Is.EqualTo(index));

                if (index == 0)
                {
                    Assert.That(session.TryRegisterRacer(
                        "duplicate-slot",
                        "Duplicate",
                        true,
                        0,
                        out _), Is.False);
                }
            }

            Assert.That(session.RacerCount, Is.EqualTo(4));
            Assert.That(session.TryRegisterRacer("player-5", "Player 5", true, 4, out _), Is.False);
        }

        [Test]
        public void RejectsOutOfOrderAndRepeatedCheckpoints()
        {
            RaceSession session = CreateStartedSession(totalLaps: 1, checkpointCount: 3, racerCount: 1);

            Assert.That(session.TryPassCheckpoint("player-1", 1), Is.False);
            Assert.That(session.TryPassCheckpoint("player-1", 0), Is.True);
            Assert.That(session.TryPassCheckpoint("player-1", 0), Is.False);
            Assert.That(session.TryPassCheckpoint("player-1", 2), Is.False);
            Assert.That(session.Standings[0].NextCheckpointIndex, Is.EqualTo(1));
        }

        [Test]
        public void CompletingFinalCheckpointAdvancesLapsAndFinishesAtConfiguredLapCount()
        {
            RaceSession session = CreateStartedSession(totalLaps: 2, checkpointCount: 2, racerCount: 1);

            session.TryPassCheckpoint("player-1", 0);
            session.TryPassCheckpoint("player-1", 1);

            RacerState racer = session.Standings[0];
            Assert.That(racer.CompletedLaps, Is.EqualTo(1));
            Assert.That(racer.CurrentLap, Is.EqualTo(2));
            Assert.That(racer.IsFinished, Is.False);

            session.AdvanceTime(12.5f);
            session.TryPassCheckpoint("player-1", 0);
            session.TryPassCheckpoint("player-1", 1);

            Assert.That(racer.IsFinished, Is.True);
            Assert.That(racer.FinishPosition, Is.EqualTo(1));
            Assert.That(racer.FinishTime, Is.EqualTo(12.5f));
            Assert.That(session.Phase, Is.EqualTo(RacePhase.Finished));
        }

        [Test]
        public void FinishOrderRemainsStableWhileRemainingRacersComplete()
        {
            RaceSession session = CreateStartedSession(totalLaps: 1, checkpointCount: 1, racerCount: 4);

            session.AdvanceTime(5f);
            Assert.That(session.TryPassCheckpoint("player-3", 0), Is.True);
            session.AdvanceTime(1f);
            Assert.That(session.TryPassCheckpoint("player-1", 0), Is.True);

            Assert.That(session.Standings[0].RacerId, Is.EqualTo("player-3"));
            Assert.That(session.Standings[1].RacerId, Is.EqualTo("player-1"));

            session.TryPassCheckpoint("player-4", 0);
            session.TryPassCheckpoint("player-2", 0);

            Assert.That(session.Phase, Is.EqualTo(RacePhase.Finished));
            Assert.That(session.Standings[0].FinishPosition, Is.EqualTo(1));
            Assert.That(session.Standings[1].FinishPosition, Is.EqualTo(2));
            Assert.That(session.Standings[2].FinishPosition, Is.EqualTo(3));
            Assert.That(session.Standings[3].FinishPosition, Is.EqualTo(4));
            Assert.That(session.Standings[0].RacerId, Is.EqualTo("player-3"));
            Assert.That(session.Standings[3].RacerId, Is.EqualTo("player-2"));
        }

        [Test]
        public void ActiveStandingsUseProgressThenRegistrationOrderForTies()
        {
            RaceSession session = CreateStartedSession(totalLaps: 3, checkpointCount: 3, racerCount: 3);

            session.TryPassCheckpoint("player-2", 0);
            session.TryPassCheckpoint("player-3", 0);
            session.TrySetSegmentProgress("player-3", 0.75f);

            Assert.That(session.Standings[0].RacerId, Is.EqualTo("player-3"));
            Assert.That(session.Standings[1].RacerId, Is.EqualTo("player-2"));
            Assert.That(session.Standings[2].RacerId, Is.EqualTo("player-1"));

            session.TrySetSegmentProgress("player-2", 0.75f);
            Assert.That(session.Standings[0].RacerId, Is.EqualTo("player-2"));
            Assert.That(session.Standings[1].RacerId, Is.EqualTo("player-3"));
        }

        [Test]
        public void ResetPreservesRegistrationAndClearsAllRaceProgress()
        {
            RaceSession session = CreateStartedSession(totalLaps: 2, checkpointCount: 2, racerCount: 2);
            session.AdvanceTime(9f);
            session.TryPassCheckpoint("player-2", 0);

            session.Reset();

            Assert.That(session.Phase, Is.EqualTo(RacePhase.Idle));
            Assert.That(session.ElapsedTime, Is.Zero);
            Assert.That(session.RacerCount, Is.EqualTo(2));
            Assert.That(session.Standings[0].RacerId, Is.EqualTo("player-1"));
            Assert.That(session.Standings[0].CurrentPosition, Is.EqualTo(1));
            Assert.That(session.Standings[1].NextCheckpointIndex, Is.Zero);
            Assert.That(session.Standings[1].FinishTime, Is.EqualTo(-1f));
            Assert.That(session.Begin(), Is.True);
        }

        private static RaceSession CreateStartedSession(int totalLaps, int checkpointCount, int racerCount)
        {
            RaceSession session = new(totalLaps, checkpointCount);
            for (int index = 0; index < racerCount; index++)
            {
                bool registered = session.TryRegisterRacer(
                    $"player-{index + 1}",
                    $"Player {index + 1}",
                    true,
                    index,
                    out _);
                Assert.That(registered, Is.True);
            }

            Assert.That(session.Begin(), Is.True);
            return session;
        }
    }
}
