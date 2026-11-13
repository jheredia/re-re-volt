using System;
using System.IO;
using NUnit.Framework;
using Presentation;

namespace Tests.EditMode
{
    public sealed class InGameTrackEditorRouteTests
    {
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
    }
}
