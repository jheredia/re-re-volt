using System;
using System.Collections.Generic;
using Core;
using Presentation;
using UnityEngine;
using Vehicle;

namespace Weapons
{
    /// <summary>Only combat authority: inventory, active simulation time, weapons, effects, and cleanup.</summary>
    public sealed class WeaponManager : MonoBehaviour
    {
        // Source-renderer budgets exclude controller-owned Full Ink outline renderers.
        public const int BoltLiveRendererBudget = 5;
        public const int OilLiveRendererBudget = 4;
        public const int BoltDisruptionRendererBudget = 3;
        public const int OilDisruptionRendererBudget = 3;
        public const int BurstRendererBudget = 3;

        [SerializeField, Min(0f)] private float fireCooldown = .35f;
        [SerializeField, Min(.1f)] private float projectileSpeed = 32f;
        [SerializeField, Min(.1f)] private float projectileLifetime = 3f;
        [SerializeField, Min(.05f)] private float projectileRadius = .3f;
        [SerializeField, Min(0f)] private float impactDuration = 1f;
        [SerializeField, Min(0f)] private float impactVelocityChange = 3f;
        [SerializeField, Range(0f, 1f)] private float impactMotorMultiplier = .25f;
        [SerializeField, Range(0f, 1f)] private float impactSteeringMultiplier = .5f;
        [SerializeField, Min(.1f)] private float oilLifetime = 9f;
        [SerializeField, Min(.1f)] private float oilRadius = 1.35f;
        [SerializeField, Min(0f)] private float oilArmingDelay = .6f;
        [SerializeField, Min(0f)] private float oilShooterGrace = 1.2f;
        [SerializeField, Min(0f)] private float oilDuration = 2.5f;
        [SerializeField, Range(0f, 1f)] private float oilGripMultiplier = .18f;
        [SerializeField, Range(0f, 1f)] private float oilMotorMultiplier = .65f;
        [SerializeField, Range(0f, 1f)] private float oilSteeringMultiplier = .45f;
        [SerializeField, Min(0f)] private float recoveryGrace = .75f;
        [SerializeField, Min(1)] private int maximumLiveWeapons = 16;
        [SerializeField, Min(1)] private int maximumFeedbackEffects = 16;
        [SerializeField, Min(.1f)] private float feedbackLifetime = .45f;

        private sealed class PlayerCombat
        {
            internal LocalPlayer Player;
            internal readonly CombatInventory Inventory = new();
            internal double ImpactUntil, OilUntil, SafeUntil;
            internal Action OnRecovered;
            internal GameObject Feedback;
        }

        private sealed class Burst
        {
            internal GameObject Visual;
            internal double Until;
        }

        private readonly List<PickupSpawner> spawners = new();
        private readonly List<LiveCombatWeapon> liveWeapons = new();
        private readonly List<Burst> bursts = new();
        private readonly PlayerCombat[] players = new PlayerCombat[LocalPlayerRules.MaximumPlayers];
        private IReadOnlyList<PickupSpawner> spawnerView;
        private IReadOnlyList<LiveCombatWeapon> weaponView;
        private RaceManager race;
        private VehicleManager vehicles;
        private Stage3RacePresentation presentation;
        private LayerMask drivableMask;
        private bool configured, subscribed;
        private GameObject combatRoot;
        private VisualStyleController visualStyle;

        public double SimulationTime { get; private set; }
        public IReadOnlyList<PickupSpawner> Pickups => spawnerView ??= spawners.AsReadOnly();
        public IReadOnlyList<LiveCombatWeapon> LiveWeapons => weaponView ??= liveWeapons.AsReadOnly();
        public int FeedbackCount => bursts.Count;
        public int FeedbackCapacity => maximumFeedbackEffects;
        public int LiveWeaponCapacity => maximumLiveWeapons;
        public int FeedbackSourceRendererCapacity =>
            maximumFeedbackEffects * BurstRendererBudget +
            LocalPlayerRules.MaximumPlayers * BoltDisruptionRendererBudget;
        public int LiveWeaponSourceRendererCapacity => maximumLiveWeapons * BoltLiveRendererBudget;
        public float FeedbackLifetime => feedbackLifetime;
        public int DisruptionFeedbackCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < players.Length; i++)
                    if (players[i]?.Feedback != null) count++;
                return count;
            }
        }
        public event Action<LocalPlayer, WeaponKind> WeaponCollected;
        public event Action<LocalPlayer, WeaponKind> WeaponFired;
        public event Action<LocalPlayer, WeaponKind> WeaponHit;
        internal Material BoltMaterial { get; private set; }
        internal Material OilMaterial { get; private set; }
        internal Material FeedbackMaterial { get; private set; }
        private Material darkOilMaterial;

        // Runtime triggers only, between checkpoints and clear of their recovery search lanes.
        public static Vector3 GetPickupPosition(int index) => index switch
        {
            0 => new Vector3(-13.3f, .95f, 3f),
            1 => new Vector3(0f, .95f, 16.7f),
            2 => new Vector3(13.3f, .95f, 0f),
            3 => new Vector3(0f, .95f, -13.3f),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        public void Configure(RaceManager raceManager, VehicleManager roster,
            Stage3RacePresentation hud, LayerMask groundMask, VisualStyleController visualStyleController = null)
        {
            if (configured) throw new InvalidOperationException("Combat is already configured.");
            if (raceManager == null || roster == null || hud == null) throw new ArgumentNullException();
            race = raceManager;
            vehicles = roster;
            presentation = hud;
            drivableMask = groundMask;
            visualStyle = visualStyleController;
            BoltMaterial = CreateMaterial("Combat amber", new Color(1f, .38f, .025f));
            OilMaterial = CreateMaterial("Combat violet", new Color(.63f, .15f, 1f));
            FeedbackMaterial = CreateMaterial("Combat white", new Color(.9f, 1f, 1f));
            darkOilMaterial = CreateMaterial("Oil slick", new Color(.075f, .025f, .13f));
            combatRoot = new GameObject("Stage4_RuntimeCombat");
            combatRoot.transform.SetParent(transform, false);
            for (int i = 0; i < 4; i++)
            {
                GameObject point = new GameObject($"Pickup_{i + 1}");
                point.transform.SetParent(combatRoot.transform, false);
                point.transform.position = GetPickupPosition(i);
                PickupSpawner spawner = point.AddComponent<PickupSpawner>();
                spawners.Add(spawner);
                spawner.Configure(this, i);
            }
            configured = true;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            if (!configured) return;
            combatRoot.SetActive(true);
            Subscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            race.RaceStateChanged += OnRaceStateChanged;
            race.RacerFinished += OnRacerFinished;
            vehicles.PlayerJoined += OnPlayerJoined;
            vehicles.PlayerLeft += OnPlayerLeft;
            vehicles.FireRequested += OnFireRequested;
            subscribed = true;
            foreach (LocalPlayer player in vehicles.Players) OnPlayerJoined(player);
        }

        private void OnPlayerJoined(LocalPlayer player)
        {
            if (players[player.PlayerIndex] != null) return;
            PlayerCombat state = new PlayerCombat { Player = player };
            state.OnRecovered = () => Recover(state);
            player.Recovery.Recovered += state.OnRecovered;
            players[player.PlayerIndex] = state;
            PublishSlot(state);
        }

        private void OnPlayerLeft(int index)
        {
            PlayerCombat state = players[index];
            if (state == null) return;
            ClearPlayer(state);
            if (state.Player.Recovery != null) state.Player.Recovery.Recovered -= state.OnRecovered;
            players[index] = null;
        }

        public WeaponKind HeldWeapon(int index) => index >= 0 && index < players.Length
            ? players[index]?.Inventory.Held ?? WeaponKind.None : WeaponKind.None;

        private bool CanSimulate
        {
            get
            {
                if (!isActiveAndEnabled || !configured || !race.RaceRunning || Time.timeScale <= 0f || !vehicles.isActiveAndEnabled)
                    return false;
                foreach (LocalPlayer player in vehicles.Players)
                    if (!player.IsConnected || (!player.Racer.IsFinished && player.Input.GameplaySuspended)) return false;
                return true;
            }
        }

        private bool CanParticipate(PlayerCombat state) => state != null && CanSimulate &&
            state.Player.Vehicle != null && state.Player.Vehicle.isActiveAndEnabled &&
            state.Player.Input.isActiveAndEnabled && !state.Player.Input.GameplaySuspended &&
            state.Player.IsConnected && !state.Player.Racer.IsFinished;

        internal LocalPlayer ResolvePlayer(Collider collider)
        {
            Rigidbody body = collider.attachedRigidbody;
            if (body == null) return null;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].Player.Vehicle != null &&
                    players[i].Player.Vehicle.gameObject == body.gameObject) return players[i].Player;
            return null;
        }

        internal void TryCollect(PickupSpawner spawner, Collider collider)
        {
            LocalPlayer player = ResolvePlayer(collider);
            if (player == null || !spawners.Contains(spawner)) return;
            PlayerCombat state = players[player.PlayerIndex];
            if (!CanParticipate(state) || SimulationTime < state.SafeUntil || !spawner.TryCollect(state.Inventory)) return;
            PublishSlot(state);
            ShowBurst(spawner.transform.position, state.Inventory.Held);
            WeaponCollected?.Invoke(player, state.Inventory.Held);
        }

        private void OnFireRequested(LocalPlayer player)
        {
            PlayerCombat state = players[player.PlayerIndex];
            if (state == null || state.Player != player || !CanParticipate(state) ||
                !state.Inventory.CanFire(SimulationTime) || liveWeapons.Count >= maximumLiveWeapons) return;
            Transform car = player.Vehicle.transform;
            Vector3 forward = Vector3.ProjectOnPlane(car.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) return;
            WeaponKind kind = state.Inventory.Held;
            Vector3 position = car.position + forward * 1.25f + Vector3.up * .15f;
            if (kind == WeaponKind.Oil)
            {
                Vector3 drop = car.position - forward * 2.7f;
                if (!Physics.Raycast(drop + Vector3.up * 2f, Vector3.down, out RaycastHit ground,
                    4f, drivableMask, QueryTriggerInteraction.Ignore) || ground.normal.y < .8f) return;
                position = ground.point + Vector3.up * .055f;
            }
            if (!state.Inventory.TryFire(SimulationTime, fireCooldown, out kind)) return;
            GameObject weaponObject = new GameObject(kind == WeaponKind.Bolt ? "Live Bolt" : "Live Oil");
            weaponObject.transform.SetParent(combatRoot.transform, false);
            weaponObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            LiveCombatWeapon weapon = weaponObject.AddComponent<LiveCombatWeapon>();
            weapon.Initialize(this, player, kind, forward, projectileSpeed, projectileRadius,
                kind == WeaponKind.Bolt ? projectileLifetime : oilLifetime, oilRadius, oilArmingDelay, oilShooterGrace);
            liveWeapons.Add(weapon);
            BuildLiveWeaponVisual(weapon, kind);
            PublishSlot(state);
            WeaponFired?.Invoke(player, kind);
        }

        private void BuildLiveWeaponVisual(LiveCombatWeapon weapon, WeaponKind kind)
        {
            if (kind == WeaponKind.Bolt)
            {
                CreateVisual(weapon.transform, "Bolt arrow core", PrimitiveType.Sphere, Vector3.zero,
                    new Vector3(.48f, .48f, 1.2f), BoltMaterial, true);
                CreateVisual(weapon.transform, "Bolt white crossbar", PrimitiveType.Cube, Vector3.back * .32f,
                    new Vector3(.9f, .13f, .4f), FeedbackMaterial, true);
                CreateVisual(weapon.transform, "Bolt luminous tail", PrimitiveType.Sphere, Vector3.back * .8f,
                    new Vector3(.24f, .24f, .8f), BoltMaterial, true);
                GameObject leftRail = CreateVisual(weapon.transform, "Bolt left chevron", PrimitiveType.Cube,
                    new Vector3(-.32f, 0f, .08f), new Vector3(.12f, .12f, .72f), FeedbackMaterial, true);
                leftRail.transform.localRotation = Quaternion.Euler(0f, 28f, 0f);
                GameObject rightRail = CreateVisual(weapon.transform, "Bolt right chevron", PrimitiveType.Cube,
                    new Vector3(.32f, 0f, .08f), new Vector3(.12f, .12f, .72f), FeedbackMaterial, true);
                rightRail.transform.localRotation = Quaternion.Euler(0f, -28f, 0f);
                return;
            }

            CreateVisual(weapon.transform, "Oil warning ring", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(oilRadius * 2f, .018f, oilRadius * 2f), OilMaterial, true);
            CreateVisual(weapon.transform, "Oil dark puddle", PrimitiveType.Cylinder, Vector3.up * .025f,
                new Vector3(oilRadius * 1.8f, .02f, oilRadius * 1.8f), darkOilMaterial);
            CreateVisual(weapon.transform, "Oil violet glint", PrimitiveType.Cube, new Vector3(.3f, .055f, .2f),
                new Vector3(.55f, .025f, .12f), OilMaterial, true);
            GameObject drop = CreateVisual(weapon.transform, "Oil drop marker", PrimitiveType.Sphere,
                Vector3.up * .22f, new Vector3(.38f, .5f, .38f), OilMaterial, true);
            drop.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private void FixedUpdate()
        {
            if (!CanSimulate) return;
            float delta = Time.fixedDeltaTime;
            SimulationTime += delta;
            foreach (PickupSpawner spawner in spawners) spawner.Step(delta);
            for (int i = liveWeapons.Count - 1; i >= 0; i--)
            {
                LiveCombatWeapon weapon = liveWeapons[i];
                if (weapon != null && weapon.Step(delta)) continue;
                RemoveWeapon(i);
            }
            foreach (PlayerCombat state in players)
            {
                if (state == null || state.Player.Vehicle == null) continue;
                bool oil = SimulationTime < state.OilUntil;
                bool impact = SimulationTime < state.ImpactUntil;
                state.Player.Vehicle.SetCombatModifiers(oil ? oilGripMultiplier : 1f,
                    impact ? impactMotorMultiplier : oil ? oilMotorMultiplier : 1f,
                    Mathf.Min(impact ? impactSteeringMultiplier : 1f, oil ? oilSteeringMultiplier : 1f));
                if (state.Feedback == null) continue;
                if (!oil && !impact)
                {
                    UnregisterVisualTree(state.Feedback);
                    Destroy(state.Feedback);
                    state.Feedback = null;
                }
                else state.Feedback.transform.Rotate(Vector3.up, 180f * delta);
            }
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                Burst burst = bursts[i];
                if (SimulationTime >= burst.Until || burst.Visual == null)
                {
                    if (burst.Visual != null)
                    {
                        UnregisterVisualTree(burst.Visual);
                        Destroy(burst.Visual);
                    }
                    bursts.RemoveAt(i);
                }
                else burst.Visual.transform.localScale = Vector3.one * (float)(.3f + 2f * (1f - (burst.Until - SimulationTime) / feedbackLifetime));
            }
        }

        internal bool TryHit(LiveCombatWeapon source, LocalPlayer target)
        {
            PlayerCombat state = players[target.PlayerIndex];
            if (!CanParticipate(state) || SimulationTime < state.SafeUntil) return false;
            if (source.Kind == WeaponKind.Bolt)
            {
                state.ImpactUntil = SimulationTime + impactDuration;
                Rigidbody body = target.Vehicle.GetComponent<Rigidbody>();
                body.AddForce(source.Direction * impactVelocityChange, ForceMode.VelocityChange);
            }
            else state.OilUntil = SimulationTime + oilDuration;
            if (state.Feedback != null)
            {
                UnregisterVisualTree(state.Feedback);
                Destroy(state.Feedback);
            }
            state.Feedback = new GameObject("Combat disruption feedback");
            state.Feedback.transform.SetParent(target.Vehicle.transform, false);
            BuildDisruptionFeedback(state.Feedback.transform, source.Kind);
            ShowBurst(target.Vehicle.transform.position + Vector3.up * .7f, source.Kind);
            WeaponHit?.Invoke(target, source.Kind);
            return true;
        }

        private void BuildDisruptionFeedback(Transform parent, WeaponKind kind)
        {
            if (kind == WeaponKind.Bolt)
            {
                for (int i = 0; i < BoltDisruptionRendererBudget; i++)
                {
                    float angle = i * 120f;
                    float radians = angle * Mathf.Deg2Rad;
                    GameObject arc = CreateVisual(parent, $"Bolt shock ray {i + 1}", PrimitiveType.Cube,
                        new Vector3(Mathf.Cos(radians) * .82f, 1.08f, Mathf.Sin(radians) * .82f),
                        new Vector3(.14f, .14f, .72f), i % 2 == 0 ? BoltMaterial : FeedbackMaterial, true);
                    arc.transform.localRotation = Quaternion.Euler(0f, -angle, i % 2 == 0 ? 24f : -24f);
                }
                return;
            }

            for (int i = 0; i < OilDisruptionRendererBudget; i++)
            {
                float angle = i * 120f;
                float radians = angle * Mathf.Deg2Rad;
                GameObject drop = CreateVisual(parent, $"Oil skid drop {i + 1}", PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(radians) * .76f, .72f + i * .1f, Mathf.Sin(radians) * .76f),
                    new Vector3(.34f, .5f, .2f), OilMaterial, true);
                drop.transform.localRotation = Quaternion.Euler(0f, -angle, 45f);
            }
        }

        internal void ShowBurst(Vector3 position, WeaponKind kind)
        {
            if (bursts.Count >= maximumFeedbackEffects) return;
            GameObject visual = new GameObject(kind == WeaponKind.Bolt ? "Bolt impact pulse" : "Oil impact pulse");
            visual.transform.SetParent(combatRoot.transform, false);
            visual.transform.localScale = Vector3.one * .3f;
            visual.transform.position = position;
            BuildBurstVisual(visual.transform, kind);
            bursts.Add(new Burst { Visual = visual, Until = SimulationTime + feedbackLifetime });
        }

        private void BuildBurstVisual(Transform parent, WeaponKind kind)
        {
            if (kind == WeaponKind.Bolt)
            {
                for (int i = 0; i < BurstRendererBudget; i++)
                {
                    float angle = i * 120f;
                    GameObject ray = CreateVisual(parent, $"Bolt pulse ray {i + 1}", PrimitiveType.Cube,
                        Quaternion.Euler(0f, angle, 0f) * Vector3.forward * .36f,
                        new Vector3(.1f, .1f, .72f), i == 1 ? FeedbackMaterial : BoltMaterial, true);
                    ray.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                }
                return;
            }

            CreateVisual(parent, "Oil pulse ring", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(.85f, .035f, .85f), OilMaterial, true);
            for (int i = 0; i < BurstRendererBudget - 1; i++)
            {
                float side = i == 0 ? -1f : 1f;
                GameObject drop = CreateVisual(parent, $"Oil pulse drop {i + 1}", PrimitiveType.Sphere,
                    new Vector3(side * .34f, .2f, .1f), new Vector3(.22f, .38f, .18f), FeedbackMaterial, true);
                drop.transform.localRotation = Quaternion.Euler(0f, 0f, side * 35f);
            }
        }

        private void PublishSlot(PlayerCombat state)
        {
            if (presentation != null) presentation.SetWeaponSlot(state.Player.PlayerIndex,
                state.Inventory.Held == WeaponKind.None ? null : state.Inventory.Held.ToString());
        }

        private void Recover(PlayerCombat state)
        {
            ClearDisruption(state);
            state.SafeUntil = SimulationTime + recoveryGrace;
        }

        private void ClearDisruption(PlayerCombat state)
        {
            state.ImpactUntil = state.OilUntil = state.SafeUntil = 0;
            if (state.Player.Vehicle != null) state.Player.Vehicle.ClearCombatModifiers();
            if (state.Feedback != null)
            {
                UnregisterVisualTree(state.Feedback);
                state.Feedback.SetActive(false);
                Destroy(state.Feedback);
            }
            state.Feedback = null;
        }

        private void ClearPlayer(PlayerCombat state)
        {
            state.Inventory.Reset();
            ClearDisruption(state);
            PublishSlot(state);
            for (int i = liveWeapons.Count - 1; i >= 0; i--)
                if (liveWeapons[i] == null || liveWeapons[i].Shooter == state.Player) RemoveWeapon(i);
        }

        private void RemoveWeapon(int index)
        {
            LiveCombatWeapon weapon = liveWeapons[index];
            if (weapon != null)
            {
                UnregisterVisualTree(weapon.gameObject);
                weapon.gameObject.SetActive(false);
                Destroy(weapon.gameObject);
            }
            liveWeapons.RemoveAt(index);
        }

        private void OnRacerFinished(RacerState racer)
        {
            if (racer.PlayerIndex >= 0 && racer.PlayerIndex < players.Length && players[racer.PlayerIndex] != null)
                ClearPlayer(players[racer.PlayerIndex]);
        }

        private void OnRaceStateChanged(RaceManager.RaceState state)
        {
            if (state != RaceManager.RaceState.Racing) ClearAllWeapons();
        }

        /// <summary>Full combat reset, including inventory, pickup sequence, timers, feedback and handling.</summary>
        public void ClearAllWeapons()
        {
            foreach (PlayerCombat state in players) if (state != null) ClearPlayer(state);
            for (int i = liveWeapons.Count - 1; i >= 0; i--) RemoveWeapon(i);
            foreach (Burst burst in bursts)
                if (burst.Visual != null)
                {
                    UnregisterVisualTree(burst.Visual);
                    burst.Visual.SetActive(false);
                    Destroy(burst.Visual);
                }
            bursts.Clear();
            SimulationTime = 0;
            foreach (PickupSpawner spawner in spawners) if (spawner != null) spawner.ResetPickup();
        }

        private void OnDisable()
        {
            if (!configured) return;
            ClearAllWeapons();
            if (subscribed)
            {
                if (race != null) { race.RaceStateChanged -= OnRaceStateChanged; race.RacerFinished -= OnRacerFinished; }
                if (vehicles != null)
                {
                    vehicles.PlayerJoined -= OnPlayerJoined;
                    vehicles.PlayerLeft -= OnPlayerLeft;
                    vehicles.FireRequested -= OnFireRequested;
                }
            }
            subscribed = false;
            for (int i = 0; i < players.Length; i++) OnPlayerLeft(i);
            if (combatRoot != null) combatRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (combatRoot != null)
            {
                UnregisterVisualTree(combatRoot);
                Destroy(combatRoot);
            }
            if (BoltMaterial != null) Destroy(BoltMaterial);
            if (OilMaterial != null) Destroy(OilMaterial);
            if (FeedbackMaterial != null) Destroy(FeedbackMaterial);
            if (darkOilMaterial != null) Destroy(darkOilMaterial);
        }

        internal GameObject CreateVisual(Transform parent, string label, PrimitiveType shape,
            Vector3 position, Vector3 scale, Material material, bool outlineEligible = false)
        {
            GameObject visual = GameObject.CreatePrimitive(shape);
            visual.name = label;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            Collider collider = visual.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            Renderer renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            visualStyle?.RegisterRenderer(renderer, outlineEligible);
            return visual;
        }

        internal void UpdateVisualSourceMaterial(Renderer renderer, Material sourceMaterial,
            ref Material currentSourceMaterial)
        {
            if (ReferenceEquals(currentSourceMaterial, sourceMaterial)) return;
            if (visualStyle != null) visualStyle.UpdateSourceMaterial(renderer, sourceMaterial);
            else renderer.sharedMaterial = sourceMaterial;
            currentSourceMaterial = sourceMaterial;
        }

        internal void UnregisterVisualTree(GameObject visualRoot)
        {
            if (visualStyle == null || visualRoot == null) return;
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) visualStyle.UnregisterRenderer(renderers[i]);
        }

        private static Material CreateMaterial(string label, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = label, color = color };
            material.SetColor("_BaseColor", color);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * .35f);
            return material;
        }
    }
}
