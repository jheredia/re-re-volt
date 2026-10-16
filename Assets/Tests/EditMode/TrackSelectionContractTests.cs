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
        public void ProductionCatalogContainsFourDistinctAvailableAuthoredTracks()
        {
            Assert.That(TrackCatalog.Entries.Count, Is.EqualTo(4));
            Assert.That(TrackCatalog.Default.Id, Is.EqualTo(TrackCatalog.WorkshopCircuitId));
            HashSet<string> ids = new();
            HashSet<string> names = new();
            HashSet<string> flows = new();
            for (int i = 0; i < TrackCatalog.Entries.Count; i++)
            {
                TrackDefinition entry = TrackCatalog.Entries[i];
                Assert.That(entry.IsAvailable, Is.True, entry.Id);
                Assert.That(ids.Add(entry.Id), Is.True, "Stable IDs must be unique.");
                Assert.That(names.Add(entry.DisplayName), Is.True, "Display names must be unique.");
                Assert.That(flows.Add(entry.FlowDescription), Is.True, "Every track needs a distinct flow promise.");
                Assert.That(entry.PreviewMetadata, Is.Not.Empty);
                Assert.That(entry.LapCount, Is.GreaterThan(0));
                Assert.That(entry.OrderedCheckpointCount, Is.GreaterThanOrEqualTo(8));
            }
        }

        [Test]
        public void EveryNewProductionRuntimeSatisfiesGridCheckpointSurfacePickupAndCleanupContract()
        {
            GameObject owner = new("TrackRuntimeContractOwner");
            RaceManager race = owner.AddComponent<RaceManager>();
            VisualStyleController visualStyle = owner.AddComponent<VisualStyleController>();
            try
            {
                // The accepted workshop builder owns separate legacy coverage and uses play-mode-only destruction.
                for (int i = 1; i < TrackCatalog.Entries.Count; i++)
                {
                    TrackDefinition definition = TrackCatalog.Entries[i];
                    Assert.That(race.TryReconfigureIdleRace(definition.LapCount,
                        definition.OrderedCheckpointCount), Is.True, definition.Id);
                    TrackSession trackSession = new(TrackCatalog.Entries, definition.Id);
                    int registrationsBeforeBuild = visualStyle.RegisteredRendererCount;
                    ITrackRuntime runtime = trackSession.BuildSelected(race, owner.transform, visualStyle);
                    GameObject runtimeRoot = runtime.Root;
                    Assert.That(runtimeRoot, Is.Not.Null, definition.Id);
                    Assert.That(runtime.Grid, Has.Length.EqualTo(4), definition.Id);
                    Assert.That(runtime.Checkpoints, Has.Length.EqualTo(definition.OrderedCheckpointCount), definition.Id);
                    Assert.That(runtime.PickupPositions.Count, Is.EqualTo(4), definition.Id);
                    Assert.That(runtime.DrivableMask.value, Is.EqualTo(1 << PrototypeCircuitBuilder.DrivableLayer), definition.Id);
                    Physics.SyncTransforms();
                    for (int gridIndex = 0; gridIndex < runtime.Grid.Length; gridIndex++)
                        Assert.That(Physics.Raycast(runtime.Grid[gridIndex].position + Vector3.up * 4f,
                                Vector3.down, 10f, runtime.DrivableMask, QueryTriggerInteraction.Ignore),
                            Is.True, $"{definition.Id}/grid {gridIndex + 1} needs drivable support.");
                    for (int checkpointIndex = 0; checkpointIndex < runtime.Checkpoints.Length; checkpointIndex++)
                        Assert.That(Physics.Raycast(runtime.Checkpoints[checkpointIndex].transform.position + Vector3.up * 4f,
                                Vector3.down, 10f, runtime.DrivableMask, QueryTriggerInteraction.Ignore),
                            Is.True, $"{definition.Id}/checkpoint {checkpointIndex} needs safe road support.");
                    for (int pickupIndex = 0; pickupIndex < runtime.PickupPositions.Count; pickupIndex++)
                        Assert.That(Physics.Raycast(runtime.PickupPositions[pickupIndex] + Vector3.up * 4f,
                                Vector3.down, 10f, runtime.DrivableMask, QueryTriggerInteraction.Ignore),
                            Is.True, $"{definition.Id}/pickup {pickupIndex + 1} needs drivable support.");
                    Collider[] colliders = runtimeRoot.GetComponentsInChildren<Collider>(true);
                    int drivable = 0;
                    for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                    {
                        Collider collider = colliders[colliderIndex];
                        if (!collider.enabled || collider.isTrigger) continue;
                        Assert.That(TrackSurfaceMarker.TryResolve(collider, out _), Is.True,
                            $"{definition.Id}/{collider.name} needs explicit surface identity.");
                        if (collider.gameObject.layer == PrototypeCircuitBuilder.DrivableLayer) drivable++;
                    }
                    Assert.That(drivable, Is.GreaterThanOrEqualTo(definition.OrderedCheckpointCount), definition.Id);
                    Assert.That(visualStyle.RegisteredRendererCount, Is.GreaterThan(registrationsBeforeBuild),
                        definition.Id);
                    runtime.Dispose();
                    Assert.That(visualStyle.RegisteredRendererCount, Is.EqualTo(registrationsBeforeBuild),
                        $"{definition.Id} must unregister every owned renderer.");
                    Assert.That(runtime.Root, Is.Null,
                        $"{definition.Id} must release and clear its owned runtime root.");
                    if (runtimeRoot != null) Object.DestroyImmediate(runtimeRoot);
                }
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
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
