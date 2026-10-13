using System;
using System.Collections.Generic;
using AI;
using AudioPresentation;
using Core;
using UnityEngine;
using Vehicle;
using Weapons;

namespace Presentation
{
    public enum Stage9BVfxFamily
    {
        Collision = 0,
        SkidSmoke = 1,
        BoltTravel = 2,
        BoltImpact = 3,
        OilImpact = 4,
        PickupRecovery = 5
    }

    public readonly struct VfxPoolMetrics
    {
        public VfxPoolMetrics(int warmCapacity, int hardActiveCap, int created, int active,
            int highWater, int requests, int drops)
        {
            WarmCapacity = warmCapacity;
            HardActiveCap = hardActiveCap;
            Created = created;
            Active = active;
            HighWater = highWater;
            Requests = requests;
            Drops = drops;
        }

        public int WarmCapacity { get; }
        public int HardActiveCap { get; }
        public int Created { get; }
        public int Active { get; }
        public int HighWater { get; }
        public int Requests { get; }
        public int Drops { get; }
    }

    /// <summary>
    /// Bounded world-space feedback owner. It observes authority, never writes it, and owns no camera or HUD content.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage9BVfxPresentation : MonoBehaviour
    {
        public const int CollisionWarmCapacity = 12;
        public const int CollisionHardActiveCap = 12;
        public const int SkidWarmCapacity = 4;
        public const int SkidHardActiveCap = 4;
        public const int BoltTravelWarmCapacity = 16;
        public const int BoltTravelHardActiveCap = 16;
        public const int BoltImpactWarmCapacity = 12;
        public const int BoltImpactHardActiveCap = 12;
        public const int OilImpactWarmCapacity = 8;
        public const int OilImpactHardActiveCap = 8;
        public const int CueWarmCapacity = 8;
        public const int CueHardActiveCap = 8;
        public const int RenderersPerEntry = 3;

        private sealed class PooledEntry
        {
            internal GameObject Root;
            internal Transform[] Elements;
            internal Renderer[] Renderers;
            internal bool Active;
            internal float StartedAt;
            internal float ReleaseAt;
            internal int RecipientPlayerIndex = -1;
            internal Transform FollowTarget;
            internal LiveCombatWeapon TrackedWeapon;
            internal TrackSurfaceKind Surface;
        }

        private sealed class VfxPool
        {
            private readonly Stage9BVfxPresentation owner;
            private readonly Stage9BVfxFamily family;
            private readonly List<PooledEntry> entries;
            private int active;

            internal VfxPool(Stage9BVfxPresentation owner, Stage9BVfxFamily family, int warmCapacity,
                int hardActiveCap)
            {
                this.owner = owner;
                this.family = family;
                WarmCapacity = Mathf.Clamp(warmCapacity, 0, hardActiveCap);
                HardActiveCap = Mathf.Max(1, hardActiveCap);
                entries = new List<PooledEntry>(HardActiveCap);
                for (int i = 0; i < WarmCapacity; i++) entries.Add(owner.CreateEntry(family, i));
            }

            internal int WarmCapacity { get; }
            internal int HardActiveCap { get; }
            internal int HighWater { get; private set; }
            internal int Requests { get; private set; }
            internal int Drops { get; private set; }
            internal int Active => active;
            internal IReadOnlyList<PooledEntry> Entries => entries;

            internal PooledEntry Acquire()
            {
                Requests++;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Active) continue;
                    Activate(entries[i]);
                    return entries[i];
                }

                if (entries.Count < HardActiveCap)
                {
                    PooledEntry entry = owner.CreateEntry(family, entries.Count);
                    entries.Add(entry);
                    Activate(entry);
                    return entry;
                }

                Drops++;
                return null;
            }

            private void Activate(PooledEntry entry)
            {
                owner.ResetEntry(entry);
                entry.Active = true;
                entry.Root.SetActive(true);
                active++;
                if (active > HighWater) HighWater = active;
            }

            internal void Release(PooledEntry entry)
            {
                if (entry == null || !entry.Active) return;
                owner.ResetEntry(entry);
                entry.Active = false;
                entry.Root.SetActive(false);
                active--;
            }

            internal void ReleaseAll()
            {
                for (int i = 0; i < entries.Count; i++) Release(entries[i]);
            }

            internal void Dispose()
            {
                ReleaseAll();
                for (int i = 0; i < entries.Count; i++) owner.DisposeEntry(entries[i]);
                entries.Clear();
            }

            internal VfxPoolMetrics Metrics => new(WarmCapacity, HardActiveCap, entries.Count, active,
                HighWater, Requests, Drops);
        }

        private sealed class HumanBinding
        {
            internal LocalPlayer Player;
            internal VehicleImpactAudioEmitter Impact;
            internal Action<RecoveryPresentationPayload> Recovered;
        }

        private sealed class AIBinding
        {
            internal AIVehiclePresentationHandle Handle;
            internal Action<RecoveryPresentationPayload> Recovered;
        }

        [SerializeField, Min(0f)] private float minimumCollisionImpulse = .75f;

        private readonly VfxPool[] pools = new VfxPool[6];
        private readonly HumanBinding[] humans = new HumanBinding[LocalPlayerRules.MaximumPlayers];
        private readonly List<AIBinding> bots = new(3);
        private readonly PooledEntry[] skidByPlayer = new PooledEntry[LocalPlayerRules.MaximumPlayers];
        private readonly RaycastHit[] surfaceHits = new RaycastHit[16];
        private readonly MaterialPropertyBlock properties = new();
        private RaceManager race;
        private VehicleManager vehicles;
        private AIManager ai;
        private WeaponManager weapons;
        private VisualStyleController visualStyle;
        private GameObject vfxRoot;
        private Material effectMaterial;
        private bool configured;
        private bool subscribed;
        private VfxDensityLevel density = VfxDensityLevel.Full;

        public VfxDensityLevel Density => density;
        public int SuppressedRequestCount { get; private set; }
        public int SurfaceClassificationMissCount { get; private set; }
        public int RegisteredSourceRendererCount { get; private set; }

        public void Configure(RaceManager raceManager, VehicleManager humanRoster, AIManager aiRoster,
            WeaponManager weaponManager, VisualStyleController styleController, VfxDensityLevel initialDensity)
        {
            if (configured) throw new InvalidOperationException("Stage 9B VFX is already configured.");
            race = raceManager != null ? raceManager : throw new ArgumentNullException(nameof(raceManager));
            vehicles = humanRoster != null ? humanRoster : throw new ArgumentNullException(nameof(humanRoster));
            weapons = weaponManager != null ? weaponManager : throw new ArgumentNullException(nameof(weaponManager));
            visualStyle = styleController != null
                ? styleController
                : throw new ArgumentNullException(nameof(styleController));
            ai = aiRoster;
            ValidateDensity(initialDensity);
            density = initialDensity;

            vfxRoot = new GameObject("Stage9B_PooledVfx");
            vfxRoot.transform.SetParent(transform, false);
            effectMaterial = CreateEffectMaterial();
            pools[(int)Stage9BVfxFamily.Collision] = new VfxPool(this, Stage9BVfxFamily.Collision,
                CollisionWarmCapacity, CollisionHardActiveCap);
            pools[(int)Stage9BVfxFamily.SkidSmoke] = new VfxPool(this, Stage9BVfxFamily.SkidSmoke,
                SkidWarmCapacity, SkidHardActiveCap);
            pools[(int)Stage9BVfxFamily.BoltTravel] = new VfxPool(this, Stage9BVfxFamily.BoltTravel,
                BoltTravelWarmCapacity, BoltTravelHardActiveCap);
            pools[(int)Stage9BVfxFamily.BoltImpact] = new VfxPool(this, Stage9BVfxFamily.BoltImpact,
                BoltImpactWarmCapacity, BoltImpactHardActiveCap);
            pools[(int)Stage9BVfxFamily.OilImpact] = new VfxPool(this, Stage9BVfxFamily.OilImpact,
                OilImpactWarmCapacity, OilImpactHardActiveCap);
            pools[(int)Stage9BVfxFamily.PickupRecovery] = new VfxPool(this, Stage9BVfxFamily.PickupRecovery,
                CueWarmCapacity, CueHardActiveCap);
            configured = true;
            if (isActiveAndEnabled)
            {
                weapons.SetExternalTransientVfxOwner(true);
                Subscribe();
            }
        }

        public void SetDensity(VfxDensityLevel level)
        {
            ValidateDensity(level);
            if (density != level) ClearActiveEffects();
            density = level;
        }

        public VfxPoolMetrics GetPoolMetrics(Stage9BVfxFamily family)
        {
            int index = (int)family;
            if (index < 0 || index >= pools.Length) throw new ArgumentOutOfRangeException(nameof(family));
            VfxPool pool = pools[index];
            return pool != null ? pool.Metrics : default;
        }

        public int GetActiveElementCount(Stage9BVfxFamily family)
        {
            VfxPool pool = GetPool(family);
            int count = 0;
            IReadOnlyList<PooledEntry> entries = pool.Entries;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Active)
                    for (int rendererIndex = 0; rendererIndex < entries[i].Renderers.Length; rendererIndex++)
                        if (entries[i].Renderers[rendererIndex].enabled) count++;
            return count;
        }

        public int GetActiveRecipientCount(int playerIndex)
        {
            int count = 0;
            for (int poolIndex = 0; poolIndex < pools.Length; poolIndex++)
            {
                VfxPool pool = pools[poolIndex];
                if (pool == null) continue;
                IReadOnlyList<PooledEntry> entries = pool.Entries;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                    if (entries[entryIndex].Active && entries[entryIndex].RecipientPlayerIndex == playerIndex) count++;
            }
            return count;
        }

        public bool TryPresentCollision(VehicleImpactPayload payload)
        {
            if (!CanPresent()) return Suppress();
            if (payload.Impulse < minimumCollisionImpulse) return false;
            if (!TryResolveCollisionSurface(payload.OtherCollider, out TrackSurfaceKind surface))
            {
                SurfaceClassificationMissCount++;
                return false;
            }
            if (payload.Normal.sqrMagnitude < .0001f) return false;
            PooledEntry entry = Acquire(Stage9BVfxFamily.Collision);
            if (entry == null) return false;
            float strength = Mathf.Clamp01(payload.Impulse / 12f);
            int recipient = payload.HasLocalPlayer
                ? payload.PlayerIndex
                : WorldRecipient(payload.PresentationSubjectId);
            ConfigureEntry(entry, recipient, payload.Point,
                Quaternion.FromToRotation(Vector3.up, payload.Normal.normalized), CollisionColor(surface),
                Mathf.Lerp(.75f, 1.35f, strength), ScaledDuration(.28f));
            entry.Surface = surface;
            return true;
        }

        public bool TryPresentWeaponResolved(WeaponResolvedPresentationPayload payload)
        {
            if (!CanPresent()) return Suppress();
            if (payload.Direction.sqrMagnitude < .0001f) return false;
            Stage9BVfxFamily family = payload.Kind == WeaponKind.Bolt
                ? Stage9BVfxFamily.BoltImpact
                : Stage9BVfxFamily.OilImpact;
            if (payload.Kind == WeaponKind.Oil && !payload.AcceptedHit) return false;
            PooledEntry entry = Acquire(family);
            if (entry == null) return false;
            Color color = payload.Kind == WeaponKind.Bolt
                ? new Color(1f, .48f, .08f)
                : new Color(.64f, .18f, .92f);
            ConfigureEntry(entry, payload.TargetPlayerIndex, payload.Point,
                Quaternion.LookRotation(payload.Direction.normalized, Vector3.up), color, 1f,
                ScaledDuration(payload.Kind == WeaponKind.Bolt ? .35f : .45f));
            return true;
        }

        public bool TryPresentPickup(PickupAcquiredPresentationPayload payload)
        {
            if (!CanPresent()) return Suppress();
            PooledEntry entry = Acquire(Stage9BVfxFamily.PickupRecovery);
            if (entry == null) return false;
            Color color = payload.Kind == WeaponKind.Bolt
                ? new Color(1f, .55f, .08f)
                : new Color(.68f, .2f, .94f);
            ConfigureEntry(entry, payload.CollectorPlayerIndex, payload.Point, Quaternion.identity, color, 1f,
                ScaledDuration(.45f));
            return true;
        }

        public bool TryPresentRecovery(int playerIndex, RecoveryPresentationPayload payload)
        {
            if (!CanPresent()) return Suppress();
            PooledEntry entry = Acquire(Stage9BVfxFamily.PickupRecovery);
            if (entry == null) return false;
            ConfigureEntry(entry, playerIndex, payload.Destination.position, payload.Destination.rotation,
                new Color(.35f, .92f, 1f), 1.25f, ScaledDuration(.65f));
            return true;
        }

        public void ClearActiveEffects()
        {
            for (int i = 0; i < pools.Length; i++) pools[i]?.ReleaseAll();
            for (int i = 0; i < skidByPlayer.Length; i++) skidByPlayer[i] = null;
        }

        private bool TryPresentBoltTravel(WeaponSpawnedPresentationPayload payload)
        {
            if (payload.Kind != WeaponKind.Bolt || payload.Weapon == null) return false;
            if (!CanPresent()) return Suppress();
            PooledEntry entry = Acquire(Stage9BVfxFamily.BoltTravel);
            if (entry == null) return false;
            ConfigureEntry(entry, payload.SourcePlayerIndex, payload.Point,
                Quaternion.LookRotation(payload.Direction.normalized, Vector3.up), new Color(1f, .45f, .06f),
                1f, float.PositiveInfinity);
            entry.FollowTarget = payload.Weapon.transform;
            entry.TrackedWeapon = payload.Weapon;
            return true;
        }

        private void Subscribe()
        {
            if (!configured || subscribed) return;
            race.RaceStateChanged += OnRaceStateChanged;
            vehicles.PlayerJoined += OnPlayerJoined;
            vehicles.PlayerLeft += OnPlayerLeft;
            weapons.WeaponPresentationSpawned += OnWeaponSpawned;
            weapons.WeaponPresentationRemoved += OnWeaponRemoved;
            weapons.WeaponPresentationResolved += OnWeaponResolved;
            weapons.PickupPresentationAcquired += OnPickupAcquired;
            weapons.CombatCleared += OnCombatCleared;
            if (ai != null)
            {
                ai.BotPresentationAdded += OnBotAdded;
                ai.BotPresentationRemoving += OnBotRemoving;
            }
            subscribed = true;
            for (int i = 0; i < vehicles.Players.Count; i++) OnPlayerJoined(vehicles.Players[i]);
            if (ai != null)
                for (int i = 0; i < ai.Drivers.Count; i++)
                {
                    AIDriverController driver = ai.Drivers[i];
                    if (driver == null) continue;
                    VehicleController vehicle = driver.GetComponent<VehicleController>();
                    VehicleRecovery recovery = driver.GetComponent<VehicleRecovery>();
                    VehicleImpactAudioEmitter impact = driver.GetComponent<VehicleImpactAudioEmitter>();
                    if (vehicle != null && recovery != null && impact != null)
                        OnBotAdded(new AIVehiclePresentationHandle(i, vehicle, recovery, impact));
                }
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            race.RaceStateChanged -= OnRaceStateChanged;
            vehicles.PlayerJoined -= OnPlayerJoined;
            vehicles.PlayerLeft -= OnPlayerLeft;
            weapons.WeaponPresentationSpawned -= OnWeaponSpawned;
            weapons.WeaponPresentationRemoved -= OnWeaponRemoved;
            weapons.WeaponPresentationResolved -= OnWeaponResolved;
            weapons.PickupPresentationAcquired -= OnPickupAcquired;
            weapons.CombatCleared -= OnCombatCleared;
            if (ai != null)
            {
                ai.BotPresentationAdded -= OnBotAdded;
                ai.BotPresentationRemoving -= OnBotRemoving;
            }
            for (int i = 0; i < humans.Length; i++) RemoveHumanBinding(i);
            for (int i = bots.Count - 1; i >= 0; i--) RemoveBotBinding(bots[i].Handle);
            subscribed = false;
        }

        private void OnPlayerJoined(LocalPlayer player)
        {
            if (player == null || player.PlayerIndex < 0 || player.PlayerIndex >= humans.Length ||
                humans[player.PlayerIndex] != null) return;
            VehicleImpactAudioEmitter impact = player.Vehicle.GetComponent<VehicleImpactAudioEmitter>();
            HumanBinding binding = new() { Player = player, Impact = impact };
            binding.Recovered = payload => OnRecovered(player.PlayerIndex, payload);
            if (impact != null) impact.ImpactObserved += OnImpactObserved;
            player.Recovery.RecoveredAt += binding.Recovered;
            humans[player.PlayerIndex] = binding;
        }

        private void OnPlayerLeft(int playerIndex)
        {
            RemoveHumanBinding(playerIndex);
            ReleaseRecipient(playerIndex);
        }

        private void RemoveHumanBinding(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= humans.Length) return;
            HumanBinding binding = humans[playerIndex];
            if (binding == null) return;
            if (binding.Impact != null) binding.Impact.ImpactObserved -= OnImpactObserved;
            if (binding.Player?.Recovery != null) binding.Player.Recovery.RecoveredAt -= binding.Recovered;
            humans[playerIndex] = null;
            skidByPlayer[playerIndex] = null;
        }

        private void OnBotAdded(AIVehiclePresentationHandle handle)
        {
            if (handle.Vehicle == null || handle.Recovery == null || handle.ImpactEmitter == null) return;
            for (int i = 0; i < bots.Count; i++)
                if (bots[i].Handle.Vehicle == handle.Vehicle) return;
            AIBinding binding = new() { Handle = handle };
            int recipient = WorldRecipient(handle.ImpactEmitter.PresentationSubjectId);
            binding.Recovered = payload => OnRecovered(recipient, payload);
            handle.ImpactEmitter.ImpactObserved += OnImpactObserved;
            handle.Recovery.RecoveredAt += binding.Recovered;
            bots.Add(binding);
        }

        private void OnBotRemoving(AIVehiclePresentationHandle handle) => RemoveBotBinding(handle);

        private void RemoveBotBinding(AIVehiclePresentationHandle handle)
        {
            for (int i = bots.Count - 1; i >= 0; i--)
            {
                AIBinding binding = bots[i];
                if (binding.Handle.Vehicle != handle.Vehicle) continue;
                if (binding.Handle.ImpactEmitter != null)
                    binding.Handle.ImpactEmitter.ImpactObserved -= OnImpactObserved;
                if (binding.Handle.Recovery != null)
                    binding.Handle.Recovery.RecoveredAt -= binding.Recovered;
                if (binding.Handle.ImpactEmitter != null)
                    ReleaseRecipient(WorldRecipient(binding.Handle.ImpactEmitter.PresentationSubjectId));
                bots.RemoveAt(i);
            }
        }

        private void OnImpactObserved(VehicleImpactPayload payload)
        {
            if (race.RaceRunning) TryPresentCollision(payload);
        }

        private void OnRecovered(int playerIndex, RecoveryPresentationPayload payload)
        {
            ReleaseRecipient(playerIndex);
            if (race.RaceRunning) TryPresentRecovery(playerIndex, payload);
        }

        private void OnWeaponSpawned(WeaponSpawnedPresentationPayload payload) => TryPresentBoltTravel(payload);
        private void OnWeaponRemoved(LiveCombatWeapon weapon) => ReleaseTrackedWeapon(weapon);
        private void OnWeaponResolved(WeaponResolvedPresentationPayload payload) => TryPresentWeaponResolved(payload);
        private void OnPickupAcquired(PickupAcquiredPresentationPayload payload) => TryPresentPickup(payload);
        private void OnCombatCleared() => ClearActiveEffects();

        private void OnRaceStateChanged(RaceManager.RaceState state)
        {
            if (state != RaceManager.RaceState.Racing) ClearActiveEffects();
        }

        private void Update()
        {
            if (!configured) return;
            float now = Time.time;
            for (int poolIndex = 0; poolIndex < pools.Length; poolIndex++)
            {
                VfxPool pool = pools[poolIndex];
                IReadOnlyList<PooledEntry> entries = pool.Entries;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    PooledEntry entry = entries[entryIndex];
                    if (!entry.Active) continue;
                    if (poolIndex == (int)Stage9BVfxFamily.BoltTravel && entry.FollowTarget == null)
                    {
                        pool.Release(entry);
                        continue;
                    }
                    if (entry.TrackedWeapon != null && !entry.TrackedWeapon.gameObject.activeInHierarchy)
                    {
                        pool.Release(entry);
                        continue;
                    }
                    if (entry.TrackedWeapon == null && entry.FollowTarget != null && !entry.FollowTarget.gameObject.activeInHierarchy)
                    {
                        pool.Release(entry);
                        continue;
                    }
                    if (entry.FollowTarget != null)
                    {
                        entry.Root.transform.SetPositionAndRotation(entry.FollowTarget.position,
                            entry.FollowTarget.rotation);
                    }
                    if (now >= entry.ReleaseAt)
                    {
                        pool.Release(entry);
                        continue;
                    }
                    AnimateEntry(entry, (Stage9BVfxFamily)poolIndex, now);
                }
            }
            UpdateSkidSmoke();
        }

        private void UpdateSkidSmoke()
        {
            if (density == VfxDensityLevel.Off || !race.RaceRunning || Time.timeScale <= 0f)
            {
                ReleaseSkids();
                return;
            }

            for (int playerIndex = 0; playerIndex < humans.Length; playerIndex++)
            {
                HumanBinding binding = humans[playerIndex];
                VehicleController vehicle = binding?.Player?.Vehicle;
                if (vehicle == null || !vehicle.isActiveAndEnabled || !vehicle.IsDrifting ||
                    !TryFindExplicitSurface(vehicle.transform.position + Vector3.up * .5f,
                        out RaycastHit hit, out TrackSurfaceKind surface))
                {
                    ReleaseSkid(playerIndex);
                    continue;
                }

                PooledEntry entry = skidByPlayer[playerIndex];
                if (entry == null || !entry.Active)
                {
                    entry = Acquire(Stage9BVfxFamily.SkidSmoke);
                    if (entry == null) continue;
                    skidByPlayer[playerIndex] = entry;
                    ConfigureEntry(entry, playerIndex, hit.point + hit.normal * .04f,
                        Quaternion.FromToRotation(Vector3.up, hit.normal), CollisionColor(surface), 1f,
                        float.PositiveInfinity);
                }
                if (entry.Surface != surface) ApplyEntryColor(entry, CollisionColor(surface));
                entry.Surface = surface;
                entry.FollowTarget = vehicle.transform;
                entry.Root.transform.position = hit.point + hit.normal * .04f;
                entry.Root.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
        }

        private bool TryFindExplicitSurface(Vector3 origin, out RaycastHit nearestHit, out TrackSurfaceKind surface)
        {
            nearestHit = default;
            surface = TrackSurfaceKind.Unclassified;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, surfaceHits, 2.5f, ~0,
                QueryTriggerInteraction.Ignore);
            if (count == surfaceHits.Length) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (surfaceHits[i].distance >= nearest ||
                    !TrackSurfaceMarker.TryResolve(surfaceHits[i].collider, out TrackSurfaceKind candidate)) continue;
                nearest = surfaceHits[i].distance;
                nearestHit = surfaceHits[i];
                surface = candidate;
            }
            return surface != TrackSurfaceKind.Unclassified;
        }

        private static bool TryResolveCollisionSurface(Collider otherCollider, out TrackSurfaceKind surface)
        {
            if (otherCollider != null && otherCollider.GetComponentInParent<VehicleController>() != null)
            {
                surface = TrackSurfaceKind.VehicleContact;
                return true;
            }

            return TrackSurfaceMarker.TryResolve(otherCollider, out surface);
        }

        private void ReleaseSkids()
        {
            for (int i = 0; i < skidByPlayer.Length; i++) ReleaseSkid(i);
        }

        private void ReleaseSkid(int playerIndex)
        {
            PooledEntry entry = skidByPlayer[playerIndex];
            if (entry != null) GetPool(Stage9BVfxFamily.SkidSmoke).Release(entry);
            skidByPlayer[playerIndex] = null;
        }

        private void ReleaseTrackedWeapon(LiveCombatWeapon weapon)
        {
            if (weapon == null) return;
            VfxPool pool = GetPool(Stage9BVfxFamily.BoltTravel);
            IReadOnlyList<PooledEntry> entries = pool.Entries;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Active && entries[i].TrackedWeapon == weapon) pool.Release(entries[i]);
        }

        private void ReleaseRecipient(int playerIndex)
        {
            for (int poolIndex = 0; poolIndex < pools.Length; poolIndex++)
            {
                VfxPool pool = pools[poolIndex];
                IReadOnlyList<PooledEntry> entries = pool.Entries;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                    if (entries[entryIndex].Active && entries[entryIndex].RecipientPlayerIndex == playerIndex)
                        pool.Release(entries[entryIndex]);
            }
            if (playerIndex >= 0 && playerIndex < skidByPlayer.Length) skidByPlayer[playerIndex] = null;
        }

        private PooledEntry Acquire(Stage9BVfxFamily family) => GetPool(family).Acquire();

        private VfxPool GetPool(Stage9BVfxFamily family)
        {
            int index = (int)family;
            if (index < 0 || index >= pools.Length) throw new ArgumentOutOfRangeException(nameof(family));
            VfxPool pool = pools[index];
            return pool ?? throw new InvalidOperationException("Stage 9B VFX has not been configured.");
        }

        private bool CanPresent() => configured && isActiveAndEnabled && density != VfxDensityLevel.Off;

        private bool Suppress()
        {
            SuppressedRequestCount++;
            return false;
        }

        private float ScaledDuration(float fullDuration) => density == VfxDensityLevel.Reduced
            ? fullDuration * .65f
            : fullDuration;

        private void ConfigureEntry(PooledEntry entry, int recipientPlayerIndex, Vector3 point,
            Quaternion rotation, Color color, float scale, float duration)
        {
            entry.RecipientPlayerIndex = recipientPlayerIndex;
            entry.StartedAt = Time.time;
            entry.ReleaseAt = float.IsPositiveInfinity(duration) ? float.PositiveInfinity : Time.time + duration;
            entry.Root.transform.SetPositionAndRotation(point, rotation);
            entry.Root.transform.localScale = Vector3.one * scale;
            int visibleElements = density == VfxDensityLevel.Reduced ? 1 : RenderersPerEntry;
            for (int i = 0; i < entry.Renderers.Length; i++)
                entry.Renderers[i].enabled = i < visibleElements;
            ApplyEntryColor(entry, color);
        }

        private void ApplyEntryColor(PooledEntry entry, Color color)
        {
            for (int i = 0; i < entry.Renderers.Length; i++)
            {
                properties.Clear();
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                properties.SetColor("_EmissionColor", color * .35f);
                entry.Renderers[i].SetPropertyBlock(properties);
            }
        }

        private PooledEntry CreateEntry(Stage9BVfxFamily family, int index)
        {
            GameObject root = new GameObject($"{family}_{index + 1:00}");
            root.transform.SetParent(vfxRoot.transform, false);
            Transform[] elements = new Transform[RenderersPerEntry];
            Renderer[] renderers = new Renderer[RenderersPerEntry];
            for (int i = 0; i < RenderersPerEntry; i++)
            {
                PrimitiveType primitive = family == Stage9BVfxFamily.OilImpact ||
                                          family == Stage9BVfxFamily.SkidSmoke
                    ? PrimitiveType.Sphere
                    : PrimitiveType.Cube;
                GameObject element = GameObject.CreatePrimitive(primitive);
                element.name = $"{family}_Element_{i + 1}";
                element.transform.SetParent(root.transform, false);
                Collider collider = element.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                    DestroyOwned(collider);
                }
                Renderer renderer = element.GetComponent<Renderer>();
                renderer.sharedMaterial = effectMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                visualStyle.RegisterRenderer(renderer, false);
                RegisteredSourceRendererCount++;
                elements[i] = element.transform;
                renderers[i] = renderer;
            }
            LayoutElements(family, elements);
            root.SetActive(false);
            return new PooledEntry { Root = root, Elements = elements, Renderers = renderers };
        }

        private static void LayoutElements(Stage9BVfxFamily family, Transform[] elements)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                float side = i - 1f;
                elements[i].localPosition = family switch
                {
                    Stage9BVfxFamily.Collision => new Vector3(side * .24f, .08f + i * .06f, .12f * i),
                    Stage9BVfxFamily.SkidSmoke => new Vector3(side * .32f, .08f, -.2f * i),
                    Stage9BVfxFamily.BoltTravel => new Vector3(side * .16f, 0f, -.42f - i * .22f),
                    Stage9BVfxFamily.BoltImpact => Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * .32f,
                    Stage9BVfxFamily.OilImpact => new Vector3(side * .32f, .12f + i * .08f, .12f * (i % 2)),
                    _ => Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * .42f
                };
                elements[i].localRotation = family == Stage9BVfxFamily.BoltImpact
                    ? Quaternion.Euler(0f, i * 120f, 18f)
                    : Quaternion.Euler(0f, i * 37f, i % 2 == 0 ? 20f : -20f);
                elements[i].localScale = family switch
                {
                    Stage9BVfxFamily.Collision => new Vector3(.08f, .08f, .42f),
                    Stage9BVfxFamily.SkidSmoke => Vector3.one * (.22f + i * .06f),
                    Stage9BVfxFamily.BoltTravel => new Vector3(.08f, .08f, .3f),
                    Stage9BVfxFamily.BoltImpact => new Vector3(.09f, .09f, .65f),
                    Stage9BVfxFamily.OilImpact => new Vector3(.26f, .16f, .2f),
                    _ => new Vector3(.16f, .16f, .42f)
                };
            }
        }

        private static void AnimateEntry(PooledEntry entry, Stage9BVfxFamily family, float now)
        {
            float pulse = 1f + Mathf.Sin(now * (family == Stage9BVfxFamily.SkidSmoke ? 5f : 12f)) * .08f;
            if (family == Stage9BVfxFamily.BoltTravel)
                entry.Root.transform.Rotate(0f, 0f, 180f * Time.deltaTime, Space.Self);
            else if (family == Stage9BVfxFamily.SkidSmoke)
                entry.Root.transform.localScale = Vector3.one * pulse;
            else
            {
                float duration = entry.ReleaseAt - entry.StartedAt;
                float normalized = duration > 0f && !float.IsInfinity(duration)
                    ? Mathf.Clamp01((now - entry.StartedAt) / duration)
                    : 0f;
                entry.Root.transform.localScale *= 1f + Time.deltaTime * Mathf.Lerp(1.2f, .1f, normalized);
            }
        }

        private void ResetEntry(PooledEntry entry)
        {
            entry.RecipientPlayerIndex = -1;
            entry.FollowTarget = null;
            entry.TrackedWeapon = null;
            entry.StartedAt = 0f;
            entry.ReleaseAt = 0f;
            entry.Surface = TrackSurfaceKind.Unclassified;
            entry.Root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            entry.Root.transform.localScale = Vector3.one;
            for (int i = 0; i < entry.Renderers.Length; i++)
            {
                entry.Renderers[i].enabled = false;
                entry.Renderers[i].SetPropertyBlock(null);
            }
        }

        private void DisposeEntry(PooledEntry entry)
        {
            if (entry == null) return;
            for (int i = 0; i < entry.Renderers.Length; i++)
            {
                if (entry.Renderers[i] == null) continue;
                visualStyle.UnregisterRenderer(entry.Renderers[i]);
                RegisteredSourceRendererCount--;
            }
        }

        private static Color CollisionColor(TrackSurfaceKind surface) => surface switch
        {
            TrackSurfaceKind.Metal => new Color(1f, .62f, .12f),
            TrackSurfaceKind.CuttingMat => new Color(.62f, .78f, .58f),
            TrackSurfaceKind.Foam => new Color(1f, .82f, .38f),
            TrackSurfaceKind.Cardboard => new Color(.72f, .46f, .24f),
            TrackSurfaceKind.Rubber => new Color(.36f, .38f, .42f),
            TrackSurfaceKind.VehicleContact => new Color(.82f, .92f, 1f),
            _ => new Color(.7f, .5f, .3f)
        };

        private static int WorldRecipient(int presentationSubjectId) => -Mathf.Max(0, presentationSubjectId) - 2;

        private static Material CreateEffectMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new(shader)
            {
                name = "Stage 9B Shared VFX",
                color = Color.white,
                enableInstancing = true,
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .15f);
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.white * .2f);
            return material;
        }

        private static void ValidateDensity(VfxDensityLevel level)
        {
            if (!Enum.IsDefined(typeof(VfxDensityLevel), level)) throw new ArgumentOutOfRangeException(nameof(level));
        }

        private static void DestroyOwned(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void OnEnable()
        {
            if (!configured) return;
            if (weapons != null) weapons.SetExternalTransientVfxOwner(true);
            Subscribe();
        }

        private void OnDisable()
        {
            if (!configured) return;
            ClearActiveEffects();
            Unsubscribe();
            if (weapons != null) weapons.SetExternalTransientVfxOwner(false);
        }

        private void OnDestroy()
        {
            if (!configured) return;
            Unsubscribe();
            ClearActiveEffects();
            if (weapons != null) weapons.SetExternalTransientVfxOwner(false);
            for (int i = 0; i < pools.Length; i++)
            {
                pools[i]?.Dispose();
                pools[i] = null;
            }
            if (effectMaterial != null) DestroyOwned(effectMaterial);
            if (vfxRoot != null) DestroyOwned(vfxRoot);
            configured = false;
        }
    }
}
