using System;
using System.Collections.Generic;
using Weapons;

namespace ReReVolt.Online.Contracts
{
    /// <summary>
    /// Host-only, engine-independent combat inventory and pickup cycle for one
    /// locked round. The world decides whether a car physically reached a pickup
    /// or can spawn a weapon; clients never call this authority.
    /// </summary>
    public sealed class OnlineCombatInventoryAuthority
    {
        public const int PickupCount = 4;
        public const double FireCooldown = .35;
        public const double PickupRespawn = 5;

        private readonly OnlineLobbyAuthority lobby;
        private readonly uint round;
        private readonly Dictionary<uint, CombatInventory> humanInventory = new Dictionary<uint, CombatInventory>(4);
        private readonly PickupCycle[] pickups = new PickupCycle[PickupCount];

        public OnlineCombatInventoryAuthority(OnlineLobbyAuthority hostLobby)
        {
            lobby = hostLobby ?? throw new ArgumentNullException(nameof(hostLobby));
            if (lobby.Phase != OnlinePhase.Countdown || lobby.Round == 0)
                throw new ArgumentException("A locked host countdown is required.", nameof(hostLobby));
            round = lobby.Round;
            LobbySnapshot roster = lobby.Snapshot();
            if (roster.Racers == null || roster.Racers.Length < 1 || roster.Racers.Length > 4)
                throw new ArgumentException("The locked racer roster is invalid.", nameof(hostLobby));
            foreach (LobbyRacer racer in roster.Racers)
                if (racer != null && !racer.Bot && !humanInventory.TryAdd(racer.Id, new CombatInventory()))
                    throw new ArgumentException("Human racer IDs must be unique.", nameof(hostLobby));
            if (humanInventory.Count == 0)
                throw new ArgumentException("The round needs at least one human racer.", nameof(hostLobby));
            for (int i = 0; i < pickups.Length; i++) pickups[i] = new PickupCycle(i, PickupRespawn);
        }

        public WeaponKind HeldWeapon(uint racerId) => humanInventory.TryGetValue(racerId, out CombatInventory inventory)
            ? inventory.Held : WeaponKind.None;

        public WeaponKind Offering(int pickupIndex) => ValidPickup(pickupIndex)
            ? pickups[pickupIndex].Offering : WeaponKind.None;

        public bool IsPickupAvailable(int pickupIndex, double hostTime) =>
            Active(hostTime) && ValidPickup(pickupIndex) && pickups[pickupIndex].IsAvailable(hostTime);

        public bool TryCollect(uint racerId, int pickupIndex, double hostTime, out WeaponKind collected)
        {
            collected = WeaponKind.None;
            if (!IsPickupAvailable(pickupIndex, hostTime) ||
                !humanInventory.TryGetValue(racerId, out CombatInventory inventory)) return false;
            WeaponKind offered = pickups[pickupIndex].Offering;
            if (!pickups[pickupIndex].TryCollect(inventory, hostTime)) return false;
            collected = offered;
            return true;
        }

        public bool CanFire(uint racerId, double hostTime) =>
            Active(hostTime) && humanInventory.TryGetValue(racerId, out CombatInventory inventory) &&
            inventory.CanFire(hostTime);

        /// <summary>Call only after the host has validated the weapon spawn point and live-weapon capacity.</summary>
        public bool TryFire(uint racerId, double hostTime, out WeaponKind fired)
        {
            fired = WeaponKind.None;
            return CanFire(racerId, hostTime) &&
                humanInventory[racerId].TryFire(hostTime, FireCooldown, out fired);
        }

        /// <summary>Discard round-local inventory and pickup state during host teardown.</summary>
        public void Clear()
        {
            foreach (CombatInventory inventory in humanInventory.Values) inventory.Reset();
            foreach (PickupCycle pickup in pickups) pickup.Reset();
        }

        private bool Active(double time) => lobby.Round == round && lobby.Phase == OnlinePhase.Racing &&
            !double.IsNaN(time) && !double.IsInfinity(time) && time >= 0;
        private static bool ValidPickup(int index) => index >= 0 && index < PickupCount;
    }
}
