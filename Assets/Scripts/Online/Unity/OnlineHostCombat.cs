using System;
using System.Collections.Generic;
using Core;
using ReReVolt.Online.Contracts;
using UnityEngine;
using Vehicle;
using Weapons;

namespace ReReVolt.Online.Unity
{
    /// <summary>Round-scoped host combat simulation. Clients submit Fire edges, never hits or modifiers.</summary>
    public sealed class OnlineHostCombat
    {
        private const int MaximumLiveWeapons = 16;
        private const float BoltSpeed = 32f, BoltLifetime = 3f, BoltRadius = .3f;
        private const float OilLifetime = 9f, OilRadius = 1.35f, OilArming = .6f, OilShooterGrace = 1.2f;
        private const double ImpactDuration = 1, OilDuration = 2.5, RecoveryGrace = .75;

        private sealed class Racer
        {
            internal uint Id;
            internal VehicleController Vehicle;
            internal Rigidbody Body;
            internal RacerState Progress;
            internal double ImpactUntil, OilUntil, SafeUntil;
        }

        private sealed class LiveWeapon
        {
            internal uint Id;
            internal uint ShooterId;
            internal WeaponKind Kind;
            internal Vector3 Position, Direction;
            internal float Age;
        }

        private readonly OnlineCombatInventoryAuthority inventory;
        private readonly RaceManager race;
        private readonly LayerMask drivableMask;
        private readonly List<Racer> racers = new List<Racer>(4);
        private readonly List<LiveWeapon> weapons = new List<LiveWeapon>(MaximumLiveWeapons);
        private readonly Collider[] overlaps = new Collider[32];
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private uint nextWeaponId;

        public double SimulationTime { get; private set; }
        public int LiveCount => weapons.Count;
        public int HitCount { get; private set; }

        public OnlineHostCombat(OnlineCombatInventoryAuthority hostInventory, RaceManager hostRace, LayerMask groundMask)
        {
            inventory = hostInventory ?? throw new ArgumentNullException(nameof(hostInventory));
            race = hostRace ?? throw new ArgumentNullException(nameof(hostRace));
            drivableMask = groundMask;
        }

        public void Register(uint id, VehicleController vehicle, Rigidbody body, RacerState progress)
        {
            if (vehicle == null || body == null || progress == null) throw new ArgumentNullException();
            for (int i = 0; i < racers.Count; i++)
                if (racers[i].Id == id) throw new ArgumentException("Racer ID already registered.", nameof(id));
            racers.Add(new Racer { Id = id, Vehicle = vehicle, Body = body, Progress = progress });
        }

        public bool TryFire(uint shooterId)
        {
            if (!CanSimulate || weapons.Count >= MaximumLiveWeapons || !inventory.CanFire(shooterId, race.ElapsedTime))
                return false;
            Racer shooter = Find(shooterId);
            if (shooter == null || shooter.Progress.IsFinished || shooter.Vehicle == null ||
                !shooter.Vehicle.isActiveAndEnabled) return false;
            Transform car = shooter.Vehicle.transform;
            Vector3 forward = Vector3.ProjectOnPlane(car.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) return false;
            WeaponKind kind = inventory.HeldWeapon(shooterId);
            Vector3 position = car.position + forward * 1.25f + Vector3.up * .15f;
            if (kind == WeaponKind.Oil)
            {
                Vector3 drop = car.position - forward * 2.7f;
                if (!Physics.Raycast(drop + Vector3.up * 2f, Vector3.down, out RaycastHit ground,
                    4f, drivableMask, QueryTriggerInteraction.Ignore) || ground.normal.y < .8f) return false;
                position = ground.point + Vector3.up * .055f;
            }
            if (!inventory.TryFire(shooterId, race.ElapsedTime, out kind)) return false;
            weapons.Add(new LiveWeapon { Id = ++nextWeaponId, ShooterId = shooterId, Kind = kind,
                Position = position, Direction = forward });
            return true;
        }

        public DrivingWeaponState[] CaptureWeapons()
        {
            var result = new DrivingWeaponState[weapons.Count];
            for (int i = 0; i < weapons.Count; i++)
            {
                LiveWeapon weapon = weapons[i];
                result[i] = new DrivingWeaponState
                {
                    Id = weapon.Id, ShooterId = weapon.ShooterId, Kind = weapon.Kind,
                    Position = new WorldVector(weapon.Position.x, weapon.Position.y, weapon.Position.z),
                    Direction = new WorldVector(weapon.Direction.x, weapon.Direction.y, weapon.Direction.z),
                    Age = weapon.Age
                };
            }
            return result;
        }

        public void Step(float delta)
        {
            if (!CanSimulate || delta <= 0f) return;
            SimulationTime += delta;
            for (int i = weapons.Count - 1; i >= 0; i--)
                if (!StepWeapon(weapons[i], delta)) weapons.RemoveAt(i);
            for (int i = 0; i < racers.Count; i++) ApplyModifiers(racers[i]);
        }

        public void Recover(uint racerId)
        {
            Racer racer = Find(racerId);
            if (racer == null) return;
            racer.ImpactUntil = racer.OilUntil = 0;
            racer.SafeUntil = SimulationTime + RecoveryGrace;
            if (racer.Vehicle != null) racer.Vehicle.ClearCombatModifiers();
        }

        public void Finish(uint racerId)
        {
            Racer racer = Find(racerId);
            if (racer == null) return;
            racer.ImpactUntil = racer.OilUntil = racer.SafeUntil = 0;
            if (racer.Vehicle != null) racer.Vehicle.ClearCombatModifiers();
            for (int i = weapons.Count - 1; i >= 0; i--)
                if (weapons[i].ShooterId == racerId) weapons.RemoveAt(i);
            inventory.ClearRacer(racerId);
        }

        public void Clear()
        {
            for (int i = 0; i < racers.Count; i++)
                if (racers[i].Vehicle != null) racers[i].Vehicle.ClearCombatModifiers();
            racers.Clear();
            weapons.Clear();
            SimulationTime = 0;
            HitCount = 0;
            nextWeaponId = 0;
        }

        private bool StepWeapon(LiveWeapon weapon, float delta)
        {
            weapon.Age += delta;
            if (weapon.Age >= (weapon.Kind == WeaponKind.Bolt ? BoltLifetime : OilLifetime)) return false;
            return weapon.Kind == WeaponKind.Bolt ? StepBolt(weapon, delta) : StepOil(weapon);
        }

        private bool StepBolt(LiveWeapon weapon, float delta)
        {
            int count = Physics.OverlapSphereNonAlloc(weapon.Position, BoltRadius, overlaps, ~0,
                QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (!IsShooter(weapon, overlaps[i])) { Impact(weapon, overlaps[i]); return false; }

            float distance = BoltSpeed * delta;
            count = Physics.SphereCastNonAlloc(weapon.Position, BoltRadius, weapon.Direction, hits, distance,
                ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            int nearest = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || IsShooter(weapon, collider) || hits[i].distance >= nearestDistance) continue;
                nearest = i;
                nearestDistance = hits[i].distance;
            }
            if (nearest >= 0) { Impact(weapon, hits[nearest].collider); return false; }
            weapon.Position += weapon.Direction * distance;
            return true;
        }

        private bool StepOil(LiveWeapon weapon)
        {
            if (weapon.Age < OilArming) return true;
            int count = Physics.OverlapBoxNonAlloc(weapon.Position + Vector3.up * .35f,
                new Vector3(OilRadius, .5f, OilRadius), overlaps, Quaternion.identity, ~0,
                QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            // Locked roster order gives simultaneous entrants a stable resolution order.
            for (int racerIndex = 0; racerIndex < racers.Count; racerIndex++)
            {
                Racer target = racers[racerIndex];
                if (target.Id == weapon.ShooterId && weapon.Age < OilShooterGrace) continue;
                for (int i = 0; i < count; i++)
                {
                    Collider collider = overlaps[i];
                    if (collider == null || collider.attachedRigidbody != target.Body) continue;
                    Vector3 nearest = collider.ClosestPoint(weapon.Position + Vector3.up * .35f);
                    if (Vector3.ProjectOnPlane(nearest - weapon.Position, Vector3.up).sqrMagnitude > OilRadius * OilRadius)
                        continue;
                    if (TryHit(weapon, target)) return false;
                }
            }
            return true;
        }

        private void Impact(LiveWeapon weapon, Collider collider)
        {
            if (collider == null) return;
            Racer target = Find(collider.attachedRigidbody);
            if (target != null) TryHit(weapon, target);
        }

        private bool TryHit(LiveWeapon weapon, Racer target)
        {
            if (target.Progress.IsFinished || target.Vehicle == null || SimulationTime < target.SafeUntil) return false;
            if (weapon.Kind == WeaponKind.Bolt)
            {
                target.ImpactUntil = SimulationTime + ImpactDuration;
                target.Body.AddForce(weapon.Direction * 3f, ForceMode.VelocityChange);
            }
            else target.OilUntil = SimulationTime + OilDuration;
            ApplyModifiers(target);
            HitCount++;
            return true;
        }

        private void ApplyModifiers(Racer racer)
        {
            if (racer.Vehicle == null) return;
            bool oil = SimulationTime < racer.OilUntil;
            bool impact = SimulationTime < racer.ImpactUntil;
            racer.Vehicle.SetCombatModifiers(oil ? .18f : 1f,
                impact ? .25f : oil ? .65f : 1f,
                Mathf.Min(impact ? .5f : 1f, oil ? .45f : 1f));
        }

        private bool IsShooter(LiveWeapon weapon, Collider collider) =>
            collider != null && collider.attachedRigidbody == Find(weapon.ShooterId)?.Body;
        private Racer Find(uint id)
        {
            for (int i = 0; i < racers.Count; i++) if (racers[i].Id == id) return racers[i];
            return null;
        }
        private Racer Find(Rigidbody body)
        {
            if (body == null) return null;
            for (int i = 0; i < racers.Count; i++) if (racers[i].Body == body) return racers[i];
            return null;
        }
        private bool CanSimulate => race != null && race.RaceRunning && Time.timeScale > 0f;
    }
}
