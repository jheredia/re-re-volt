using System.Collections;
using Core;
using NUnit.Framework;
using Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Vehicle;

namespace Tests.PlayMode
{
    public sealed class DriverPersonaPresentationIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private RaceManager race;
        private VehicleManager roster;
        private Stage3RacePresentation presentation;
        private Texture2D foundersAtlas;
        private Texture2D neighborsAtlas;
        private DriverPortraitAtlas atlas;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            if (root != null) Object.DestroyImmediate(root);
            if (foundersAtlas != null) Object.DestroyImmediate(foundersAtlas);
            if (neighborsAtlas != null) Object.DestroyImmediate(neighborsAtlas);
            Assert.That(InputUser.all.Count, Is.Zero, "Presentation teardown must not retain input ownership.");
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator OneThroughFourPlayersOwnCanonicalNamesAndSharedAtlasQuadrants()
        {
            CreateTestAtlas();
            CreateHarness(atlas);

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(roster.TryJoinKeyboard(keyboard, out _), Is.True);
            yield return SettleUi();
            AssertPlayerPresentation(1);

            for (int count = 2; count <= 4; count++)
            {
                Gamepad pad = InputSystem.AddDevice<Gamepad>();
                Assert.That(roster.TryJoinGamepad(pad, out _), Is.True);
                yield return SettleUi();
                AssertPlayerPresentation(count);
            }
        }

        [UnityTest]
        public IEnumerator PersonaPresentationPersistsWithoutChangingHandlingOrFamilyIdentity()
        {
            CreateTestAtlas();
            CreateHarness(atlas);

            Assert.That(roster.TryJoinKeyboard(InputSystem.AddDevice<Keyboard>(), out _), Is.True);
            for (int i = 1; i < 4; i++)
                Assert.That(roster.TryJoinGamepad(InputSystem.AddDevice<Gamepad>(), out _), Is.True);
            yield return SettleUi();

            for (int playerIndex = 0; playerIndex < roster.Players.Count; playerIndex++)
            {
                for (int cycle = 0; cycle < playerIndex; cycle++)
                {
                    Assert.That(roster.TryCycleHandlingModel(playerIndex, 1), Is.True);
                    Assert.That(roster.TryCycleVehicleFamily(playerIndex, 1), Is.True);
                }
            }

            HandlingModelId[] handling = new HandlingModelId[4];
            VehicleFamilyId[] families = new VehicleFamilyId[4];
            for (int i = 0; i < 4; i++)
            {
                handling[i] = roster.Players[i].Vehicle.SelectedHandlingModel;
                families[i] = roster.Players[i].FamilyVisual.FamilyId;
            }

            Assert.That(race.StartRace(), Is.True);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            Assert.That(presentation.LobbyVisible, Is.False);
            Assert.That(presentation.HudPortraitCount, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
                Assert.That(FindRawImage($"HudPortrait_P{i + 1}").gameObject.activeInHierarchy, Is.True);

            race.ResetRace();
            yield return null;
            Assert.That(presentation.LobbyVisible, Is.True);
            Assert.That(presentation.LobbyPortraitCount, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(FindRawImage($"LobbyPortrait_P{i + 1}").gameObject.activeInHierarchy, Is.True);
                Assert.That(roster.Players[i].Vehicle.SelectedHandlingModel, Is.EqualTo(handling[i]));
                Assert.That(roster.Players[i].FamilyVisual.FamilyId, Is.EqualTo(families[i]));
                Assert.That(FindHudText(i), Does.Contain(DriverPersonaCatalog.GetForPlayerSlot(i).Callsign));
            }
        }

        [UnityTest]
        public IEnumerator MissingAtlasFallsBackToNamesWithoutCreatingPortraitViews()
        {
            neighborsAtlas = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                { name = "NeighborsOnlyAtlasTestDouble" };
            atlas = new DriverPortraitAtlas(null, neighborsAtlas);
            CreateHarness(atlas);
            Assert.That(roster.TryJoinKeyboard(InputSystem.AddDevice<Keyboard>(), out _), Is.True);
            yield return SettleUi();

            Assert.That(atlas.GetTexture(DriverPortraitSheet.Neighbors), Is.SameAs(neighborsAtlas));
            Assert.That(presentation.PortraitAtlasAvailable, Is.False);
            Assert.That(presentation.LobbyPortraitCount, Is.Zero);
            Assert.That(presentation.HudPortraitCount, Is.Zero);
            Assert.That(presentation.GetComponentsInChildren<RawImage>(true), Is.Empty);
            Assert.That(FindText("PlayersAndControls"), Does.Contain("P1  KEYBOARD  MODEL A  READY  TOURING   MICA"));
            Assert.That(FindHudText(0), Does.Contain("MICA"));
        }

        [UnityTest]
        public IEnumerator LobbyLeaveAndRejoinRemovesDeferredPortraitAndHudViewsByOwnedSlot()
        {
            CreateTestAtlas();
            CreateHarness(atlas);

            Gamepad firstPad = InputSystem.AddDevice<Gamepad>();
            Gamepad secondPad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(firstPad, out LocalPlayer first), Is.True);
            Assert.That(roster.TryJoinGamepad(secondPad, out LocalPlayer second), Is.True);
            Assert.That(first.PlayerIndex, Is.EqualTo(0));
            Assert.That(second.PlayerIndex, Is.EqualTo(1));
            yield return SettleUi();
            AssertExactUiOwnership(0, 1);
            Transform releasedMarker = first.FamilyVisual.PresentationPivot.Find("PlayerIdentityMarker_P1");
            Assert.That(releasedMarker, Is.Not.Null);

            Assert.That(roster.TryLeavePlayer(first.PlayerIndex), Is.True);
            yield return SettleUi();
            Assert.That(releasedMarker == null, Is.True,
                "Releasing a player vehicle must clean up its presentation-only identity marker.");
            Assert.That(roster.Players, Has.Count.EqualTo(1));
            Assert.That(roster.Players[0], Is.SameAs(second));
            AssertExactUiOwnership(1);

            Assert.That(roster.TryJoinGamepad(firstPad, out LocalPlayer rejoined), Is.True);
            Assert.That(rejoined.PlayerIndex, Is.EqualTo(0), "The freed slot must remain the identity owner.");
            yield return SettleUi();
            Assert.That(roster.Players, Has.Count.EqualTo(2));
            Assert.That(roster.Players[0], Is.SameAs(rejoined));
            Assert.That(roster.Players[1], Is.SameAs(second));
            Assert.That(rejoined.FamilyVisual.PresentationPivot.Find("PlayerIdentityMarker_P1"), Is.Not.Null);
            AssertExactUiOwnership(0, 1);
        }

        private void CreateTestAtlas()
        {
            foundersAtlas = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "FoundersAtlasTestDouble" };
            neighborsAtlas = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "NeighborsAtlasTestDouble" };
            atlas = new DriverPortraitAtlas(foundersAtlas, neighborsAtlas);
            Assert.That(atlas.GetTexture(DriverPortraitSheet.Founders), Is.SameAs(foundersAtlas));
            Assert.That(atlas.GetTexture(DriverPortraitSheet.Neighbors), Is.SameAs(neighborsAtlas));
        }

        private void CreateHarness(DriverPortraitAtlas portraitAtlas)
        {
            root = new GameObject("DriverPersonaPresentationTest");
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, 8), Is.True);
            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, null), Is.True);
            presentation = root.AddComponent<Stage3RacePresentation>();
            presentation.Configure(race, roster, null, portraitAtlas);
        }

        private void AssertPlayerPresentation(int playerCount)
        {
            Assert.That(presentation.PortraitAtlasAvailable, Is.True);
            Assert.That(presentation.LobbyPortraitCount, Is.EqualTo(playerCount));
            Assert.That(presentation.HudPortraitCount, Is.EqualTo(playerCount));

            for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                DriverPersonaDefinition persona = DriverPersonaCatalog.GetForPlayerSlot(playerIndex);
                RawImage lobbyPortrait = FindRawImage($"LobbyPortrait_P{playerIndex + 1}");
                RawImage hudPortrait = FindRawImage($"HudPortrait_P{playerIndex + 1}");
                Assert.That(lobbyPortrait.texture, Is.SameAs(foundersAtlas));
                Assert.That(hudPortrait.texture, Is.SameAs(foundersAtlas));
                Assert.That(lobbyPortrait.uvRect, Is.EqualTo(persona.PortraitUv));
                Assert.That(hudPortrait.uvRect, Is.EqualTo(persona.PortraitUv));
                Assert.That(lobbyPortrait.rectTransform.sizeDelta, Is.EqualTo(new Vector2(32f, 32f)));
                Vector2 expectedPortraitSize = playerCount == LocalPlayerRules.MaximumPlayers
                    ? new Vector2(64f, 64f)
                    : new Vector2(88f, 88f);
                Assert.That(hudPortrait.rectTransform.sizeDelta, Is.EqualTo(expectedPortraitSize));
                Assert.That(FindText("PlayersAndControls"), Does.Contain(persona.Callsign));
                Assert.That(FindHudText(playerIndex), Does.Contain(persona.Callsign));
                Assert.That(FindHudText(playerIndex), Does.Contain($"P{playerIndex + 1}"));

                RectTransform viewport = FindRect($"Player_{playerIndex + 1}_HUD");
                RectTransform badge = viewport.Find("WorkshopLabel").GetComponent<RectTransform>();
                Viewport expected = LocalPlayerRules.GetViewport(playerCount, playerIndex);
                Assert.That(viewport.anchorMin, Is.EqualTo(new Vector2(expected.X, expected.Y)));
                Assert.That(viewport.anchorMax,
                    Is.EqualTo(new Vector2(expected.X + expected.Width, expected.Y + expected.Height)));
                Vector2 expectedBadgeSize = playerCount == LocalPlayerRules.MaximumPlayers
                    ? new Vector2(420f, 88f)
                    : new Vector2(540f, 108f);
                Assert.That(badge.sizeDelta, Is.EqualTo(expectedBadgeSize));
                Assert.That(badge.anchoredPosition.x + badge.sizeDelta.x,
                    Is.LessThan(expected.Width * 1920f), "HUD badge must stay inside its viewport width.");
                Assert.That(-badge.anchoredPosition.y + badge.sizeDelta.y,
                    Is.LessThan(expected.Height * 1080f), "HUD badge must stay inside its viewport height.");
            }

            foreach (RawImage portrait in presentation.GetComponentsInChildren<RawImage>(true))
                Assert.That(portrait.texture, Is.SameAs(foundersAtlas),
                    "Every default-slot portrait must share the founders atlas texture.");
        }

        private void AssertExactUiOwnership(params int[] occupiedSlots)
        {
            Transform[] allTransforms = presentation.GetComponentsInChildren<Transform>(true);
            RawImage[] allPortraits = presentation.GetComponentsInChildren<RawImage>(true);
            int hudRootCount = 0;
            int lobbyPortraitCount = 0;
            int hudPortraitCount = 0;

            for (int i = 0; i < allTransforms.Length; i++)
            {
                string objectName = allTransforms[i].gameObject.name;
                if (objectName.StartsWith("Player_") && objectName.EndsWith("_HUD")) hudRootCount++;
                if (objectName.StartsWith("LobbyPortrait_P")) lobbyPortraitCount++;
                if (objectName.StartsWith("HudPortrait_P")) hudPortraitCount++;
            }

            Assert.That(hudRootCount, Is.EqualTo(occupiedSlots.Length));
            Assert.That(lobbyPortraitCount, Is.EqualTo(occupiedSlots.Length));
            Assert.That(hudPortraitCount, Is.EqualTo(occupiedSlots.Length));
            Assert.That(allPortraits, Has.Length.EqualTo(occupiedSlots.Length * 2));

            for (int slot = 0; slot < DriverPersonaCatalog.DefaultPlayerSlotCount; slot++)
            {
                bool occupied = System.Array.IndexOf(occupiedSlots, slot) >= 0;
                Assert.That(CountNamed(allTransforms, $"Player_{slot + 1}_HUD"),
                    Is.EqualTo(occupied ? 1 : 0));
                Assert.That(CountNamed(allTransforms, $"LobbyPortrait_P{slot + 1}"),
                    Is.EqualTo(occupied ? 1 : 0));
                Assert.That(CountNamed(allTransforms, $"HudPortrait_P{slot + 1}"),
                    Is.EqualTo(occupied ? 1 : 0));
                if (!occupied) continue;

                DriverPersonaDefinition persona = DriverPersonaCatalog.GetForPlayerSlot(slot);
                Assert.That(FindRawImage($"LobbyPortrait_P{slot + 1}").uvRect, Is.EqualTo(persona.PortraitUv));
                Assert.That(FindRawImage($"HudPortrait_P{slot + 1}").uvRect, Is.EqualTo(persona.PortraitUv));
                Assert.That(FindHudText(slot), Does.Contain(persona.Callsign));
            }
        }

        private static int CountNamed(Transform[] transforms, string objectName)
        {
            int count = 0;
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i].gameObject.name == objectName) count++;
            return count;
        }

        private RawImage FindRawImage(string objectName)
        {
            foreach (RawImage image in presentation.GetComponentsInChildren<RawImage>(true))
                if (image.gameObject.name == objectName) return image;
            Assert.Fail($"Could not find portrait object {objectName}.");
            return null;
        }

        private RectTransform FindRect(string objectName)
        {
            foreach (RectTransform rect in presentation.GetComponentsInChildren<RectTransform>(true))
                if (rect.gameObject.name == objectName) return rect;
            Assert.Fail($"Could not find RectTransform {objectName}.");
            return null;
        }

        private string FindText(string objectName)
        {
            foreach (TextMeshProUGUI label in presentation.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (label.gameObject.name == objectName) return label.text;
            Assert.Fail($"Could not find text object {objectName}.");
            return null;
        }

        private string FindHudText(int playerIndex)
        {
            RectTransform viewport = FindRect($"Player_{playerIndex + 1}_HUD");
            TextMeshProUGUI label = viewport.GetComponentInChildren<TextMeshProUGUI>(true);
            Assert.That(label, Is.Not.Null);
            return label.text;
        }

        private static IEnumerator SettleUi()
        {
            yield return null;
            yield return null;
        }
    }
}
