using System.Collections.Generic;
using Core;
using NUnit.Framework;
using Presentation;
using UnityEngine;

namespace Tests.EditMode
{
    public sealed class TrackSelectionContractTests
    {
        private sealed class FakeRuntime : ITrackRuntime
        {
            public GameObject Root { get; } = new("FakeTrackRuntime");
            public Transform[] Grid { get; } = new Transform[4];
            public RaceCheckpoint[] Checkpoints { get; } = new RaceCheckpoint[1];
            public LayerMask DrivableMask => 1 << 8;
            public IReadOnlyList<Vector3> PickupPositions { get; } = new Vector3[4];
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }

        [Test]
        public void ProductionCatalogContainsOneGenuineAvailableWorkshopTrack()
        {
            Assert.That(TrackCatalog.Entries.Count, Is.EqualTo(1));
            Assert.That(TrackCatalog.Default.Id, Is.EqualTo(TrackCatalog.WorkshopCircuitId));
            Assert.That(TrackCatalog.Default.IsAvailable, Is.True);
            Assert.That(TrackCatalog.Default.OrderedCheckpointCount,
                Is.EqualTo(PrototypeCircuitBuilder.OrderedCheckpointCount));
            Assert.That(TrackCatalog.Default.FlowDescription, Is.Not.Empty);
            Assert.That(TrackCatalog.Default.PreviewMetadata, Is.Not.Empty);
        }

        [Test]
        public void MissingUnknownAndUnavailablePreferencesFallBackToFirstAvailableTrack()
        {
            TrackDefinition available = Definition("available", _ => new FakeRuntime());
            TrackDefinition unavailable = Definition("unavailable", null);
            TrackDefinition[] catalog = { available, unavailable };

            Assert.That(new TrackSession(catalog).SelectedTrackId, Is.EqualTo("available"));
            Assert.That(new TrackSession(catalog, "missing").SelectedTrackId, Is.EqualTo("available"));
            Assert.That(new TrackSession(catalog, "unavailable").SelectedTrackId, Is.EqualTo("available"));
        }

        [Test]
        public void SelectedBuilderAloneConstructsTheSessionRuntime()
        {
            int firstBuilds = 0;
            int secondBuilds = 0;
            FakeRuntime expected = new();
            TrackSession session = new(new[]
            {
                Definition("first", _ => { firstBuilds++; return new FakeRuntime(); }),
                Definition("second", _ => { secondBuilds++; return expected; })
            });
            Assert.That(session.TrySelect("second"), Is.True);

            ITrackRuntime built = session.BuildSelected(null);

            Assert.That(built, Is.SameAs(expected));
            Assert.That(firstBuilds, Is.Zero);
            Assert.That(secondBuilds, Is.EqualTo(1));
            built.Dispose();
            Object.DestroyImmediate(expected.Root);
        }

        [Test]
        public void UnavailableSelectionCannotBuildOrStart()
        {
            TrackSession session = new(new[]
            {
                Definition("available", _ => new FakeRuntime()),
                Definition("unavailable", null)
            });

            Assert.That(session.TrySelect("unavailable"), Is.True);
            Assert.That(session.CanStart, Is.False);
            Assert.Throws<System.InvalidOperationException>(() => session.BuildSelected(null));
        }

        [Test]
        public void IdleTrackRuleChangePreservesRegisteredRacerIdentity()
        {
            RaceSession race = new(3, 8);
            Assert.That(race.TryRegisterRacer("player-1", "Player 1", true, 0, out RacerState racer), Is.True);

            Assert.That(race.TryReconfigure(4, 10), Is.True);

            Assert.That(race.TotalLaps, Is.EqualTo(4));
            Assert.That(race.CheckpointCount, Is.EqualTo(10));
            Assert.That(race.TryGetRacer("player-1", out RacerState retained), Is.True);
            Assert.That(retained, Is.SameAs(racer));
        }

        private static TrackDefinition Definition(string id, System.Func<RaceManager, ITrackRuntime> builder) =>
            new(id, id.ToUpperInvariant(), "FLOW", "PREVIEW", 3, 1,
                builder == null ? null : (race, parent, style) => builder(race));
    }
}
