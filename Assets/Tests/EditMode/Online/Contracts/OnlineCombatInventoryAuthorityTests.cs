using NUnit.Framework;
using ReReVolt.Online.Contracts;
using Weapons;

namespace ReReVolt.Tests.Online
{
    public sealed class OnlineCombatInventoryAuthorityTests
    {
        [Test]
        public void LockedHumanOnlyPickupAndFirePreserveSlotCycleAndHostPhase()
        {
            OnlineLobbyAuthority lobby = LockedLobby();
            LobbySnapshot roster = lobby.Snapshot();
            uint host = roster.Racers[0].Id;
            uint bot = 0;
            foreach (LobbyRacer racer in roster.Racers)
                if (racer.Bot) bot = racer.Id;
            Assert.That(bot, Is.Not.Zero);
            var combat = new OnlineCombatInventoryAuthority(lobby);

            Assert.That(combat.TryCollect(host, 0, 0, out _), Is.False,
                "Countdown must not grant weapons before the race starts.");
            Assert.That(lobby.BeginRace(), Is.True);
            Assert.That(combat.TryCollect(bot, 0, 0, out _), Is.False,
                "Driving-only bots do not own weapon inventory in this slice.");
            Assert.That(combat.TryCollect(host, -1, 0, out _), Is.False);
            Assert.That(combat.TryCollect(host, 0, 0, out WeaponKind collected), Is.True);
            Assert.That(collected, Is.EqualTo(WeaponKind.Bolt));
            Assert.That(combat.HeldWeapon(host), Is.EqualTo(WeaponKind.Bolt));
            Assert.That(combat.IsPickupAvailable(0, 0), Is.False);

            Assert.That(combat.TryCollect(host, 1, 0, out _), Is.False,
                "A full slot must not consume or rotate another pickup.");
            Assert.That(combat.Offering(1), Is.EqualTo(WeaponKind.Oil));
            Assert.That(combat.IsPickupAvailable(1, 0), Is.True);
            Assert.That(combat.TryFire(host, 0, out WeaponKind fired), Is.True);
            Assert.That(fired, Is.EqualTo(WeaponKind.Bolt));
            Assert.That(combat.HeldWeapon(host), Is.EqualTo(WeaponKind.None));
            Assert.That(combat.TryCollect(host, 1, 0, out collected), Is.True);
            Assert.That(collected, Is.EqualTo(WeaponKind.Oil));
            Assert.That(combat.TryFire(host, .34, out _), Is.False);
            Assert.That(combat.HeldWeapon(host), Is.EqualTo(WeaponKind.Oil));
            Assert.That(combat.TryFire(host, .35, out fired), Is.True);
            Assert.That(fired, Is.EqualTo(WeaponKind.Oil));
            Assert.That(combat.IsPickupAvailable(0, 4.99), Is.False);
            Assert.That(combat.TryCollect(host, 0, 5, out collected), Is.True);
            Assert.That(collected, Is.EqualTo(WeaponKind.Oil),
                "A consumed pickup alternates its offering on respawn.");

            Assert.That(lobby.CompleteRace(), Is.True);
            Assert.That(combat.TryFire(host, 6, out _), Is.False);
            Assert.That(combat.TryCollect(host, 2, 6, out _), Is.False);
            combat.Clear();
            Assert.That(combat.HeldWeapon(host), Is.EqualTo(WeaponKind.None));
            Assert.That(combat.Offering(0), Is.EqualTo(WeaponKind.Bolt));
        }

        [Test]
        public void InvalidRacerTimeAndRoundCannotChangeInventory()
        {
            OnlineLobbyAuthority lobby = LockedLobby();
            var combat = new OnlineCombatInventoryAuthority(lobby);
            uint racer = lobby.Snapshot().Racers[0].Id;
            Assert.That(lobby.BeginRace(), Is.True);
            Assert.That(combat.TryCollect(uint.MaxValue, 0, 0, out _), Is.False);
            Assert.That(combat.TryCollect(racer, 0, double.NaN, out _), Is.False);
            Assert.That(combat.TryCollect(racer, 0, double.PositiveInfinity, out _), Is.False);
            Assert.That(combat.HeldWeapon(racer), Is.EqualTo(WeaponKind.None));
            Assert.That(combat.IsPickupAvailable(0, 0), Is.True);
            Assert.That(lobby.AbortRound(), Is.True);
            Assert.That(combat.TryCollect(racer, 0, 0, out _), Is.False);
        }

        private static OnlineLobbyAuthority LockedLobby()
        {
            var lobby = new OnlineLobbyAuthority("host", 91, new[] { "track" }, "track");
            Apply(lobby, new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 1 });
            foreach (LobbyRacer racer in lobby.Snapshot().Racers)
                if (!racer.Bot)
                    Apply(lobby, new LobbyCommand { Action = LobbyAction.SetReady,
                        RacerId = racer.Id, Ready = true });
            Apply(lobby, new LobbyCommand { Action = LobbyAction.Start });
            return lobby;
        }

        private static void Apply(OnlineLobbyAuthority lobby, LobbyCommand command)
        {
            command.Epoch = lobby.Epoch;
            command.Revision = lobby.Revision;
            Assert.That(lobby.Apply("host", command, true), Is.EqualTo(OnlineError.None));
        }
    }
}
