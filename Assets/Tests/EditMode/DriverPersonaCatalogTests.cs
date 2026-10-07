using NUnit.Framework;
using Presentation;
using UnityEngine;

namespace Tests.EditMode
{
    public sealed class DriverPersonaCatalogTests
    {
        [Test]
        public void CatalogExposesExactlySevenOriginalPersonasInStableOrder()
        {
            Assert.That(DriverPersonaCatalog.Count, Is.EqualTo(7));
            Assert.That(DriverPersonaCatalog.DefaultPlayerSlotCount, Is.EqualTo(4));
            Assert.That(DriverPersonaCatalog.Definitions, Has.Count.EqualTo(7));

            AssertPersona(0, DriverPersonaId.Mica, "MICA", "Pit-bench fabricator and setup lead.",
                DriverPortraitSheet.Founders, new Rect(0f, .5f, .5f, .5f));
            AssertPersona(1, DriverPersonaId.Beto, "BETO", "Club mechanic and safety scrutineer.",
                DriverPortraitSheet.Founders, new Rect(.5f, .5f, .5f, .5f));
            AssertPersona(2, DriverPersonaId.Noa, "NOA", "Radio marshal and audio tinkerer.",
                DriverPortraitSheet.Founders, new Rect(0f, 0f, .5f, .5f));
            AssertPersona(3, DriverPersonaId.Luz, "LUZ", "Course builder and night-race marshal.",
                DriverPortraitSheet.Founders, new Rect(.5f, 0f, .5f, .5f));
            AssertPersona(4, DriverPersonaId.Tavi, "TAVI", "Junior electronics tinkerer and battery runner.",
                DriverPortraitSheet.Neighbors, new Rect(0f, .5f, .5f, .5f));
            AssertPersona(5, DriverPersonaId.Cata, "CATA", "Family-pit host and race-day coordinator.",
                DriverPortraitSheet.Neighbors, new Rect(.5f, .5f, .5f, .5f));
            AssertPersona(6, DriverPersonaId.Vale, "VALE", "Body-shell painter and livery restorer.",
                DriverPortraitSheet.Neighbors, new Rect(0f, 0f, .5f, .5f));
        }

        [Test]
        public void SlotAssignmentAndQuadrantsAreDeterministicAndRejectUnknownValues()
        {
            for (int slot = 0; slot < DriverPersonaCatalog.DefaultPlayerSlotCount; slot++)
            {
                DriverPersonaDefinition bySlot = DriverPersonaCatalog.GetForPlayerSlot(slot);
                Assert.That(bySlot, Is.SameAs(DriverPersonaCatalog.Definitions[slot]));
                Assert.That(DriverPersonaCatalog.GetDefinition(bySlot.Id), Is.SameAs(bySlot));
                Assert.That(DriverPersonaCatalog.IsDefined(bySlot.Id), Is.True);
            }

            Assert.That(DriverPersonaCatalog.Definitions[0].PortraitUv.y, Is.EqualTo(.5f), "Mica is top-left.");
            Assert.That(DriverPersonaCatalog.Definitions[1].PortraitUv.y, Is.EqualTo(.5f), "Beto is top-right.");
            Assert.That(DriverPersonaCatalog.Definitions[2].PortraitUv.y, Is.Zero, "Noa is bottom-left.");
            Assert.That(DriverPersonaCatalog.Definitions[3].PortraitUv.y, Is.Zero, "Luz is bottom-right.");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DriverPersonaCatalog.GetForPlayerSlot(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DriverPersonaCatalog.GetForPlayerSlot(4));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                DriverPersonaCatalog.GetDefinition((DriverPersonaId)99));
            Assert.That(DriverPersonaCatalog.IsDefined((DriverPersonaId)99), Is.False);

            for (int index = 4; index < DriverPersonaCatalog.Count; index++)
            {
                DriverPersonaDefinition laterSelection = DriverPersonaCatalog.Definitions[index];
                Assert.That(laterSelection.PortraitSheet, Is.EqualTo(DriverPortraitSheet.Neighbors));
                Assert.That(laterSelection.PortraitUv, Is.Not.EqualTo(new Rect(.5f, 0f, .5f, .5f)),
                    "The neighbors atlas bottom-right quadrant is intentionally empty.");
            }
        }

        [Test]
        public void TwoSheetAtlasUsesStableResourcesKeysAndExactTextureOwnership()
        {
            Assert.That(DriverPortraitAtlas.GetResourcePath(DriverPortraitSheet.Founders),
                Is.EqualTo("UI/DriverPortraitAtlas"));
            Assert.That(DriverPortraitAtlas.GetResourcePath(DriverPortraitSheet.Neighbors),
                Is.EqualTo("UI/DriverPortraitAtlasNeighbors"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                DriverPortraitAtlas.GetResourcePath((DriverPortraitSheet)99));

            Texture2D founders = new Texture2D(2, 2);
            Texture2D neighbors = new Texture2D(2, 2);
            try
            {
                DriverPortraitAtlas atlas = new DriverPortraitAtlas(founders, neighbors);
                Assert.That(atlas.GetTexture(DriverPortraitSheet.Founders), Is.SameAs(founders));
                Assert.That(atlas.GetTexture(DriverPortraitSheet.Founders), Is.SameAs(founders));
                Assert.That(atlas.GetTexture(DriverPortraitSheet.Neighbors), Is.SameAs(neighbors));
                Assert.That(atlas.GetTexture(DriverPortraitSheet.Neighbors), Is.SameAs(neighbors));
                Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                    atlas.GetTexture((DriverPortraitSheet)99));
            }
            finally
            {
                Object.DestroyImmediate(founders);
                Object.DestroyImmediate(neighbors);
            }
        }

        [Test]
        public void BothRuntimePortraitAtlasesLoadAtAuthoredDimensions()
        {
            Texture2D founders = Resources.Load<Texture2D>(
                DriverPortraitAtlas.GetResourcePath(DriverPortraitSheet.Founders));
            Texture2D neighbors = Resources.Load<Texture2D>(
                DriverPortraitAtlas.GetResourcePath(DriverPortraitSheet.Neighbors));

            Assert.That(founders, Is.Not.Null);
            Assert.That(neighbors, Is.Not.Null);
            Assert.That(founders, Is.Not.SameAs(neighbors));
            Assert.That(founders.width, Is.EqualTo(1024));
            Assert.That(founders.height, Is.EqualTo(1024));
            Assert.That(neighbors.width, Is.EqualTo(1024));
            Assert.That(neighbors.height, Is.EqualTo(1024));
        }

        private static void AssertPersona(int index, DriverPersonaId id, string callsign, string role,
            DriverPortraitSheet sheet, Rect uv)
        {
            DriverPersonaDefinition persona = DriverPersonaCatalog.Definitions[index];
            Assert.That(persona.Id, Is.EqualTo(id));
            Assert.That(persona.Callsign, Is.EqualTo(callsign));
            Assert.That(persona.ClubRole, Is.EqualTo(role));
            Assert.That(persona.PortraitSheet, Is.EqualTo(sheet));
            Assert.That(persona.PortraitUv, Is.EqualTo(uv));
            Assert.That(DriverPersonaCatalog.GetDefinition(id), Is.SameAs(persona));
            Assert.That(DriverPersonaCatalog.IsDefined(id), Is.True);
        }
    }
}
