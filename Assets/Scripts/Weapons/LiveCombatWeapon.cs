using UnityEngine;
using Vehicle;

namespace Weapons
{
    /// <summary>Bounded physics queries and visual transform; lifetime is stepped by WeaponManager only.</summary>
    public sealed class LiveCombatWeapon : MonoBehaviour
    {
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Collider[] overlaps = new Collider[32];
        private WeaponManager owner;
        private float speed, radius, lifetime, oilRadius, armingDelay, shooterGrace;

        public WeaponKind Kind { get; private set; }
        public LocalPlayer Shooter { get; private set; }
        public Vector3 Direction { get; private set; }
        public float Age { get; private set; }
        public bool IsArmed => Kind == WeaponKind.Bolt || Age >= armingDelay;

        internal void Initialize(WeaponManager manager, LocalPlayer shooter, WeaponKind kind, Vector3 direction,
            float projectileSpeed, float projectileRadius, float seconds, float hazardRadius, float arming, float grace)
        {
            owner = manager;
            Shooter = shooter;
            Kind = kind;
            Direction = direction;
            speed = projectileSpeed;
            radius = projectileRadius;
            lifetime = seconds;
            oilRadius = hazardRadius;
            armingDelay = arming;
            shooterGrace = grace;
        }

        internal bool Step(float delta)
        {
            Age += delta;
            if (Age >= lifetime) return false;
            if (Kind == WeaponKind.Oil) return StepOil();

            // Overlap catches point-blank spawns; swept spheres prevent tunnelling at high relative speeds.
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (!IsShooter(overlaps[i])) return Impact(overlaps[i], transform.position);

            float distance = speed * delta;
            count = Physics.SphereCastNonAlloc(transform.position, radius, Direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            int nearest = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (IsShooter(hits[i].collider) || hits[i].distance >= nearestDistance) continue;
                nearest = i;
                nearestDistance = hits[i].distance;
            }
            if (nearest >= 0) return Impact(hits[nearest].collider, hits[nearest].point);
            transform.position += Direction * distance;
            return true;
        }

        private bool IsShooter(Collider collider) => Shooter.Vehicle != null && collider.transform.IsChildOf(Shooter.Vehicle.transform);

        private bool Impact(Collider collider, Vector3 point)
        {
            LocalPlayer target = owner.ResolvePlayer(collider);
            bool accepted = target != null && owner.TryHit(this, target);
            owner.NotifyWeaponResolved(this, target, point, collider, accepted);
            owner.ShowBurst(point, Kind);
            return false;
        }

        private bool StepOil()
        {
            if (!IsArmed) return true;
            // A shallow volume prevents hitting cars that jump over the slick.
            int count = Physics.OverlapBoxNonAlloc(transform.position + Vector3.up * .35f,
                new Vector3(oilRadius, .5f, oilRadius), overlaps, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            // Stable slot ordering resolves simultaneous entrants independently of PhysX query order.
            for (int slot = 0; slot < 4; slot++)
                for (int i = 0; i < count; i++)
                {
                    LocalPlayer target = owner.ResolvePlayer(overlaps[i]);
                    if (target == null || target.PlayerIndex != slot || (target == Shooter && Age < shooterGrace)) continue;
                    Vector3 nearest = overlaps[i].ClosestPoint(transform.position + Vector3.up * .35f);
                    Vector3 horizontal = Vector3.ProjectOnPlane(nearest - transform.position, Vector3.up);
                    if (horizontal.sqrMagnitude > oilRadius * oilRadius) continue;
                    if (owner.TryHit(this, target))
                    {
                        owner.NotifyWeaponResolved(this, target, nearest, overlaps[i], true);
                        return false;
                    }
                }
            return true;
        }
    }
}
