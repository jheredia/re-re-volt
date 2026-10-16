using System.Collections;
using System.Reflection;
using AI;
using Core;
using NUnit.Framework;
using Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.TestTools;
using Vehicle;

namespace Tests.PlayMode
{
    public sealed class Stage9CTrackRosterIntegrationTests : InputTestFixture
    {
        private GameObject root;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            root = new GameObject("Stage9CTrackRosterTest");
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
            Assert.That(InputUser.all.Count, Is.Zero);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator EveryTrackBuildsRebindsAndAcceptsZeroToThreeBotsWithoutLocalOwnership()
        {
            RaceManager race = root.AddComponent<RaceManager>();
            typeof(RaceManager).GetField("countdownTime", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(race, 0f);
            TrackDefinition first = TrackCatalog.Entries[0];
            Assert.That(race.ConfigureRace(first.LapCount, first.OrderedCheckpointCount), Is.True);
            TrackSession tracks = new(TrackCatalog.Entries);
            ITrackRuntime active = tracks.BuildSelected(race, root.transform, null);
            VehicleManager humans = root.AddComponent<VehicleManager>();
            Assert.That(humans.Configure(race, active.Grid), Is.True);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(humans.TryJoinKeyboard(keyboard, out LocalPlayer human), Is.True);
            AIManager ai = root.AddComponent<AIManager>();
            Assert.That(ai.Configure(race, humans, active.Grid, active.Checkpoints, active.DrivableMask), Is.True);

            for (int trackIndex = 0; trackIndex < TrackCatalog.Entries.Count; trackIndex++)
            {
                TrackDefinition definition = TrackCatalog.Entries[trackIndex];
                Assert.That(tracks.TrySelect(definition.Id), Is.True);
                ITrackRuntime candidate = trackIndex == 0 ? active : tracks.BuildSelected(race, root.transform, null);
                if (trackIndex != 0)
                {
                    Assert.That(race.TryReconfigureIdleRace(definition.LapCount,
                        definition.OrderedCheckpointCount), Is.True, definition.Id);
                    Assert.That(humans.TryReconfigureTrack(candidate.Grid, candidate.DrivableMask), Is.True, definition.Id);
                    Assert.That(ai.TryReconfigureTrack(candidate.Grid, candidate.Checkpoints,
                        candidate.DrivableMask), Is.True, definition.Id);
                    active.Dispose();
                    active = candidate;
                }

                Assert.That(human.Recovery, Is.Not.Null, definition.Id);
                human.Recovery.ConfigureGroundMask(active.DrivableMask);
                for (int botCount = 0; botCount <= 3; botCount++)
                {
                    Assert.That(ai.TrySetSelectedBotCount(botCount), Is.True, $"{definition.Id}/{botCount}");
                    Assert.That(ai.SelectedBotCount, Is.EqualTo(botCount));
                    Assert.That(ai.ActiveBotCount, Is.Zero, "Setup bots must not spawn yet.");
                    Assert.That(humans.Players, Has.Count.EqualTo(1), "AI must not consume a local player slot.");
                    Assert.That(humans.Players[0].Camera, Is.Not.Null);
                    Assert.That(InputUser.all.Count, Is.EqualTo(1), "AI must not own an input device.");
                }

                Assert.That(ai.PrepareRaceRoster(), Is.True, definition.Id);
                Assert.That(ai.ActiveBotCount, Is.EqualTo(3), definition.Id);
                for (int botIndex = 0; botIndex < ai.Drivers.Count; botIndex++)
                {
                    Assert.That(ai.Drivers[botIndex].GetComponentInChildren<Camera>(true), Is.Null);
                    Assert.That(ai.Drivers[botIndex].GetComponentInChildren<Canvas>(true), Is.Null);
                }
                Assert.That(InputUser.all.Count, Is.EqualTo(1));
                Assert.That(race.StartRace(), Is.True, definition.Id);
                yield return null;
                Assert.That(race.RaceRunning, Is.True, definition.Id);

                for (int checkpoint = 0; checkpoint < definition.OrderedCheckpointCount; checkpoint++)
                    Assert.That(race.ReportCheckpoint(human.Racer.RacerId, checkpoint), Is.True,
                        $"{definition.Id} checkpoint {checkpoint} should use authoritative ordering.");
                Assert.That(human.Racer.CurrentLap, Is.EqualTo(2), definition.Id);
                race.ResetRace();
                Assert.That(ai.ReleaseBotsForLobby(), Is.True, definition.Id);
                Assert.That(ai.ActiveBotCount, Is.Zero);
                yield return null;
            }

            active.Dispose();
        }
    }
}
