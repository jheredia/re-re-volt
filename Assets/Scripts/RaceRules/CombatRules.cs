using System;

namespace Weapons
{
    public enum WeaponKind { None, Bolt, Oil }

    /// <summary>One slot and a fire cooldown, driven only by the combat owner's simulation clock.</summary>
    public sealed class CombatInventory
    {
        private double nextFireTime;
        public WeaponKind Held { get; private set; }

        public bool TryCollect(WeaponKind weapon)
        {
            if (Held != WeaponKind.None || (weapon != WeaponKind.Bolt && weapon != WeaponKind.Oil)) return false;
            Held = weapon;
            return true;
        }

        public bool CanFire(double time) => Held != WeaponKind.None && time >= nextFireTime;

        public bool TryFire(double time, double cooldown, out WeaponKind weapon)
        {
            weapon = WeaponKind.None;
            if (!CanFire(time) || double.IsNaN(cooldown) || cooldown < 0) return false;
            weapon = Held;
            Held = WeaponKind.None;
            nextFireTime = time + cooldown;
            return true;
        }

        public void Reset()
        {
            Held = WeaponKind.None;
            nextFireTime = 0;
        }
    }

    /// <summary>Each box alternates its offering after successful collections; reset repeats the sequence.</summary>
    public sealed class PickupCycle
    {
        private readonly int initialParity;
        private readonly double cooldown;
        private int parity;
        private double availableAt;

        public PickupCycle(int index, double respawnSeconds)
        {
            if (index < 0 || double.IsNaN(respawnSeconds) || double.IsInfinity(respawnSeconds) || respawnSeconds <= 0)
                throw new ArgumentOutOfRangeException();
            initialParity = index & 1;
            cooldown = respawnSeconds;
            Reset();
        }

        public WeaponKind Offering => parity == 0 ? WeaponKind.Bolt : WeaponKind.Oil;
        public bool IsAvailable(double time) => time >= availableAt;

        public bool TryCollect(CombatInventory inventory, double time)
        {
            if (!IsAvailable(time) || inventory == null || !inventory.TryCollect(Offering)) return false;
            availableAt = time + cooldown;
            parity ^= 1;
            return true;
        }

        public void Reset()
        {
            parity = initialParity;
            availableAt = 0;
        }
    }
}
