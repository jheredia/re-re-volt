using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Core;
using NUnit.Framework;
using Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Vehicle;
using Weapons;

namespace Tests.PlayMode
{
    public sealed class Stage8CombatReadabilityIntegrationTests : InputTestFixture
    {
        private static readonly VisualStyleMode[] StyleModes =
        {
            VisualStyleMode.Clean3D,
            VisualStyleMode.InkLite,
            VisualStyleMode.FullInk
        };

        private GameObject root;
        private VisualStyleController style;
        private RaceManager race;
        private PrototypeCircuitBuilder.Result circuit;
        private VehicleManager roster;
        private Stage3RacePresentation hud;
        private WeaponManager combat;
        private Texture2D foundersAtlas;
        private Texture2D neighborsAtlas;
        private readonly Gamepad[] pads = new Gamepad[4];
        private readonly LocalPlayer[] players = new LocalPlayer[4];

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            root = new GameObject("Stage8CombatReadabilityTest");
            style = root.AddComponent<VisualStyleController>();
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, PrototypeCircuitBuilder.OrderedCheckpointCount), Is.True);
            circuit = PrototypeCircuitBuilder.Build(race, root.transform, style);
            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, circuit.Grid, null, null, style), Is.True);
            hud = root.AddComponent<Stage3RacePresentation>();
            foundersAtlas = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                { name = "Stage8FoundersAtlasTestDouble" };
            neighborsAtlas = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                { name = "Stage8NeighborsAtlasTestDouble" };
            hud.Configure(race, roster, style, new DriverPortraitAtlas(foundersAtlas, neighborsAtlas));
            combat = root.AddComponent<WeaponManager>();
            combat.Configure(race, roster, hud, circuit.DrivableMask, style);

            for (int i = 0; i < pads.Length; i++)
            {
                pads[i] = InputSystem.AddDevice<Gamepad>();
                Assert.That(roster.TryJoinGamepad(pads[i], out players[i]), Is.True);
                players[i].Recovery.ConfigureGroundMask(circuit.DrivableMask);
            }
            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
            circuit?.Dispose();
            Object.DestroyImmediate(foundersAtlas);
            Object.DestroyImmediate(neighborsAtlas);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator PickupsAndFourOwnedHudSlotsKeepExplicitIdentityAcrossAllStyles()
        {
            foreach (VisualStyleMode mode in StyleModes)
            {
                style.SetMode(mode);
                for (int i = 0; i < combat.Pickups.Count; i++)
                {
                    WeaponKind expected = i % 2 == 0 ? WeaponKind.Bolt : WeaponKind.Oil;
                    AssertPickupIdentity(combat.Pickups[i], expected);
                }
                Renderer boltShell = FindSourceRenderer(combat.Pickups[0].transform, "Pickup box");
                Renderer oilShell = FindSourceRenderer(combat.Pickups[1].transform, "Pickup box");
                Assert.That(ColorDistance(ReadColor(boltShell.sharedMaterial), ReadColor(oilShell.sharedMaterial)),
                    Is.GreaterThan(.45f), $"{mode} must retain amber/violet pickup separation.");
            }

            yield return StartRace();
            for (int i = 0; i < players.Length; i++)
            {
                WeaponKind expected = i % 2 == 0 ? WeaponKind.Bolt : WeaponKind.Oil;
                yield return Collect(players[i], i, expected);
                AssertHudIdentity(i, expected);
            }
            AssertPortraitBackedFourPlayerHudBounds();

            foreach (VisualStyleMode mode in StyleModes)
            {
                style.SetMode(mode);
                for (int i = 0; i < players.Length; i++)
                    AssertHudIdentity(i, i % 2 == 0 ? WeaponKind.Bolt : WeaponKind.Oil);
            }
        }

        [UnityTest]
        public IEnumerator RespawnedPickupSwapsShapeTogetherWithAuthoritativeOffering()
        {
            yield return StartRace();
            yield return Collect(players[0], 0, WeaponKind.Bolt);
            Assert.That(combat.Pickups[0].Offering, Is.EqualTo(WeaponKind.Oil));
            yield return new WaitForSeconds(5.05f);
            Assert.That(combat.Pickups[0].IsAvailable, Is.True);
            AssertPickupIdentity(combat.Pickups[0], WeaponKind.Oil);
        }

        [UnityTest]
        public IEnumerator FiredBoltAndDeployedOilKeepDifferentShapesAndColorsAcrossAllStyles()
        {
            yield return StartRace();
            yield return Collect(players[0], 0, WeaponKind.Bolt);
            yield return Collect(players[1], 1, WeaponKind.Oil);
            yield return WaitForPresentationCleanup();

            Place(players[1], new Vector3(15f, .8f, 4f), 180f);
            yield return Press(pads[1], GamepadButton.West);
            LiveCombatWeapon oil = FindWeapon(WeaponKind.Oil);
            Assert.That(oil, Is.Not.Null);

            Place(players[0], new Vector3(-15f, .8f, 6f), 0f);
            System.Action<LocalPlayer> freezeBolt = player =>
            {
                if (player == players[0]) Time.timeScale = 0f;
            };
            roster.FireRequested += freezeBolt;
            try
            {
                yield return Press(pads[0], GamepadButton.West);
                LiveCombatWeapon bolt = FindWeapon(WeaponKind.Bolt);
                Assert.That(bolt, Is.Not.Null);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(SourceRendererCount(bolt.transform), Is.EqualTo(WeaponManager.BoltLiveRendererBudget));
                Assert.That(SourceRendererCount(oil.transform), Is.EqualTo(WeaponManager.OilLiveRendererBudget));
                AssertNoEnabledPresentationColliders(bolt.transform);
                AssertNoEnabledPresentationColliders(oil.transform);
                Assert.That(bolt.transform.Find("Bolt left chevron"), Is.Not.Null);
                Assert.That(oil.transform.Find("Oil warning ring"), Is.Not.Null);
                Assert.That(oil.transform.Find("Oil drop marker"), Is.Not.Null);

                foreach (VisualStyleMode mode in StyleModes)
                {
                    style.SetMode(mode);
                    Renderer boltCore = FindSourceRenderer(bolt.transform, "Bolt arrow core");
                    Renderer oilRing = FindSourceRenderer(oil.transform, "Oil warning ring");
                    Assert.That(ColorDistance(ReadColor(boltCore.sharedMaterial), ReadColor(oilRing.sharedMaterial)),
                        Is.GreaterThan(.45f), $"{mode} must retain live-weapon color identity.");
                }
            }
            finally
            {
                roster.FireRequested -= freezeBolt;
                Time.timeScale = 1f;
            }
        }

        [UnityTest]
        public IEnumerator FireAndHitEventsObservePublishedSlotThenBoundedHitPresentation()
        {
            yield return StartRace();
            yield return Collect(players[0], 0, WeaponKind.Bolt);
            yield return WaitForPresentationCleanup();
            Place(players[0], new Vector3(-15f, .8f, 3f), 0f);
            Place(players[1], new Vector3(-15f, .8f, 9f), 0f);
            Place(players[2], new Vector3(15f, .8f, -10f), 180f);
            Place(players[3], new Vector3(15f, .8f, -15f), 180f);

            List<string> order = new();
            combat.WeaponFired += (shooter, kind) =>
            {
                if (shooter != players[0]) return;
                Assert.That(kind, Is.EqualTo(WeaponKind.Bolt));
                Assert.That(combat.HeldWeapon(0), Is.EqualTo(WeaponKind.None),
                    "HUD publication and inventory consumption precede WeaponFired.");
                AssertHudIdentity(0, WeaponKind.None);
                Assert.That(combat.LiveWeapons.Count, Is.EqualTo(1));
                order.Add("fire");
            };
            combat.WeaponHit += (target, kind) =>
            {
                if (target != players[1]) return;
                Assert.That(kind, Is.EqualTo(WeaponKind.Bolt));
                Assert.That(target.Vehicle.HasCombatModifier, Is.True,
                    "Authoritative modifier application precedes WeaponHit.");
                Transform feedback = target.Vehicle.transform.Find("Combat disruption feedback");
                Assert.That(feedback, Is.Not.Null);
                Assert.That(SourceRendererCount(feedback), Is.EqualTo(WeaponManager.BoltDisruptionRendererBudget));
                Assert.That(combat.FeedbackCount, Is.EqualTo(1),
                    "The manager-owned hit pulse exists before WeaponHit; collision pulse remains after the callback.");
                order.Add("hit");
            };

            yield return Press(pads[0], GamepadButton.West);
            yield return new WaitForSeconds(.35f);
            Assert.That(order, Is.EqualTo(new[] { "fire", "hit" }));
            Assert.That(combat.FeedbackCount, Is.LessThanOrEqualTo(combat.FeedbackCapacity));
            Assert.That(combat.DisruptionFeedbackCount, Is.EqualTo(1));
            Transform boltFeedback = players[1].Vehicle.transform.Find("Combat disruption feedback");
            foreach (VisualStyleMode mode in StyleModes)
            {
                style.SetMode(mode);
                Renderer ray = FindSourceRenderer(boltFeedback, "Bolt shock ray 1");
                Color color = ReadColor(ray.sharedMaterial);
                Assert.That(color.r, Is.GreaterThan(color.b), $"{mode} must retain the amber bolt cue.");
            }
        }

        [UnityTest]
        public IEnumerator OilHitUsesDropLanguageInEveryStyleAndCleansUpWithinExistingDuration()
        {
            yield return StartRace();
            yield return Collect(players[1], 1, WeaponKind.Oil);
            yield return WaitForPresentationCleanup();
            Place(players[1], new Vector3(15f, .8f, 4f), 180f);
            Place(players[0], new Vector3(-15f, .8f, -12f), 0f);
            yield return Press(pads[1], GamepadButton.West);
            LiveCombatWeapon oil = FindWeapon(WeaponKind.Oil);
            Assert.That(oil, Is.Not.Null);
            Place(players[0], oil.transform.position + Vector3.up * .65f, 0f);

            bool hit = false;
            combat.WeaponHit += (target, kind) =>
            {
                if (target != players[0] || kind != WeaponKind.Oil) return;
                hit = true;
                Time.timeScale = 0f;
            };
            float hitDeadline = Time.realtimeSinceStartup + 2f;
            while (!hit && Time.realtimeSinceStartup < hitDeadline) yield return null;
            Assert.That(hit, Is.True);
            Transform feedback = players[0].Vehicle.transform.Find("Combat disruption feedback");
            Assert.That(feedback, Is.Not.Null);
            Assert.That(SourceRendererCount(feedback), Is.EqualTo(WeaponManager.OilDisruptionRendererBudget));
            Assert.That(feedback.Find("Oil skid drop 1"), Is.Not.Null);

            foreach (VisualStyleMode mode in StyleModes)
            {
                style.SetMode(mode);
                Renderer drop = FindSourceRenderer(feedback, "Oil skid drop 1");
                Assert.That(ReadColor(drop.sharedMaterial).b, Is.GreaterThan(ReadColor(drop.sharedMaterial).g),
                    $"{mode} must retain the violet oil cue.");
            }

            Time.timeScale = 1f;
            yield return new WaitForSeconds(2.6f);
            Assert.That(players[0].Vehicle.HasCombatModifier, Is.False);
            Assert.That(combat.DisruptionFeedbackCount, Is.Zero);
            Assert.That(combat.FeedbackCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FeedbackBudgetsCleanupAndStyleRegistrationsRemainBounded()
        {
            Assert.That(combat.FeedbackCapacity, Is.EqualTo(16));
            Assert.That(combat.LiveWeaponCapacity, Is.EqualTo(16));
            Assert.That(combat.FeedbackSourceRendererCapacity, Is.EqualTo(60));
            Assert.That(combat.LiveWeaponSourceRendererCapacity, Is.EqualTo(80));
            Assert.That(combat.FeedbackLifetime, Is.EqualTo(.45f).Within(.0001f));
            yield return StartRace();
            int baselineRegistrations = style.RegisteredRendererCount;
            MethodInfo showBurst = typeof(WeaponManager).GetMethod("ShowBurst",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showBurst, Is.Not.Null);

            for (int i = 0; i < combat.FeedbackCapacity; i++)
                showBurst.Invoke(combat, new object[] { new Vector3(i * .05f, 2f, 0f), WeaponKind.Bolt });

            int registrationsAtCapacity = baselineRegistrations +
                combat.FeedbackCapacity * WeaponManager.BurstRendererBudget;
            Assert.That(combat.FeedbackCount, Is.EqualTo(combat.FeedbackCapacity));
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(registrationsAtCapacity));
            Assert.That(style.RegisteredRendererCount - baselineRegistrations,
                Is.EqualTo(16 * 3),
                "Sixteen accepted burst groups own exactly three source renderers each.");

            showBurst.Invoke(combat, new object[] { Vector3.up * 2f, WeaponKind.Oil });
            Assert.That(combat.FeedbackCount, Is.EqualTo(combat.FeedbackCapacity),
                "The overflow burst must be dropped without replacing or appending a group.");
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(registrationsAtCapacity),
                "Overflow must not register any renderers.");

            yield return new WaitForSeconds(combat.FeedbackLifetime + .1f);
            Assert.That(combat.FeedbackCount, Is.Zero);
            Assert.That(combat.DisruptionFeedbackCount, Is.Zero);
            Assert.That(style.RegisteredRendererCount, Is.EqualTo(baselineRegistrations));

            combat.ClearAllWeapons();
            Assert.That(combat.FeedbackCount, Is.Zero);
            Assert.That(combat.DisruptionFeedbackCount, Is.Zero);
            Assert.That(combat.LiveWeapons.Count, Is.Zero);
        }

        private void AssertPortraitBackedFourPlayerHudBounds()
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(hud.PortraitAtlasAvailable, Is.True);
            Assert.That(hud.HudPortraitCount, Is.EqualTo(4));
            for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
            {
                Transform viewport = hud.PresentationCanvas.transform.Find($"SplitHUD/Player_{playerIndex + 1}_HUD");
                Assert.That(viewport, Is.Not.Null);
                RectTransform badge = viewport.Find("WorkshopLabel").GetComponent<RectTransform>();
                RectTransform raceData = badge.Find("RaceData").GetComponent<RectTransform>();
                Viewport expected = LocalPlayerRules.GetViewport(players.Length, playerIndex);
                float viewportWidth = expected.Width * 1920f;
                float availableRaceDataWidth = badge.sizeDelta.x - raceData.offsetMin.x + raceData.offsetMax.x;

                Assert.That(badge.sizeDelta, Is.EqualTo(new Vector2(540f, 108f)));
                Assert.That(badge.anchoredPosition.x, Is.GreaterThanOrEqualTo(0f));
                Assert.That(badge.anchoredPosition.x + badge.sizeDelta.x, Is.LessThanOrEqualTo(viewportWidth),
                    $"P{playerIndex + 1} badge must stay inside its four-player viewport.");
                Assert.That(raceData.offsetMin.x, Is.EqualTo(108f), "Portrait-backed text keeps its left inset.");
                Assert.That(raceData.offsetMax.x, Is.EqualTo(-112f), "Weapon identity keeps its right reserve.");
                Assert.That(availableRaceDataWidth, Is.GreaterThanOrEqualTo(310f),
                    $"P{playerIndex + 1} RaceData must retain at least its pre-Stage8 preferred width.");
                Assert.That(raceData.rect.width, Is.EqualTo(availableRaceDataWidth).Within(.01f));
            }
        }

        [UnityTest]
        public IEnumerator PauseAndDisconnectFreezeCombatWithoutLosingOwnedHudIdentity()
        {
            yield return StartRace();
            yield return Collect(players[0], 0, WeaponKind.Bolt);
            AssertHudIdentity(0, WeaponKind.Bolt);
            yield return Press(pads[0], GamepadButton.Start);
            Assert.That(hud.IsPaused, Is.True);
            double stopped = combat.SimulationTime;
            yield return Press(pads[0], GamepadButton.West);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(combat.SimulationTime, Is.EqualTo(stopped));
            Assert.That(combat.HeldWeapon(0), Is.EqualTo(WeaponKind.Bolt));
            AssertHudIdentity(0, WeaponKind.Bolt);

            yield return Press(pads[0], GamepadButton.Start);
            Assert.That(hud.IsPaused, Is.False);
            InputSystem.RemoveDevice(pads[1]);
            yield return Frames();
            Assert.That(hud.IsPaused, Is.True);
            stopped = combat.SimulationTime;
            yield return Press(pads[0], GamepadButton.West);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(combat.SimulationTime, Is.EqualTo(stopped));
            Assert.That(combat.HeldWeapon(0), Is.EqualTo(WeaponKind.Bolt));
            AssertHudIdentity(0, WeaponKind.Bolt);
            InputSystem.AddDevice(pads[1]);
            yield return Frames();
            Assert.That(hud.IsPaused, Is.False);
        }

        [Test]
        public void Stage8ReadabilityDoesNotMutateStage4GameplayTuningOrPickupPoses()
        {
            AssertPrivateFloat(combat, "fireCooldown", .35f);
            AssertPrivateFloat(combat, "projectileSpeed", 32f);
            AssertPrivateFloat(combat, "projectileLifetime", 3f);
            AssertPrivateFloat(combat, "projectileRadius", .3f);
            AssertPrivateFloat(combat, "impactDuration", 1f);
            AssertPrivateFloat(combat, "impactVelocityChange", 3f);
            AssertPrivateFloat(combat, "impactMotorMultiplier", .25f);
            AssertPrivateFloat(combat, "impactSteeringMultiplier", .5f);
            AssertPrivateFloat(combat, "oilLifetime", 9f);
            AssertPrivateFloat(combat, "oilRadius", 1.35f);
            AssertPrivateFloat(combat, "oilArmingDelay", .6f);
            AssertPrivateFloat(combat, "oilShooterGrace", 1.2f);
            AssertPrivateFloat(combat, "oilDuration", 2.5f);
            AssertPrivateFloat(combat, "oilGripMultiplier", .18f);
            AssertPrivateFloat(combat, "oilMotorMultiplier", .65f);
            AssertPrivateFloat(combat, "oilSteeringMultiplier", .45f);
            AssertPrivateFloat(combat, "recoveryGrace", .75f);
            AssertPrivateFloat(combat.Pickups[0], "respawnCooldown", 5f);
            Assert.That(WeaponManager.GetPickupPosition(0), Is.EqualTo(new Vector3(-13.3f, .95f, 3f)));
            Assert.That(WeaponManager.GetPickupPosition(1), Is.EqualTo(new Vector3(0f, .95f, 16.7f)));
            Assert.That(WeaponManager.GetPickupPosition(2), Is.EqualTo(new Vector3(13.3f, .95f, 0f)));
            Assert.That(WeaponManager.GetPickupPosition(3), Is.EqualTo(new Vector3(0f, .95f, -13.3f)));

            CombatInventory inventory = new();
            Assert.That(inventory.TryCollect(WeaponKind.Bolt), Is.True);
            Assert.That(inventory.TryCollect(WeaponKind.Oil), Is.False, "The inventory remains single-slot.");
            Assert.That(inventory.TryFire(0d, .35d, out WeaponKind fired), Is.True);
            Assert.That(fired, Is.EqualTo(WeaponKind.Bolt));
            Assert.That(inventory.TryCollect(WeaponKind.Oil), Is.True);
            Assert.That(inventory.CanFire(.349d), Is.False, "The existing fire cooldown remains authoritative.");
            Assert.That(inventory.CanFire(.35d), Is.True);
        }

        private IEnumerator StartRace()
        {
            Assert.That(race.StartRace(), Is.True);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);
        }

        private IEnumerator Collect(LocalPlayer player, int pickupIndex, WeaponKind expected)
        {
            PickupSpawner pickup = combat.Pickups[pickupIndex];
            Assert.That(pickup.Offering, Is.EqualTo(expected));
            Assert.That(pickup.IsAvailable, Is.True);
            Place(player, WeaponManager.GetPickupPosition(pickupIndex), 0f);
            yield return new WaitForSeconds(.1f);
            Assert.That(combat.HeldWeapon(player.PlayerIndex), Is.EqualTo(expected));
            Assert.That(pickup.IsAvailable, Is.False);
        }

        private IEnumerator WaitForPresentationCleanup()
        {
            yield return new WaitForSeconds(combat.FeedbackLifetime + .1f);
            Assert.That(combat.FeedbackCount, Is.Zero);
        }

        private void AssertPickupIdentity(PickupSpawner pickup, WeaponKind kind)
        {
            Transform box = pickup.transform.Find("Pickup box");
            Assert.That(box, Is.Not.Null);
            Transform bolt = box.Find("Bolt pickup identity");
            Transform oil = box.Find("Oil pickup identity");
            Assert.That(bolt, Is.Not.Null);
            Assert.That(oil, Is.Not.Null);
            Assert.That(SourceRendererCount(bolt) + SourceRendererCount(oil),
                Is.EqualTo(PickupSpawner.IdentityRendererBudget));
            AssertNoEnabledPresentationColliders(bolt);
            AssertNoEnabledPresentationColliders(oil);
            Assert.That(bolt.gameObject.activeSelf, Is.EqualTo(kind == WeaponKind.Bolt));
            Assert.That(oil.gameObject.activeSelf, Is.EqualTo(kind == WeaponKind.Oil));
            Assert.That(bolt.Find("Bolt zig middle"), Is.Not.Null);
            Assert.That(oil.Find("Oil drop"), Is.Not.Null);
            Assert.That(oil.Find("Oil puddle mark"), Is.Not.Null);
        }

        private void AssertHudIdentity(int playerIndex, WeaponKind kind)
        {
            Transform badge = hud.PresentationCanvas.transform.Find(
                $"SplitHUD/Player_{playerIndex + 1}_HUD/WorkshopLabel");
            Assert.That(badge, Is.Not.Null);
            string suffix = kind == WeaponKind.Bolt ? "BOLT" : kind == WeaponKind.Oil ? "OIL" : "EMPTY";
            Transform identity = badge.Find("WeaponIdentity");
            Assert.That(identity, Is.Not.Null, $"P{playerIndex + 1} owns its {suffix} identity.");
            Assert.That(identity.Find("BoltIcon").gameObject.activeSelf, Is.EqualTo(kind == WeaponKind.Bolt));
            Assert.That(identity.Find("OilIcon").gameObject.activeSelf, Is.EqualTo(kind == WeaponKind.Oil));
            TextMeshProUGUI label = identity.Find("WeaponName").GetComponent<TextMeshProUGUI>();
            Assert.That(label.text, Is.EqualTo(suffix));
        }

        private LiveCombatWeapon FindWeapon(WeaponKind kind)
        {
            foreach (LiveCombatWeapon weapon in combat.LiveWeapons)
                if (weapon != null && weapon.Kind == kind) return weapon;
            return null;
        }

        private static void Place(LocalPlayer player, Vector3 position, float yaw)
        {
            player.Vehicle.Teleport(new Pose(position, Quaternion.Euler(0f, yaw, 0f)));
            player.ChaseCamera.SnapToTarget();
            Physics.SyncTransforms();
        }

        private static IEnumerator Press(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return Frames();
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Frames();
        }

        private static IEnumerator Frames()
        {
            yield return null;
            yield return null;
        }

        private static int SourceRendererCount(Transform parent)
        {
            int count = 0;
            foreach (Renderer renderer in parent.GetComponentsInChildren<Renderer>(true))
                if (!renderer.name.EndsWith("_InkOutline")) count++;
            return count;
        }

        private static Renderer FindSourceRenderer(Transform parent, string name)
        {
            foreach (Renderer renderer in parent.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == name) return renderer;
            Assert.Fail($"Missing source renderer '{name}' below '{parent.name}'.");
            return null;
        }

        private static void AssertNoEnabledPresentationColliders(Transform parent)
        {
            foreach (Collider collider in parent.GetComponentsInChildren<Collider>(true))
                Assert.That(collider.enabled, Is.False, $"{collider.name} must remain presentation-only.");
        }

        private static Color ReadColor(Material material)
        {
            Assert.That(material, Is.Not.Null);
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            return material.color;
        }

        private static float ColorDistance(Color first, Color second) =>
            Vector3.Distance(new Vector3(first.r, first.g, first.b), new Vector3(second.r, second.g, second.b));

        private static void AssertPrivateFloat(object instance, string fieldName, float expected)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            Assert.That((float)field.GetValue(instance), Is.EqualTo(expected).Within(.0001f), fieldName);
        }
    }
}
