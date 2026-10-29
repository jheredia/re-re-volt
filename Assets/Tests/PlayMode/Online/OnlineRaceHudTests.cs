using System.Collections;
using NUnit.Framework;
using Presentation;
using ReReVolt.Online.Contracts;
using ReReVolt.Online.Unity;
using UnityEngine;
using UnityEngine.TestTools;
using Weapons;

namespace ReReVolt.Tests.Online
{
    public sealed class OnlineRaceHudTests
    {
        [UnityTest]
        public IEnumerator LocalHudShowsOnlyOwnedRacersFromRoundSnapshotsAndCleansUp()
        {
            var holder = new GameObject("Online HUD test");
            var hud = holder.AddComponent<OnlineRaceHud>();
            var roster = new LobbySnapshot
            {
                Epoch = 71, Round = 2, TrackId = TrackCatalog.Default.Id,
                Phase = OnlinePhase.Countdown,
                Racers = new[]
                {
                    new LobbyRacer { Id = 11, Peer = "host", Slot = 0 },
                    new LobbyRacer { Id = 12, Peer = "host", Slot = 1 },
                    new LobbyRacer { Id = 13, Peer = "friend", Slot = 0 }
                }
            };
            try
            {
                hud.Initialize(roster, "host");
                Assert.That(hud.LocalLabelCount, Is.EqualTo(2));
                Assert.That(hud.TryGetLocalStatus(13, out _), Is.False,
                    "A remote peer must not get a local HUD label.");
                var countdown = Snapshot(roster, DrivingPhase.Countdown);
                countdown.Countdown = 3;
                hud.ApplySnapshot(countdown);
                Assert.That(hud.TryGetLocalStatus(11, out string first), Is.True);
                Assert.That(first, Does.Contain("P1   START IN 3"));
                Assert.That(hud.TryGetLocalStatus(12, out string second), Is.True);
                Assert.That(second, Does.Contain("P2   START IN 3"));

                var racing = Snapshot(roster, DrivingPhase.Racing);
                racing.Elapsed = 63.4f;
                racing.Racers[0].CompletedLaps = 1;
                racing.Racers[0].PositionInRace = 2;
                racing.Racers[0].HeldWeapon = WeaponKind.Bolt;
                hud.ApplySnapshot(racing);
                Assert.That(hud.TryGetLocalStatus(11, out first), Is.True);
                Assert.That(first, Does.Contain("LAP 2/" + TrackCatalog.Default.LapCount));
                Assert.That(first, Does.Contain("POS 2/3"));
                Assert.That(first, Does.Contain("01:03.4"));
                Assert.That(first, Does.Contain("BOLT"));
                Assert.That(hud.TryGetLocalStatus(12, out second), Is.True);
                Assert.That(second, Does.Contain("P2"));

                string accepted = first;
                racing.Epoch++;
                hud.ApplySnapshot(racing);
                Assert.That(hud.TryGetLocalStatus(11, out first), Is.True);
                Assert.That(first, Is.EqualTo(accepted), "A different session must not repaint local status.");

                var result = Snapshot(roster, DrivingPhase.Results);
                result.Racers[0].FinishPosition = 1;
                result.Racers[0].FinishTime = 73.2f;
                hud.ApplySnapshot(result);
                Assert.That(hud.TryGetLocalStatus(11, out first), Is.True);
                Assert.That(first, Does.Contain("FINISHED #1"));
                Assert.That(first, Does.Contain("01:13.2"));
            }
            finally
            {
                hud.Shutdown();
                Object.Destroy(holder);
            }
            yield return null;
            Assert.That(hud == null, Is.True);
        }

        private static DrivingWorldSnapshot Snapshot(LobbySnapshot roster, DrivingPhase phase) =>
            new DrivingWorldSnapshot
            {
                Epoch = roster.Epoch, Round = roster.Round, TrackId = roster.TrackId,
                Phase = phase, Racers = new[]
                {
                    new DrivingRacerState { RacerId = 11, PositionInRace = 1 },
                    new DrivingRacerState { RacerId = 12, PositionInRace = 2 },
                    new DrivingRacerState { RacerId = 13, PositionInRace = 3 }
                }
            };
    }
}
