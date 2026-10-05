using System.Collections;
using System.Collections.Generic;
using Core;
using NUnit.Framework;
using Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.TestTools;
using Vehicle;
using Weapons;

namespace Tests.PlayMode
{
    public sealed class VisualStyleRuntimeIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private VisualStyleController style;
        private RaceManager race;
        private PrototypeCircuitBuilder.Result circuit;
        private VehicleManager roster;
        private Stage3RacePresentation presentation;
        private WeaponManager combat;
        private Keyboard keyboard;
        private Camera lobbyCamera;
        private readonly List<AudioListener> externalListeners = new();

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            externalListeners.Clear();
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
            {
                if (!listener.enabled) continue;
                externalListeners.Add(listener);
                listener.enabled = false;
            }
            root = new GameObject("Stage6B_RuntimeTest");
            style = root.AddComponent<VisualStyleController>();
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, PrototypeCircuitBuilder.OrderedCheckpointCount), Is.True);
            circuit = PrototypeCircuitBuilder.Build(race, root.transform, style);
            roster = root.AddComponent<VehicleManager>();
            GameObject cameraObject = new GameObject("Stage6B_LobbyCamera");
            cameraObject.transform.SetParent(root.transform, false);
            lobbyCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            Assert.That(roster.Configure(race, circuit.Grid, null, lobbyCamera, style), Is.True);
            roster.PlayerJoined += player => player.Recovery.ConfigureGroundMask(circuit.DrivableMask);
            presentation = root.AddComponent<Stage3RacePresentation>();
            presentation.Configure(race, roster, style);
            combat = root.AddComponent<WeaponManager>();
            combat.Configure(race, roster, presentation, circuit.DrivableMask, style);
            keyboard = InputSystem.AddDevice<Keyboard>();
            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            circuit?.Dispose();
            Object.DestroyImmediate(root);
            for (int i = 0; i < externalListeners.Count; i++)
                if (externalListeners[i] != null) externalListeners[i].enabled = true;
            externalListeners.Clear();
            Assert.That(InputUser.all.Count, Is.Zero);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator KeyboardAndGamepadShareOneGatedSessionStyleAndLiveLabels()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            yield return PressPad(pad, GamepadButton.RightStick);
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.Clean3D), "An unpaired device must not own menu input.");
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer keyboardPlayer), Is.True);
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer gamepadPlayer), Is.True);
            yield return Frames();
            Assert.That(EnabledAudioListenerCount(), Is.EqualTo(1));

            presentation.enabled = false;
            presentation.enabled = true;
            presentation.enabled = false;
            presentation.enabled = true;
            yield return Frames();

            Renderer body = keyboardPlayer.Vehicle.transform.Find("Body").GetComponent<Renderer>();
            MaterialPropertyBlock playerColor = new MaterialPropertyBlock();
            playerColor.SetColor("_BaseColor", Color.green);
            body.SetPropertyBlock(playerColor);
            Assert.That(FindText("PlayersAndControls"), Does.Contain("VISUAL STYLE  CLEAN 3D  [V / R3]"));

            yield return Press(Key.V);
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.InkLite));
            Assert.That(FindText("PlayersAndControls"), Does.Contain("VISUAL STYLE  INK LITE  [V / R3]"));
            yield return PressPad(pad, GamepadButton.RightStick);
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.FullInk),
                "A second paired player must operate the same global style authority.");
            AssertPropertyColor(body, Color.green);

            yield return Press(Key.Enter);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            yield return Press(Key.V);
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.FullInk), "Countdown must reject style changes.");
            yield return WaitForRacing();
            yield return PressPad(pad, GamepadButton.RightStick);
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.FullInk), "Active racing must reject style changes.");

            InputSystem.RemoveDevice(pad);
            yield return Frames();
            Assert.That(presentation.IsPaused, Is.True);
            yield return Press(Key.V);
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.FullInk), "Disconnect pause must reject keyboard style input.");
            InputSystem.AddDevice(pad);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Frames();
            Assert.That(presentation.IsPaused, Is.False);

            InputSystem.RemoveDevice(keyboard);
            yield return Frames();
            Assert.That(presentation.IsPaused, Is.True);
            yield return PressPad(pad, GamepadButton.RightStick);
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.FullInk), "Disconnect pause must reject gamepad style input.");
            InputSystem.AddDevice(keyboard);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames();
            Assert.That(presentation.IsPaused, Is.False);

            yield return Press(Key.Escape);
            Assert.That(presentation.IsPaused, Is.True);
            yield return PressPad(pad, GamepadButton.RightStick);
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.Clean3D));
            Assert.That(FindText("Body"), Does.Contain("VISUAL STYLE  CLEAN 3D  [V / R3]"));
            yield return Press(Key.V);
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.InkLite));
            Assert.That(FindText("Body"), Does.Contain("VISUAL STYLE  INK LITE  [V / R3]"));
            AssertPropertyColor(body, Color.green);

            yield return PressPad(pad, GamepadButton.Start);
            Assert.That(presentation.IsPaused, Is.False);
            CompleteProgress(keyboardPlayer);
            CompleteProgress(gamepadPlayer);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
            yield return Press(Key.V);
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.InkLite), "Results must reject style changes.");

            yield return Press(Key.Enter);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.InkLite), "Rematch must preserve the session style.");
            race.ResetRace();
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Idle));
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.InkLite));
            Assert.That(FindText("PlayersAndControls"), Does.Contain("VISUAL STYLE  INK LITE  [V / R3]"));
            Assert.That(EnabledAudioListenerCount(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RuntimeObjectsInheritStylePickupSwapsStayStableAndCleanupUnregisters()
        {
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer player), Is.True);
            yield return Frames();
            style.SetMode(VisualStyleMode.FullInk);

            Renderer body = player.Vehicle.transform.Find("Body").GetComponent<Renderer>();
            Assert.That(body.sharedMaterial.shader.name, Is.EqualTo("ReReVolt/InkLit"));
            Assert.That(body.transform.Find("Body_InkOutline"), Is.Not.Null);
            Assert.That(body.transform.Find("Body_InkOutline").gameObject.activeSelf, Is.True);
            Transform road = circuit.Root.transform.Find("Road_West");
            Transform barrier = circuit.Root.transform.Find("Outer_West");
            Assert.That(road.Find("Road_West_InkOutline"), Is.Null, "Flat road must not be outlined.");
            Assert.That(barrier.Find("Outer_West_InkOutline"), Is.Not.Null, "Raised barrier must be outlined.");

            int registeredBeforeCycles = style.RegisteredRendererCount;
            int outlinesBeforeCycles = style.OutlineObjectCount;
            int materialsBeforeCycles = style.InkMaterialCount;
            for (int i = 0; i < 4; i++)
            {
                style.CycleMode();
                style.CycleMode();
                style.CycleMode();
            }
            Assert.That(style.Mode, Is.EqualTo(VisualStyleMode.FullInk));
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(registeredBeforeCycles));
            Assert.That(style.OutlineObjectCount, Is.EqualTo(outlinesBeforeCycles));
            Assert.That(style.InkMaterialCount, Is.EqualTo(materialsBeforeCycles));

            yield return Press(Key.Enter);
            yield return WaitForRacing();
            PickupSpawner pickup = combat.Pickups[0];
            Renderer pickupBox = pickup.transform.Find("Pickup box").GetComponent<Renderer>();
            Material boltInk = pickupBox.sharedMaterial;
            player.Vehicle.Teleport(new Pose(WeaponManager.GetPickupPosition(0), Quaternion.identity));
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.15f);
            Assert.That(combat.HeldWeapon(0), Is.EqualTo(WeaponKind.Bolt));
            Assert.That(pickup.Offering, Is.EqualTo(WeaponKind.Oil));
            Material oilInk = pickupBox.sharedMaterial;
            Assert.That(oilInk, Is.Not.SameAs(boltInk));
            int materialsAfterSwap = style.InkMaterialCount;
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            Assert.That(pickupBox.sharedMaterial, Is.SameAs(oilInk),
                "Unchanged FixedUpdate refreshes must retain the styled material reference.");
            Assert.That(style.InkMaterialCount, Is.EqualTo(materialsAfterSwap));

            yield return new WaitForSeconds(.5f);
            int baselineRegistrations = style.RegisteredRendererCount;
            yield return Press(Key.Space);
            Assert.That(combat.LiveWeapons.Count, Is.EqualTo(1));
            LiveCombatWeapon weapon = combat.LiveWeapons[0];
            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name.EndsWith("_InkOutline")) continue;
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("ReReVolt/InkLit"));
            }
            Assert.That(style.RegisteredRendererCount, Is.GreaterThan(baselineRegistrations));
            combat.ClearAllWeapons();
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(baselineRegistrations),
                "Weapon and feedback cleanup must unregister every transient renderer immediately.");

            race.ResetRace();
            PickupSpawner destroyedSpawner = combat.Pickups[3];
            int beforeSpawnerDestroy = style.RegisteredRendererCount;
            Object.Destroy(destroyedSpawner.gameObject);
            yield return null;
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(beforeSpawnerDestroy - 3),
                "Spawner destruction must unregister box, band, and base renderers.");

            int beforeRosterDisable = style.RegisteredRendererCount;
            int playerRendererCount = CountSourceRenderers(player.Vehicle.gameObject);
            roster.enabled = false;
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(beforeRosterDisable - playerRendererCount));
            Assert.That(InputUser.all.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AuthoredVehicleRegistrationIsTransactionalAcrossLeaveAndRosterDisable()
        {
            GameObject prefab = PrototypeVehicleFactory.Create(new Pose(new Vector3(1000f, 1000f, 1000f), Quaternion.identity));
            prefab.name = "AuthoredVehiclePrefab";
            prefab.transform.SetParent(root.transform, true);
            GameObject authoredOwner = new GameObject("AuthoredRoster");
            authoredOwner.transform.SetParent(root.transform, false);
            RaceManager authoredRace = authoredOwner.AddComponent<RaceManager>();
            Assert.That(authoredRace.ConfigureRace(3, PrototypeCircuitBuilder.OrderedCheckpointCount), Is.True);
            VehicleManager authoredRoster = authoredOwner.AddComponent<VehicleManager>();
            Assert.That(authoredRoster.Configure(authoredRace, circuit.Grid, prefab, lobbyCamera, style), Is.True);
            int baseline = style.RegisteredRendererCount;

            Gamepad first = InputSystem.AddDevice<Gamepad>();
            Assert.That(authoredRoster.TryJoinGamepad(first, out LocalPlayer firstPlayer), Is.True);
            int firstRendererCount = CountSourceRenderers(firstPlayer.Vehicle.gameObject);
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(baseline + firstRendererCount));
            Renderer firstBody = firstPlayer.Vehicle.transform.Find("Body").GetComponent<Renderer>();
            Assert.That(firstBody.transform.Find("Body_InkOutline"), Is.Not.Null,
                "A real authored MeshRenderer shell must remain outline eligible.");
            Assert.That(authoredRoster.TryLeavePlayer(firstPlayer.PlayerIndex), Is.True);
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(baseline));
            Assert.That(InputUser.all.Count, Is.Zero);

            Gamepad second = InputSystem.AddDevice<Gamepad>();
            Assert.That(authoredRoster.TryJoinGamepad(second, out LocalPlayer secondPlayer), Is.True);
            Assert.That(style.RegisteredRendererCount,
                Is.EqualTo(baseline + CountSourceRenderers(secondPlayer.Vehicle.gameObject)));
            authoredRoster.enabled = false;
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(baseline));
            Assert.That(InputUser.all.Count, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MalformedVehicleRendererRollsBackStyleRacerAndInputOwnership()
        {
            GameObject prefab = PrototypeVehicleFactory.Create(new Pose(new Vector3(1000f, 1000f, 1000f), Quaternion.identity));
            prefab.name = "MalformedVehiclePrefab";
            prefab.transform.SetParent(root.transform, true);
            GameObject malformed = new GameObject("Malformed shell", typeof(MeshRenderer));
            malformed.transform.SetParent(prefab.transform, false);

            GameObject badOwner = new GameObject("MalformedRoster");
            badOwner.transform.SetParent(root.transform, false);
            RaceManager badRace = badOwner.AddComponent<RaceManager>();
            Assert.That(badRace.ConfigureRace(3, PrototypeCircuitBuilder.OrderedCheckpointCount), Is.True);
            VehicleManager badRoster = badOwner.AddComponent<VehicleManager>();
            Assert.That(badRoster.Configure(badRace, circuit.Grid, prefab, lobbyCamera, style), Is.True);
            int baseline = style.RegisteredRendererCount;
            Gamepad pad = InputSystem.AddDevice<Gamepad>();

            LogAssert.Expect(LogType.Error,
                "[VehicleManager] Car visual-style registration failed and was rolled back: InvalidOperationException: A registered MeshRenderer needs a MeshFilter for outlines.");
            Assert.That(badRoster.TryJoinGamepad(pad, out _), Is.False);
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(baseline));
            Assert.That(badRace.Racers, Is.Empty);
            Assert.That(badRoster.Players, Is.Empty);
            Assert.That(InputUser.all.Count, Is.Zero);
            Assert.That(Object.FindObjectsByType<ChaseCamera>(FindObjectsInactive.Include), Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StyledCombatFeedbackReplacesExpiresAndOilCleanupStaysBounded()
        {
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer shooter), Is.True);
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            Assert.That(roster.TryJoinGamepad(pad, out LocalPlayer target), Is.True);
            style.SetMode(VisualStyleMode.FullInk);
            yield return Frames();
            yield return Press(Key.Enter);
            yield return WaitForRacing();
            int staticRegistrations = style.RegisteredRendererCount;

            yield return Collect(shooter, 0, WeaponKind.Bolt);
            Assert.That(combat.FeedbackCount, Is.EqualTo(1));
            Assert.That(style.RegisteredRendererCount, Is.GreaterThan(staticRegistrations));
            yield return new WaitForSeconds(.55f);
            Assert.That(combat.FeedbackCount, Is.Zero);
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(staticRegistrations),
                "Pickup burst expiry must release its renderer registration.");

            Place(shooter, new Vector3(-15f, .8f, 3f), 0f);
            Place(target, new Vector3(-15f, .8f, 9f), 0f);
            yield return Press(Key.Space);
            yield return new WaitForSeconds(.3f);
            Transform firstFeedback = target.Vehicle.transform.Find("Combat disruption feedback");
            Assert.That(firstFeedback, Is.Not.Null);

            yield return Collect(shooter, 2, WeaponKind.Bolt);
            Place(shooter, new Vector3(-15f, .8f, 3f), 0f);
            Place(target, new Vector3(-15f, .8f, 9f), 0f);
            yield return Press(Key.Space);
            yield return new WaitForSeconds(.3f);
            Transform replacementFeedback = target.Vehicle.transform.Find("Combat disruption feedback");
            Assert.That(replacementFeedback, Is.Not.Null);
            Assert.That(replacementFeedback, Is.Not.SameAs(firstFeedback),
                "A repeated hit must replace, not accumulate, disruption feedback.");

            yield return new WaitForSeconds(.55f);
            Assert.That(combat.FeedbackCount, Is.Zero);
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(staticRegistrations + 3),
                "Only the three current disruption sparks may remain after pulse expiry.");
            yield return new WaitForSeconds(.55f);
            Assert.That(target.Vehicle.transform.Find("Combat disruption feedback"), Is.Null);
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(staticRegistrations));

            yield return Collect(shooter, 1, WeaponKind.Oil);
            yield return new WaitForSeconds(.55f);
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(staticRegistrations));
            Place(shooter, new Vector3(15f, .8f, 4f), 180f);
            Place(target, new Vector3(-15f, .8f, -4f), 0f);
            yield return Press(Key.Space);
            Assert.That(combat.LiveWeapons.Count, Is.EqualTo(1));
            LiveCombatWeapon oil = combat.LiveWeapons[0];
            Assert.That(oil.Kind, Is.EqualTo(WeaponKind.Oil));
            foreach (Renderer renderer in oil.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name.EndsWith("_InkOutline")) continue;
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("ReReVolt/InkLit"));
            }
            Assert.That(style.RegisteredRendererCount, Is.GreaterThan(staticRegistrations));
            combat.ClearAllWeapons();
            Assert.That(combat.LiveWeapons, Is.Empty);
            Assert.That(combat.FeedbackCount, Is.Zero);
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(staticRegistrations));
        }

        [UnityTest]
        public IEnumerator BootstrapCreatesExactlyOneControllerBeforeCompositionAndIsIdempotent()
        {
            circuit.Dispose();
            circuit = null;
            Object.DestroyImmediate(root);
            root = null;

            GameObject bootstrapObject = new GameObject("Stage3_RuntimeBootstrap");
            Stage3Bootstrap bootstrap = bootstrapObject.AddComponent<Stage3Bootstrap>();
            Assert.That(bootstrap.InitializeNow(), Is.True);
            Assert.That(bootstrapObject.GetComponents<VisualStyleController>(), Has.Length.EqualTo(1));
            Assert.That(bootstrap.VisualStyle, Is.SameAs(bootstrapObject.GetComponent<VisualStyleController>()));
            Assert.That(bootstrap.CircuitRoot, Is.Not.Null);
            Assert.That(bootstrap.InitializeNow(), Is.True);
            Assert.That(bootstrapObject.GetComponents<VisualStyleController>(), Has.Length.EqualTo(1));

            if (GameManager.Instance != null) Object.DestroyImmediate(GameManager.Instance.gameObject);
            Object.DestroyImmediate(bootstrapObject);

            GameObject duplicateObject = new GameObject("Stage3_RuntimeBootstrap_DuplicateTest");
            Stage3Bootstrap duplicateBootstrap = duplicateObject.AddComponent<Stage3Bootstrap>();
            duplicateObject.AddComponent<VisualStyleController>();
            duplicateObject.AddComponent<VisualStyleController>();
            LogAssert.Expect(LogType.Error,
                "[Stage3] Runtime bootstrap requires exactly one VisualStyleController; duplicate components were found.");
            Assert.That(duplicateBootstrap.InitializeNow(), Is.False);
            Assert.That(duplicateBootstrap.CircuitRoot, Is.Null);
            Assert.That(duplicateBootstrap.RaceManager, Is.Null);
            Assert.That(duplicateObject.transform.childCount, Is.Zero);
            Object.DestroyImmediate(duplicateObject);
            yield return null;
        }

        private void CompleteProgress(LocalPlayer player)
        {
            for (int guard = 0; guard < 24 && !player.Racer.IsFinished; guard++)
                Assert.That(race.ReportCheckpoint(player.Racer.RacerId, player.Racer.NextCheckpointIndex), Is.True);
            Assert.That(player.Racer.IsFinished, Is.True);
        }

        private IEnumerator Collect(LocalPlayer player, int pickupIndex, WeaponKind expected)
        {
            Place(player, WeaponManager.GetPickupPosition(pickupIndex), 0f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.15f);
            Assert.That(combat.HeldWeapon(player.PlayerIndex), Is.EqualTo(expected));
        }

        private static void Place(LocalPlayer player, Vector3 position, float yaw)
        {
            player.Vehicle.Teleport(new Pose(position, Quaternion.Euler(0f, yaw, 0f)));
            Physics.SyncTransforms();
        }

        private static int CountSourceRenderers(GameObject owner)
        {
            int count = 0;
            Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (!renderers[i].gameObject.name.EndsWith("_InkOutline")) count++;
            return count;
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

        private IEnumerator WaitForRacing()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);
        }

        private string FindText(string objectName)
        {
            foreach (TextMeshProUGUI label in presentation.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (label.gameObject.name == objectName) return label.text;
            Assert.Fail($"Could not find text object {objectName}.");
            return null;
        }

        private static void AssertPropertyColor(Renderer renderer, Color expected)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(expected));
        }

        private static int EnabledAudioListenerCount()
        {
            int count = 0;
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i].isActiveAndEnabled) count++;
            return count;
        }

        private static IEnumerator Frames()
        {
            yield return null;
            yield return null;
        }
    }
}
