using NUnit.Framework;
using ReReVolt.Online.Contracts;
using ReReVolt.Online.Unity;

namespace ReReVolt.Tests.Online
{
    /// <summary>Focused wire-contract coverage; NGO peer delivery is a later two-process gate.</summary>
    public sealed class OnlineRaceMessageRoutingTests
    {
        [Test]
        public void InputCodecRoundTripsBoundedPeerFrameWithoutAliasing()
        {
            var source = new PeerInputFrame
            {
                Epoch = 44, Round = 3, Sequence = 11,
                Racers = new[] { new RacerInputCommand { RacerId = 7, Sequence = 9, Throttle = 1000, Steer = -250, Brake = 12, Edges = InputEdges.Recover } }
            };

            Assert.That(OnlineRaceWireCodec.TryEncodeInput(source, out var bytes), Is.True);
            source.Racers[0].Throttle = -1000;
            Assert.That(OnlineRaceWireCodec.TryDecodeInput(bytes, out var decoded), Is.True);
            Assert.That(decoded.Epoch, Is.EqualTo(44));
            Assert.That(decoded.Round, Is.EqualTo(3));
            Assert.That(decoded.Racers, Has.Length.EqualTo(1));
            Assert.That(decoded.Racers[0].Throttle, Is.EqualTo(1000));
            Assert.That(decoded.Racers[0].Edges, Is.EqualTo(InputEdges.Recover));
        }

        [Test]
        public void SnapshotCodecFeedsOnlyValidatedRepairState()
        {
            var authority = new OnlineLobbyAuthority("host", 44, new[] { "track" }, "track");
            ReadyAndStart(authority);
            var lobby = authority.Snapshot();
            var source = new DrivingWorldSnapshot
            {
                Epoch = lobby.Epoch, Round = lobby.Round, Sequence = 1, Tick = 4, TrackId = lobby.TrackId,
                Phase = DrivingPhase.Racing, Countdown = 0, Elapsed = 2,
                Racers = new[]
                {
                    new DrivingRacerState
                    {
                        RacerId = lobby.Racers[0].Id, Position = new WorldVector(1, 2, 3), Velocity = new WorldVector(), AngularVelocity = new WorldVector(),
                        Rotation = new WorldRotation { W = 1 }, CompletedLaps = 0, NextCheckpoint = 0, PositionInRace = 1,
                        FinishPosition = 0, FinishTime = -1, AcknowledgedInput = 2, RecoveryRevision = 0
                    }
                },
                Pickups = new[]
                {
                    new DrivingPickupState { Index = 0, Offering = Weapons.WeaponKind.Bolt, Available = true },
                    new DrivingPickupState { Index = 1, Offering = Weapons.WeaponKind.Oil, Available = true },
                    new DrivingPickupState { Index = 2, Offering = Weapons.WeaponKind.Bolt, Available = true },
                    new DrivingPickupState { Index = 3, Offering = Weapons.WeaponKind.Oil, Available = true }
                },
                Weapons = new[] { new DrivingWeaponState { Id = 1, ShooterId = lobby.Racers[0].Id,
                    Kind = Weapons.WeaponKind.Bolt, Position = new WorldVector(1, 2, 3),
                    Direction = new WorldVector(0, 0, 1), Age = .1f } },
                CombatEvents = new[] { new DrivingCombatEvent { Id = 1, WeaponId = 1,
                    ShooterId = lobby.Racers[0].Id, TargetId = 0, Weapon = Weapons.WeaponKind.Bolt,
                    Kind = DrivingCombatEventKind.WorldImpact, Position = new WorldVector(2, 2, 3) } }
            };

            Assert.That(OnlineRaceWireCodec.TryEncodeSnapshot(source, out var bytes), Is.True);
            source.Racers[0].Position.X = 99;
            Assert.That(OnlineRaceWireCodec.TryDecodeSnapshot(bytes, out var decoded), Is.True);
            var receiver = new OnlineDrivingSnapshotReceiver(lobby, 3, 8);
            Assert.That(receiver.Accept(decoded), Is.EqualTo(SnapshotError.None));
            Assert.That(receiver.Latest.Racers[0].Position.X, Is.EqualTo(1));
            Assert.That(receiver.Latest.Weapons[0].Id, Is.EqualTo(1));
            Assert.That(receiver.Latest.CombatEvents[0].Kind, Is.EqualTo(DrivingCombatEventKind.WorldImpact));
        }

        [Test]
        public void CodecRejectsWrongVersionAndOversizedPayload()
        {
            Assert.That(OnlineRaceWireCodec.TryDecodeInput(System.Text.Encoding.UTF8.GetBytes("{\"Version\":99}"), out _), Is.False);
            // The envelope cannot be expanded with an arbitrary untrusted string field; snapshots can.
            var snapshot = new DrivingWorldSnapshot { TrackId = new string('x', 9000), Racers = new DrivingRacerState[1] };
            Assert.That(OnlineRaceWireCodec.TryEncodeSnapshot(snapshot, out _), Is.False);
        }

        [Test]
        public void MaximumBoundedCombatRepairSetFitsSnapshotWireBudget()
        {
            var snapshot = new DrivingWorldSnapshot
            {
                TrackId = "rc-club-workshop", Racers = new DrivingRacerState[4],
                Pickups = new DrivingPickupState[4], Weapons = new DrivingWeaponState[16],
                CombatEvents = new DrivingCombatEvent[16]
            };
            for (int i = 0; i < snapshot.Weapons.Length; i++)
            {
                snapshot.Weapons[i] = new DrivingWeaponState { Id = (uint)(i + 1),
                    ShooterId = 1234567890, Kind = Weapons.WeaponKind.Bolt,
                    Position = new WorldVector(123.456f, 2.345f, -99.123f),
                    Direction = new WorldVector(.707f, 0, .707f), Age = 2.345f };
                snapshot.CombatEvents[i] = new DrivingCombatEvent { Id = (uint)(i + 1),
                    WeaponId = (uint)(i + 1), ShooterId = 1234567890,
                    TargetId = 987654321, Weapon = Weapons.WeaponKind.Bolt,
                    Kind = DrivingCombatEventKind.Hit,
                    Position = new WorldVector(123.456f, 2.345f, -99.123f) };
            }
            Assert.That(OnlineRaceWireCodec.TryEncodeSnapshot(snapshot, out byte[] bytes), Is.True);
            Assert.That(bytes.Length, Is.LessThanOrEqualTo(8192));
        }

        private static void ReadyAndStart(OnlineLobbyAuthority authority)
        {
            var setReady = new LobbyCommand { Action = LobbyAction.SetReady, RacerId = authority.Snapshot().Racers[0].Id, Ready = true,
                Epoch = authority.Epoch, Revision = authority.Revision };
            Assert.That(authority.Apply("host", setReady, true), Is.EqualTo(OnlineError.None));
            var start = new LobbyCommand { Action = LobbyAction.Start, Epoch = authority.Epoch, Revision = authority.Revision };
            Assert.That(authority.Apply("host", start, true), Is.EqualTo(OnlineError.None));
            authority.BeginRace();
        }
    }
}
