using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Presentation;
using UnityEngine;

namespace Tests.EditMode
{
    public sealed class InGameTrackEditorRouteTests
    {
        private sealed class MemoryPreferences : ILocalPreferenceStore
        {
            private readonly Dictionary<string, string> values = new();
            public bool TryRead(string key, out string value) => values.TryGetValue(key, out value);
            public void Write(string key, string value) => values[key] = value;
        }

        [Test]
        public void PreviewAndAutoCloseResolveStartWithoutChangingDraftUntilPlaced()
        {
            InGameTrackDraft draft = InGameTrackDraft.Create();
            InGameTrackLayout layout = draft.Layout;
            string[] road =
            {
                "straight-short", "corner-left-wide", "straight-long", "straight-short",
                "corner-left-wide", "straight-long", "straight-short", "straight-short",
                "corner-left-wide", "straight-long", "straight-short", "corner-left-wide"
            };
            foreach (string id in road)
                Assert.That(layout.TryAppend(id, TrackRoadSurface.Standard, out string error),
                    Is.True, id + ": " + error);

            int originalCount = layout.Route.Count;
            Assert.That(layout.IsClosed, Is.False);
            Assert.That(layout.TryPreview(InGameTrackKit.CloseId, TrackRoadSurface.Standard,
                out TrackPiecePlacement preview, out string previewError),
                Is.True, previewError);
            Assert.That(preview.Entry, Is.EqualTo(layout.OpenEnd));
            Assert.That(preview.Exit, Is.EqualTo(layout.Route[0].Entry));
            Assert.That(layout.Route.Count, Is.EqualTo(originalCount), "A preview must not place road.");
            Assert.That(layout.TryAppend(InGameTrackKit.CloseId, TrackRoadSurface.Standard,
                out string placeError), Is.True, placeError);
            Assert.That(layout.IsClosed, Is.True);
            Assert.That(layout.ValidateTopology(), Is.Empty);
            Assert.That(InGameTrackRouteValidator.Validate(draft), Is.Empty,
                "A joined route must also pass the playable-road clearance gate.");
        }

        [Test]
        public void ExistingKitOneDraftLoadsWithoutChangingItsPieces()
        {
            string folder = Path.Combine(Path.GetTempPath(), "rc-track-kit-migration-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new InGameTrackDraftStore(storageDirectory: folder);
                InGameTrackDraft draft = InGameTrackDraft.Create();
                Assert.That(draft.Layout.TryAppend("corner-left-tight", TrackRoadSurface.Slick,
                    out string error), Is.True, error);
                Assert.That(store.TrySave(draft, out error), Is.True, error);
                string path = Path.Combine(folder, draft.LocalId + ".json");
                File.WriteAllText(path, File.ReadAllText(path).Replace(InGameTrackKit.Version,
                    InGameTrackKit.PreviousVersion));

                Assert.That(store.TryLoad(draft.LocalId, out InGameTrackDraft loaded, out error),
                    Is.True, error);
                Assert.That(loaded.Layout.Route.Count, Is.EqualTo(draft.Layout.Route.Count));
                Assert.That(loaded.Layout.Route[1].Spec.Id, Is.EqualTo("corner-left-tight"));
                Assert.That(loaded.Layout.Route[1].Exit,
                    Is.EqualTo(draft.Layout.Route[1].Exit));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void SavedLocalDraftCanBecomePersistedTestDriveSelection()
        {
            InGameTrackDraft draft = InGameTrackDraft.Create();
            string[] road =
            {
                "straight-short", "corner-left-wide", "straight-long", "straight-short",
                "corner-left-wide", "straight-long", "straight-short", "straight-short",
                "corner-left-wide", "straight-long", "straight-short", "corner-left-wide"
            };
            foreach (string piece in road)
                Assert.That(draft.Layout.TryAppend(piece, TrackRoadSurface.Standard, out string error),
                    Is.True, error);
            Assert.That(draft.Layout.TryAppend(InGameTrackKit.CloseId, TrackRoadSurface.Standard,
                out string closeError), Is.True, closeError);
            string path = Path.Combine(Application.persistentDataPath, "TrackDrafts", draft.LocalId + ".json");
            try
            {
                var drafts = new InGameTrackDraftStore();
                Assert.That(drafts.TrySave(draft, out string saveError), Is.True, saveError);
                string id = TrackCatalog.LocalDraftTrackId(draft.LocalId);
                Assert.That(TrackCatalog.IsLocalAvailable(id), Is.True);
                var storage = new MemoryPreferences();
                var preferences = new DemoFlowPreferences(storage);
                Assert.That(preferences.SetTrackId(id), Is.True);
                var reloaded = new DemoFlowPreferences(storage);
                reloaded.Load();
                Assert.That(reloaded.TrackId, Is.EqualTo(id));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        }

        [Test]
        public void RoadOverlapReportsTheSamePieceIndicesTheMapShows()
        {
            var layout = new InGameTrackLayout();
            Assert.That(layout.TryAppend("corner-left-tight", TrackRoadSurface.Standard,
                out string firstError), Is.True, firstError);
            Assert.That(layout.TryAppend("corner-left-tight", TrackRoadSurface.Standard,
                out string secondError), Is.True, secondError);
            var overlaps = InGameTrackRouteValidator.FindRoadOverlaps(layout);
            Assert.That(overlaps, Is.Not.Empty);
            Assert.That(overlaps[0].First + 1, Is.EqualTo(1));
            Assert.That(overlaps[0].Second + 1, Is.EqualTo(3));
            Assert.That(InGameTrackRouteValidator.MinimumDeckClearance, Is.EqualTo(3f));
        }
    }
}
