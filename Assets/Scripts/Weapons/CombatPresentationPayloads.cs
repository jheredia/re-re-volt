using UnityEngine;
using Vehicle;

namespace Weapons
{
    /// <summary>Exact presentation context emitted after a live weapon is accepted by combat authority.</summary>
    public readonly struct WeaponSpawnedPresentationPayload
    {
        public WeaponSpawnedPresentationPayload(LiveCombatWeapon weapon, LocalPlayer source, WeaponKind kind,
            Vector3 point, Vector3 direction)
        {
            Weapon = weapon;
            Source = source;
            Kind = kind;
            Point = point;
            Direction = direction;
        }

        public LiveCombatWeapon Weapon { get; }
        public LocalPlayer Source { get; }
        public int SourcePlayerIndex => Source != null ? Source.PlayerIndex : -1;
        public WeaponKind Kind { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
    }

    /// <summary>Spatial outcome observed after authoritative hit acceptance has already been decided.</summary>
    public readonly struct WeaponResolvedPresentationPayload
    {
        public WeaponResolvedPresentationPayload(LiveCombatWeapon weapon, LocalPlayer source, LocalPlayer target,
            WeaponKind kind, Vector3 point, Vector3 direction, Collider collider, bool acceptedHit)
        {
            Weapon = weapon;
            Source = source;
            Target = target;
            Kind = kind;
            Point = point;
            Direction = direction;
            Collider = collider;
            AcceptedHit = acceptedHit;
        }

        public LiveCombatWeapon Weapon { get; }
        public LocalPlayer Source { get; }
        public LocalPlayer Target { get; }
        public int SourcePlayerIndex => Source != null ? Source.PlayerIndex : -1;
        public int TargetPlayerIndex => Target != null ? Target.PlayerIndex : -1;
        public WeaponKind Kind { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public Collider Collider { get; }
        public bool AcceptedHit { get; }
    }

    /// <summary>Exact accepted pickup result; pickup availability and inventory remain WeaponManager authority.</summary>
    public readonly struct PickupAcquiredPresentationPayload
    {
        public PickupAcquiredPresentationPayload(int pickupIndex, LocalPlayer collector, WeaponKind kind,
            Vector3 point)
        {
            PickupIndex = pickupIndex;
            Collector = collector;
            Kind = kind;
            Point = point;
        }

        public int PickupIndex { get; }
        public LocalPlayer Collector { get; }
        public int CollectorPlayerIndex => Collector != null ? Collector.PlayerIndex : -1;
        public WeaponKind Kind { get; }
        public Vector3 Point { get; }
    }
}
