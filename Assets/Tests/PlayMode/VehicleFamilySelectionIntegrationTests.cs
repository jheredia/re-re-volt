using System.Collections;
using Core;
using NUnit.Framework;
using Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using Vehicle;

namespace Tests.PlayMode
{
    public sealed class VehicleFamilySelectionIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private VisualStyleController style;
        private RaceManager race;
        private VehicleManager roster;
        private Stage3RacePresentation presentation;
        private Keyboard keyboard;
        private GameObject bootstrapGameManager;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            root = new GameObject("VehicleFamilySelectionTest");
            style = root.AddComponent<VisualStyleController>();
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(1, 1), Is.True);
            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, null, null, null, style), Is.True);
            presentation = root.AddComponent<Stage3RacePresentation>();
            presentation.Configure(race, roster, style);
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            if (root != null) Object.DestroyImmediate(root);
            if (bootstrapGameManager != null) Object.DestroyImmediate(bootstrapGameManager);
            Assert.That(InputUser.all.Count, Is.Zero);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator OwnedInputsWrapFamiliesPreserveColorAndReplaceOnlyFamilyRegistrations()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer keyboardPlayer), Is.True);
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer gamepadPlayer), Is.True);
            yield return Frames();
            AssertSingleEnabledAudioListener("after joining two players with the configured camera fallback");

            VehicleController vehicle = keyboardPlayer.Vehicle;
            Rigidbody body = vehicle.GetComponent<Rigidbody>();
            InputHandler input = keyboardPlayer.Input;
            RacerState racer = keyboardPlayer.Racer;
            ChaseCamera chase = keyboardPlayer.ChaseCamera;
            HandlingModelId handling = vehicle.SelectedHandlingModel;
            Renderer wheelVisual = vehicle.transform.Find("Wheel_FL/WheelVisual").GetComponent<Renderer>();
            Transform wheelOutline = wheelVisual.transform.Find("WheelVisual_InkOutline");
            Assert.That(wheelOutline, Is.Not.Null);

            Color playerColor = new Color(.17f, .64f, .91f);
            ApplyColor(keyboardPlayer.FamilyVisual.ColorableBodyRenderers, playerColor);
            int oldFamilyRendererCount = keyboardPlayer.FamilyVisual.SourceRenderers.Count;
            int registeredBefore = style.RegisteredRendererCount;
            int outlinesBefore = style.OutlineObjectCount;
            int changes = 0;
            LocalPlayer lastChanged = null;
            roster.VehicleFamilyChanged += changed =>
            {
                lastChanged = changed;
                changes++;
            };

            yield return Press(Key.W);

            Assert.That(keyboardPlayer.FamilyVisual.FamilyId, Is.EqualTo(VehicleFamilyId.Drift),
                "Keyboard W/Up selects the previous family and wraps Touring to Drift.");
            Assert.That(gamepadPlayer.FamilyVisual.FamilyId, Is.EqualTo(VehicleFamilyId.Touring));
            Assert.That(changes, Is.EqualTo(1), "One successful swap must raise exactly one notification.");
            Assert.That(lastChanged, Is.SameAs(keyboardPlayer));
            int newFamilyRendererCount = keyboardPlayer.FamilyVisual.SourceRenderers.Count;
            Assert.That(style.RegisteredRendererCount,
                Is.EqualTo(registeredBefore - oldFamilyRendererCount + newFamilyRendererCount));
            Assert.That(style.OutlineObjectCount,
                Is.EqualTo(outlinesBefore - oldFamilyRendererCount + newFamilyRendererCount));
            Assert.That(wheelVisual.transform.Find("WheelVisual_InkOutline"), Is.SameAs(wheelOutline),
                "Family replacement must not enumerate or rebuild controller-owned wheel outlines.");
            foreach (Renderer colorable in keyboardPlayer.FamilyVisual.ColorableBodyRenderers)
                AssertPropertyColor(colorable, playerColor);

            Assert.That(keyboardPlayer.Vehicle, Is.SameAs(vehicle));
            Assert.That(vehicle.GetComponent<Rigidbody>(), Is.SameAs(body));
            Assert.That(keyboardPlayer.Input, Is.SameAs(input));
            Assert.That(keyboardPlayer.Racer, Is.SameAs(racer));
            Assert.That(keyboardPlayer.ChaseCamera, Is.SameAs(chase));
            Assert.That(vehicle.SelectedHandlingModel, Is.EqualTo(handling));

            yield return PressPad(pad, GamepadButton.DpadDown);
            Assert.That(keyboardPlayer.FamilyVisual.FamilyId, Is.EqualTo(VehicleFamilyId.Drift));
            Assert.That(gamepadPlayer.FamilyVisual.FamilyId, Is.EqualTo(VehicleFamilyId.Buggy),
                "Only the player paired to the pressed d-pad may change family.");
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(lastChanged, Is.SameAs(gamepadPlayer));

            string lobbyText = FindText("PlayersAndControls");
            Assert.That(lobbyText, Does.Contain("P1  KEYBOARD  MODEL A  READY  DRIFT"));
            Assert.That(lobbyText, Does.Contain("P2  GAMEPAD  MODEL A  READY  BUGGY"));
            Assert.That(lobbyText, Does.Contain("LOBBY CAR: UP/DOWN OR W/S"));
        }

        [UnityTest]
        public IEnumerator FamilySelectionIsIdleConnectedOnlyAndPersistsAcrossRematchAndLobbyReturn()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer first), Is.True);
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer second), Is.True);
            yield return Frames();
            Assert.That(roster.TryCycleVehicleFamily(first.PlayerIndex, 1), Is.True);
            Assert.That(roster.TryCycleVehicleFamily(second.PlayerIndex, -1), Is.True);
            AssertFamilies(VehicleFamilyId.Buggy, VehicleFamilyId.Drift);
            int changesWhileDisconnected = 0;
            roster.VehicleFamilyChanged += _ => changesWhileDisconnected++;

            InputSystem.RemoveDevice(pad);
            yield return Frames();
            Assert.That(second.IsConnected, Is.False);
            Assert.That(roster.TryCycleVehicleFamily(first.PlayerIndex, 1), Is.False,
                "No player may change lobby identity while another roster device is disconnected.");
            Assert.That(roster.TryCycleVehicleFamily(second.PlayerIndex, 1), Is.False,
                "An idle but disconnected player cannot change identity.");
            yield return Press(Key.S);
            AssertFamilies(VehicleFamilyId.Buggy, VehicleFamilyId.Drift);
            Assert.That(changesWhileDisconnected, Is.Zero,
                "Rejected direct and owned-input requests must not emit family changes.");
            InputSystem.AddDevice(pad);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Frames();
            Assert.That(second.IsConnected, Is.True);
            AssertFamilies(VehicleFamilyId.Buggy, VehicleFamilyId.Drift);

            yield return Press(Key.Enter);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            Assert.That(FindText("RaceData"), Does.Contain("P1   MODEL A   BUGGY"));
            Assert.That(roster.TryCycleVehicleFamily(first.PlayerIndex, 1), Is.False);
            yield return Press(Key.S);
            AssertFamilies(VehicleFamilyId.Buggy, VehicleFamilyId.Drift);
            yield return WaitForRacing();
            Assert.That(roster.TryCycleVehicleFamily(first.PlayerIndex, 1), Is.False);

            yield return Press(Key.Escape);
            Assert.That(presentation.IsPaused, Is.True);
            Assert.That(roster.TryCycleVehicleFamily(second.PlayerIndex, 1), Is.False);
            yield return Press(Key.Escape);
            Assert.That(presentation.IsPaused, Is.False);

            Finish(first);
            Finish(second);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
            Assert.That(roster.TryCycleVehicleFamily(first.PlayerIndex, 1), Is.False);
            AssertFamilies(VehicleFamilyId.Buggy, VehicleFamilyId.Drift);

            yield return Press(Key.Enter);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            AssertFamilies(VehicleFamilyId.Buggy, VehicleFamilyId.Drift);
            yield return WaitForRacing();
            Finish(first);
            Finish(second);
            yield return Press(Key.Escape);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Idle));
            AssertFamilies(VehicleFamilyId.Buggy, VehicleFamilyId.Drift);
            Assert.That(FindText("PlayersAndControls"), Does.Contain("MODEL A  READY  BUGGY"));
        }

        [UnityTest]
        public IEnumerator BootstrapPlayerJoinedColorsMarkerlessAuthoredBody()
        {
            Object.DestroyImmediate(root);
            root = new GameObject("MarkerlessBootstrapTest");
            Stage3Bootstrap bootstrap = root.AddComponent<Stage3Bootstrap>();
            Assert.That(bootstrap.InitializeNow(), Is.True);
            AssertSingleEnabledAudioListener("after bootstrap initialization");
            bootstrapGameManager = GameManager.Instance != null ? GameManager.Instance.gameObject : null;

            GameObject authoredPrefab = PrototypeVehicleFactory.Create(
                new Pose(new Vector3(1000f, 1000f, 1000f), Quaternion.identity));
            authoredPrefab.name = "MarkerlessAuthoredVehicle";
            authoredPrefab.transform.SetParent(root.transform, true);
            Object.DestroyImmediate(authoredPrefab.GetComponent<VehicleFamilyVisual>());
            Transform generatedPivot = authoredPrefab.transform.Find("PresentationVisualPivot");
            Assert.That(generatedPivot, Is.Not.Null);
            Transform directBody = generatedPivot.Find("Body");
            Assert.That(directBody, Is.Not.Null);
            directBody.SetParent(authoredPrefab.transform, false);
            Assert.That(authoredPrefab.GetComponent<VehicleController>().IsConfigured, Is.True);
            Assert.That(authoredPrefab.GetComponent<VehicleFamilyVisual>(), Is.Null);
            Camera lobbyCamera = Camera.main != null
                ? Camera.main
                : Object.FindAnyObjectByType<Camera>();
            Assert.That(bootstrap.VehicleManager.Configure(bootstrap.RaceManager, bootstrap.Grid, authoredPrefab,
                lobbyCamera, bootstrap.VisualStyle), Is.True);
            AssertSingleEnabledAudioListener("after roster reconfiguration");

            Assert.That(bootstrap.VehicleManager.TryJoinKeyboard(keyboard, out LocalPlayer player), Is.True);
            AssertSingleEnabledAudioListener("after the first player joins");
            Assert.That(player.FamilyVisual, Is.Null);
            Transform authoredBody = player.Vehicle.transform.Find("Body");
            Assert.That(authoredBody, Is.Not.Null,
                "Markerless authored vehicles expose the legacy direct Body color seam.");
            Renderer body = authoredBody.GetComponent<Renderer>();
            Assert.That(body, Is.Not.Null, "The authored Body must remain renderable after joining.");
            AssertPropertyColor(body, new Color(.95f, .2f, .12f));
            yield return null;
        }

        private void AssertFamilies(params VehicleFamilyId[] expected)
        {
            Assert.That(roster.Players.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(roster.Players[i].FamilyVisual.FamilyId, Is.EqualTo(expected[i]),
                    $"Unexpected vehicle family for player {i + 1}.");
        }

        private void Finish(LocalPlayer player)
        {
            Assert.That(race.ReportCheckpoint(player.Racer.RacerId, 0), Is.True);
        }

        private IEnumerator WaitForRacing()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);
        }

        private IEnumerator Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames();
        }

        private static IEnumerator PressPad(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return Frames();
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Frames();
        }

        private string FindText(string objectName)
        {
            foreach (TextMeshProUGUI label in presentation.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (label.gameObject.name == objectName) return label.text;
            Assert.Fail($"Could not find text object {objectName}.");
            return null;
        }

        private static void ApplyColor(System.Collections.Generic.IReadOnlyList<Renderer> renderers, Color color)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            for (int i = 0; i < renderers.Count; i++) renderers[i].SetPropertyBlock(properties);
        }

        private static void AssertPropertyColor(Renderer renderer, Color expected)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            ColorEqualityComparer comparer = new ColorEqualityComparer(.00001f);
            Assert.That(properties.GetColor("_BaseColor"), Is.EqualTo(expected).Using(comparer));
            Assert.That(properties.GetColor("_Color"), Is.EqualTo(expected).Using(comparer));
        }

        private static void AssertSingleEnabledAudioListener(string checkpoint)
        {
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            var enabledNames = new System.Collections.Generic.List<string>();
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i].isActiveAndEnabled) enabledNames.Add(listeners[i].gameObject.name);
            Assert.That(enabledNames, Has.Count.EqualTo(1),
                $"Expected one active listener {checkpoint}; found: {string.Join(", ", enabledNames)}");
        }

        private static IEnumerator Frames()
        {
            yield return null;
            yield return null;
        }
    }
}
