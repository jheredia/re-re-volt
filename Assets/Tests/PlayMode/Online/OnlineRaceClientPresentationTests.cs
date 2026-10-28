using System.Collections;
using NUnit.Framework;
using Presentation;
using ReReVolt.Online.Contracts;
using ReReVolt.Online.Unity;
using UnityEngine;
using UnityEngine.TestTools;
using Vehicle;

namespace ReReVolt.Tests.Online
{
    public sealed class OnlineRaceClientPresentationTests
    {
        private GameObject holder;
        private OnlineRaceClientPresentation presentation;

        [SetUp]
        public void SetUp()
        {
            holder = new GameObject("Online client presentation test");
            presentation = holder.AddComponent<OnlineRaceClientPresentation>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (presentation != null) presentation.Shutdown();
            if (holder != null) Object.Destroy(holder);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AcceptedSnapshotsMoveVisualOnlyProxiesAndCreateOnlyPeerLocalSplitViews()
        {
            LobbySnapshot roster = LockedRoster();
            presentation.Initialize(roster, "host");
            yield return null; // Primitive collider destruction is deferred by Unity in play mode.

            Assert.That(presentation.ProxyCount, Is.EqualTo(3));
            Assert.That(presentation.LocalCameraCount, Is.EqualTo(2));
            Assert.That(holder.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            foreach (Collider collider in holder.GetComponentsInChildren<Collider>(true))
                Assert.That(collider.enabled, Is.False, "Client track geometry must never run collision authority.");
            Assert.That(holder.GetComponentsInChildren<WheelCollider>(true), Is.Empty);
            Assert.That(holder.GetComponentsInChildren<VehicleController>(true), Is.Empty);
            Assert.That(holder.GetComponentsInChildren<AudioListener>(true), Is.Empty);

            DrivingWorldSnapshot snapshot = Snapshot(roster);
            presentation.ApplyAcceptedSnapshot(snapshot);
            Assert.That(presentation.TryGetProxy(roster.Racers[0].Id, out Transform first), Is.True);
            Assert.That(first.position, Is.EqualTo(new Vector3(2, .4f, -1)));
            Assert.That(presentation.TryGetProxy(roster.Racers[2].Id, out Transform remote), Is.True);
            Assert.That(remote.position, Is.EqualTo(new Vector3(-4, .5f, 3)));
            Assert.That(presentation.TryGetLocalCamera(roster.Racers[0].Id, out Camera upper), Is.True);
            Assert.That(presentation.TryGetLocalCamera(roster.Racers[1].Id, out Camera lower), Is.True);
            Assert.That(upper.rect, Is.EqualTo(new Rect(0, .5f, 1, .5f)));
            Assert.That(lower.rect, Is.EqualTo(new Rect(0, 0, 1, .5f)));
            Assert.That(presentation.TryGetLocalCamera(roster.Racers[2].Id, out _), Is.False);

            presentation.Shutdown();
            yield return null;
            Assert.That(holder.GetComponentsInChildren<Camera>(true), Is.Empty);
        }

        private static LobbySnapshot LockedRoster()
        {
            var authority = new OnlineLobbyAuthority("host", 81,
                new[] { TrackCatalog.Default.Id }, TrackCatalog.Default.Id);
            Apply(authority, "host", new LobbyCommand { Action = LobbyAction.AddHuman, Slot = 1 });
            Apply(authority, "host", new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 1 });
            foreach (LobbyRacer racer in authority.Snapshot().Racers)
                if (!racer.Bot)
                    Apply(authority, racer.Peer, new LobbyCommand { Action = LobbyAction.SetReady, RacerId = racer.Id, Ready = true });
            Apply(authority, "host", new LobbyCommand { Action = LobbyAction.Start });
            return authority.Snapshot();
        }

        private static void Apply(OnlineLobbyAuthority authority, string peer, LobbyCommand command)
        {
            command.Epoch = authority.Epoch;
            command.Revision = authority.Revision;
            Assert.That(authority.Apply(peer, command, true), Is.EqualTo(OnlineError.None));
        }

        private static DrivingWorldSnapshot Snapshot(LobbySnapshot roster)
        {
            return new DrivingWorldSnapshot
            {
                Epoch = roster.Epoch, Round = roster.Round, Sequence = 1, Tick = 1, TrackId = roster.TrackId,
                Phase = DrivingPhase.Countdown, Countdown = 3, Elapsed = 0,
                Racers = new[]
                {
                    State(roster.Racers[0].Id, new WorldVector(2, .4f, -1)),
                    State(roster.Racers[1].Id, new WorldVector(5, .4f, -2)),
                    State(roster.Racers[2].Id, new WorldVector(-4, .5f, 3))
                }
            };
        }

        private static DrivingRacerState State(uint id, WorldVector position) => new DrivingRacerState
        {
            RacerId = id, Position = position, Rotation = new WorldRotation { W = 1 }, PositionInRace = 1,
            FinishTime = -1
        };
    }
}
