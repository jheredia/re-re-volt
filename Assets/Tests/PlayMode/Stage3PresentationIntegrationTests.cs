using System.Collections;
using System.Collections.Generic;
using System.IO;
using AI;
using Core;
using NUnit.Framework;
using Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Vehicle;

namespace Tests.PlayMode
{
    public sealed class Stage3PresentationIntegrationTests : InputTestFixture
    {
        private const string ArtifactPrefix = "/private/tmp/re-re-volt-stage3-";
        private GameObject root;
        private RaceManager race;
        private VehicleManager roster;
        private AIManager ai;
        private Stage3RacePresentation presentation;
        private VisualStyleController visualStyle;
        private PrototypeCircuitBuilder.Result circuit;
        private Camera lobbyCamera;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            root = new GameObject("Stage3PresentationTest");
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, PrototypeCircuitBuilder.OrderedCheckpointCount), Is.True);
            circuit = PrototypeCircuitBuilder.Build(race, root.transform);
            roster = root.AddComponent<VehicleManager>();
            GameObject cameraObject = new GameObject("Stage3TestLobbyCamera");
            cameraObject.transform.SetParent(root.transform, false);
            lobbyCamera = cameraObject.AddComponent<Camera>();
            lobbyCamera.clearFlags = CameraClearFlags.SolidColor;
            lobbyCamera.backgroundColor = new Color(.045f, .065f, .07f);
            lobbyCamera.fieldOfView = 54f;
            lobbyCamera.transform.position = new Vector3(0f, 45f, -39f);
            lobbyCamera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, 1.5f) - lobbyCamera.transform.position);
            Assert.That(roster.Configure(race, circuit.Grid, null, lobbyCamera), Is.True);
            roster.PlayerJoined += player => player.Recovery.ConfigureGroundMask(circuit.DrivableMask);
            ai = root.AddComponent<AIManager>();
            Assert.That(ai.Configure(race, roster, circuit.Grid, circuit.Checkpoints, circuit.DrivableMask), Is.True);
            visualStyle = root.AddComponent<VisualStyleController>();
            presentation = root.AddComponent<Stage3RacePresentation>();
            presentation.Configure(race, roster, visualStyle, aiManager: ai);
            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
            circuit?.Dispose();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator ControllerConfirmationStartsConfiguredThreeLapRaceAndBuildsSplitHud()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out _), Is.True);
            yield return WaitForPlayerLoop();
            Assert.That(presentation.HudCount, Is.EqualTo(1));
            Assert.That(presentation.LobbyVisible, Is.True);

            yield return PressAndRelease(pad, GamepadButton.South);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            Assert.That(race.TotalLaps, Is.EqualTo(3));
            Assert.That(race.CheckpointCount, Is.EqualTo(8));
            Assert.That(roster.AllowControllerJoining, Is.False);
        }

        [UnityTest]
        public IEnumerator PauseDisconnectResultsAndRematchRemainControllerDriven()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer player), Is.True);
            Assert.That(race.StartRace(), Is.True);
            // Each countdown yield resumes on a frame; editor stalls can exceed a fixed 3.2-second sleep.
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True, "Countdown must enter Racing within 10 seconds.");

            yield return PressAndRelease(pad, GamepadButton.Start);
            Assert.That(presentation.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(presentation.ModalVisible, Is.True);
            yield return PressAndRelease(pad, GamepadButton.Start);
            Assert.That(presentation.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            InputSystem.RemoveDevice(pad);
            yield return WaitForPlayerLoop();
            Assert.That(player.IsConnected, Is.False);
            Assert.That(presentation.IsPaused, Is.True);
            Assert.That(presentation.ModalVisible, Is.True);
            InputSystem.AddDevice(pad);
            yield return WaitForPlayerLoop();
            Assert.That(player.IsConnected, Is.True);
            Assert.That(presentation.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            for (int lap = 0; lap < race.TotalLaps; lap++)
                for (int checkpoint = 0; checkpoint < race.CheckpointCount; checkpoint++)
                    Assert.That(race.ReportCheckpoint(player.Racer.RacerId, checkpoint), Is.True);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
            Assert.That(presentation.ModalVisible, Is.True);

            yield return PressAndRelease(pad, GamepadButton.South);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            Assert.That(player.Racer.CompletedLaps, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CaptureRequiredStage3VisualReviewStates()
        {
            yield return Capture("wide-circuit", hidePresentation: true);
            yield return Capture("lobby", hidePresentation: false);

            var pads = new List<Gamepad>();
            Gamepad first = InputSystem.AddDevice<Gamepad>();
            pads.Add(first);
            Assert.That(roster.TryJoinGamepad(first, out _), Is.True);
            yield return WaitForPlayerLoop();
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Setup));
            AssertSetupContentFits(1, "AI TEST CARS  0 / 3");
            yield return WaitForPlayerLoop();
            yield return Capture("setup-selections", hidePresentation: false);
            yield return PressAndRelease(first, GamepadButton.South);
            yield return new WaitForSecondsRealtime(4.1f);
            Assert.That(race.RaceRunning, Is.True);
            yield return Capture("one-player-hud", hidePresentation: false);

            race.ResetRace();
            yield return WaitForPlayerLoop();
            for (int i = 1; i < 4; i++)
            {
                Gamepad pad = InputSystem.AddDevice<Gamepad>();
                pads.Add(pad);
                Assert.That(roster.TryJoinGamepad(pad, out _), Is.True);
            }
            yield return WaitForPlayerLoop();
            Assert.That(presentation.HudCount, Is.EqualTo(4));
            Assert.That(presentation.CurrentPage, Is.EqualTo(Stage3RacePresentation.DemoFlowPage.Setup));
            AssertSetupContentFits(4, "AI TEST CARS  0 / 0");
            yield return PressAndRelease(first, GamepadButton.South);
            yield return new WaitForSecondsRealtime(4.1f);
            Assert.That(race.RaceRunning, Is.True);
            AssertCompactFourPlayerIdentity();
            yield return Capture("four-player-split", hidePresentation: false);
            visualStyle.SetMode(VisualStyleMode.InkLite);
            Assert.That(visualStyle.Mode, Is.EqualTo(VisualStyleMode.InkLite));
            AssertCompactFourPlayerIdentity();
            yield return Capture("four-player-split-ink-lite", hidePresentation: false);
            visualStyle.SetMode(VisualStyleMode.FullInk);
            Assert.That(visualStyle.Mode, Is.EqualTo(VisualStyleMode.FullInk));
            AssertCompactFourPlayerIdentity();
            yield return Capture("four-player-split-full-ink", hidePresentation: false);
            visualStyle.SetMode(VisualStyleMode.Clean3D);

            for (int playerIndex = 0; playerIndex < roster.Players.Count; playerIndex++)
            {
                string racerId = roster.Players[playerIndex].Racer.RacerId;
                for (int lap = 0; lap < race.TotalLaps; lap++)
                    for (int checkpoint = 0; checkpoint < race.CheckpointCount; checkpoint++)
                        Assert.That(race.ReportCheckpoint(racerId, checkpoint), Is.True);
            }
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
            Assert.That(presentation.ModalVisible, Is.True);
            yield return Capture("results-rematch", hidePresentation: false);
        }

        private IEnumerator Capture(string suffix, bool hidePresentation)
        {
            string path = ArtifactPrefix + suffix + ".png";
            if (File.Exists(path)) File.Delete(path);
            presentation.PresentationCanvas.enabled = !hidePresentation;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();

            if (!hidePresentation && presentation.HudCount == LocalPlayerRules.MaximumPlayers && race.RaceRunning)
            {
                // Four split cameras alternate the Editor readback buffer that contains the overlay canvas.
                // Prime that buffer once, then rebuild and save the following fully rendered frame.
                Texture2D warmup = ScreenCapture.CaptureScreenshotAsTexture();
                Assert.That(warmup, Is.Not.Null);
                Object.Destroy(warmup);
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
            }

            // Read the fully rendered frame synchronously so a queued PNG write cannot overlap the next style state.
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.That(screenshot, Is.Not.Null);
            File.WriteAllBytes(path, screenshot.EncodeToPNG());
            Object.Destroy(screenshot);

            presentation.PresentationCanvas.enabled = true;
            Assert.That(File.Exists(path), Is.True, $"Expected visual review artifact at {path}");
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(1024));
        }

        private void AssertSetupContentFits(int expectedPlayerCount, string expectedAiCount)
        {
            TextMeshProUGUI setup = presentation.PresentationCanvas.transform
                .Find("Lobby/PlayersAndControls").GetComponent<TextMeshProUGUI>();
            Canvas.ForceUpdateCanvases();
            setup.ForceMeshUpdate();

            for (int i = 1; i <= expectedPlayerCount; i++)
                Assert.That(setup.text, Does.Contain($"P{i}  "));
            Assert.That(setup.text, Does.Contain("START 3-LAP RACE"));
            Assert.That(setup.text, Does.Contain(expectedAiCount));
            Assert.That(setup.text, Does.Contain("VISUAL STYLE  CLEAN 3D  [V / R3]"));
            Assert.That(setup.text, Does.Contain("LOBBY CAR: UP/DOWN OR W/S"));
            Assert.That(setup.text, Does.Contain("LOBBY MODEL: LEFT/RIGHT OR Q/E"));
            Assert.That(setup.text, Does.Contain("DRIVE: WASD/ARROWS"));
            Assert.That(setup.text, Does.Contain("FIRE: SPACE / X"));
            Assert.That(setup.isTextOverflowing, Is.False,
                $"Setup copy must fit its visible panel at {Screen.width}x{Screen.height}.");
        }

        private void AssertCompactFourPlayerIdentity()
        {
            Canvas.ForceUpdateCanvases();
            for (int playerIndex = 0; playerIndex < roster.Players.Count; playerIndex++)
            {
                Transform badge = presentation.PresentationCanvas.transform.Find(
                    $"SplitHUD/Player_{playerIndex + 1}_HUD/WorkshopLabel");
                Assert.That(badge.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(420f, 88f)));
                TextMeshProUGUI raceData = badge.Find("RaceData").GetComponent<TextMeshProUGUI>();
                raceData.ForceMeshUpdate();
                Assert.That(raceData.text, Does.Contain($"P{playerIndex + 1}"));
                Assert.That(raceData.text, Does.Contain(
                    DriverPersonaCatalog.GetForPlayerSlot(playerIndex).Callsign));
                Assert.That(raceData.isTextOverflowing, Is.False);

                VehicleFamilyVisual family = roster.Players[playerIndex].FamilyVisual;
                Assert.That(family.PresentationPivot.Find($"PlayerIdentityMarker_P{playerIndex + 1}"), Is.Not.Null);
            }
        }

        private static IEnumerator PressAndRelease(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return WaitForPlayerLoop();
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return WaitForPlayerLoop();
        }

        private static IEnumerator WaitForPlayerLoop()
        {
            yield return null;
            yield return null;
        }
    }
}
