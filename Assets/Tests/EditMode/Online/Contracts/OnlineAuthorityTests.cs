using System;
using NUnit.Framework;
using ReReVolt.Online.Contracts;

namespace ReReVolt.Tests.Online
{
    public sealed class OnlineAuthorityTests
    {
        private OnlineLobbyAuthority lobby;
        [SetUp] public void SetUp() => lobby = new OnlineLobbyAuthority("host", 12, new[] { "a", "b", "c", "d" }, "a");
        private OnlineError Apply(string actor, LobbyCommand command, bool simulation = true)
        {
            command.Epoch = lobby.Epoch;
            command.Revision = lobby.Revision;
            return lobby.Apply(actor, command, simulation);
        }
        private uint Id(string peer, int slot = 0)
        {
            foreach (var racer in lobby.Snapshot().Racers)
                if (racer.Peer == peer && racer.Slot == slot) return racer.Id;
            throw new InvalidOperationException("Missing test racer");
        }
        private void ReadyAll()
        {
            foreach (var racer in lobby.Snapshot().Racers)
                if (!racer.Bot) Assert.That(Apply(racer.Peer, new LobbyCommand { Action = LobbyAction.SetReady, RacerId = racer.Id, Ready = true }), Is.EqualTo(OnlineError.None));
        }
        private void Start()
        {
            ReadyAll();
            Assert.That(Apply("host", new LobbyCommand { Action = LobbyAction.Start }), Is.EqualTo(OnlineError.None));
            Assert.That(lobby.BeginRace(), Is.True);
        }

        [Test] public void AdmissionUsesFourCombinedSlotsWithoutEvictingBots()
        {
            Apply("host", new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 3 });
            ulong revision = lobby.Revision;
            Assert.That(lobby.AdmitPeer("friend"), Is.EqualTo(OnlineError.Capacity));
            Assert.That(lobby.Revision, Is.EqualTo(revision));
            Assert.That(lobby.Snapshot().Peers, Is.EqualTo(new[] { "host" }));
            Apply("host", new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 2 });
            Assert.That(lobby.AdmitPeer("friend"), Is.EqualTo(OnlineError.None));
            Assert.That(lobby.Snapshot().Racers.Length, Is.EqualTo(4));
        }

        [Test] public void PeerOwnsOnlyItsLocalSlotsAndCannotSelectHostOptions()
        {
            lobby.AdmitPeer("friend");
            Assert.That(Apply("friend", new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 1 }), Is.EqualTo(OnlineError.NotHost));
            Assert.That(Apply("friend", new LobbyCommand { Action = LobbyAction.SelectTrack, TrackId = "b" }), Is.EqualTo(OnlineError.NotHost));
            Assert.That(Apply("friend", new LobbyCommand { Action = LobbyAction.SetReady, RacerId = Id("host"), Ready = true }), Is.EqualTo(OnlineError.NotOwner));
            Assert.That(Apply("friend", new LobbyCommand { Action = LobbyAction.AddHuman, Slot = 1 }), Is.EqualTo(OnlineError.None));
            Assert.That(lobby.OwnsHuman("host", Id("friend", 1)), Is.False);
            Assert.That(lobby.OwnsHuman("friend", Id("friend", 1)), Is.True);
        }

        [Test] public void RejectsWrongEpochStaleUnknownAndMalformedCommandsWithoutMutation()
        {
            ulong revision = lobby.Revision;
            Assert.That(lobby.Apply("host", new LobbyCommand { Epoch = 13 }, true), Is.EqualTo(OnlineError.WrongEpoch));
            Assert.That(lobby.Apply("host", new LobbyCommand { Epoch = 12, Revision = 999 }, true), Is.EqualTo(OnlineError.StaleRevision));
            Assert.That(Apply("intruder", new LobbyCommand()), Is.EqualTo(OnlineError.UnknownPeer));
            Assert.That(Apply("host", new LobbyCommand { Action = (LobbyAction)99 }), Is.EqualTo(OnlineError.InvalidCommand));
            Assert.That(Apply("host", new LobbyCommand { Action = LobbyAction.SelectTrack, TrackId = "missing" }), Is.EqualTo(OnlineError.InvalidCommand));
            Assert.That(lobby.Revision, Is.EqualTo(revision));
        }

        [Test] public void StartRequiresAllHumansReadyAndIntegratedSimulation()
        {
            lobby.AdmitPeer("friend");
            Assert.That(Apply("host", new LobbyCommand { Action = LobbyAction.Start }), Is.EqualTo(OnlineError.NotReady));
            ReadyAll();
            Assert.That(Apply("friend", new LobbyCommand { Action = LobbyAction.Start }), Is.EqualTo(OnlineError.NotHost));
            Assert.That(Apply("host", new LobbyCommand { Action = LobbyAction.Start }, false), Is.EqualTo(OnlineError.SimulationUnavailable));
            Assert.That(lobby.Phase, Is.EqualTo(OnlinePhase.Lobby));
            Assert.That(Apply("host", new LobbyCommand { Action = LobbyAction.Start }), Is.EqualTo(OnlineError.None));
            Assert.That(lobby.Phase, Is.EqualTo(OnlinePhase.Countdown));
            Assert.That(lobby.AdmitPeer("late"), Is.EqualTo(OnlineError.Locked));
            Assert.That(Apply("host", new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 1 }), Is.EqualTo(OnlineError.Locked));
        }

        [Test] public void TrackRosterAndBotEditsInvalidateReadiness()
        {
            ReadyAll();
            Apply("host", new LobbyCommand { Action = LobbyAction.SelectTrack, TrackId = "b" });
            Assert.That(lobby.Snapshot().Racers[0].Ready, Is.False);
            ReadyAll();
            Apply("host", new LobbyCommand { Action = LobbyAction.AddHuman, Slot = 1 });
            Assert.That(lobby.Snapshot().Racers[0].Ready, Is.False);
            ReadyAll();
            Apply("host", new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 1 });
            Assert.That(lobby.Snapshot().Racers[0].Ready, Is.False);
            Assert.That(lobby.Snapshot().Racers[2].Ready, Is.True);
        }

        [Test] public void RematchRetainsIdsAndTrackButRequiresRenewedReadiness()
        {
            lobby.AdmitPeer("friend");
            Apply("host", new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 2 });
            Apply("host", new LobbyCommand { Action = LobbyAction.SelectTrack, TrackId = "d" });
            var original = lobby.Snapshot();
            Start();
            Assert.That(lobby.CompleteRace(), Is.True);
            Assert.That(lobby.CompleteRace(), Is.False);
            Assert.That(Apply("host", new LobbyCommand { Action = LobbyAction.Rematch }), Is.EqualTo(OnlineError.None));
            Assert.That(Apply("host", new LobbyCommand { Action = LobbyAction.Start }), Is.EqualTo(OnlineError.NotReady));
            var snapshot = lobby.Snapshot();
            Assert.That(snapshot.TrackId, Is.EqualTo(original.TrackId));
            for (int i = 0; i < original.Racers.Length; i++) Assert.That(snapshot.Racers[i].Id, Is.EqualTo(original.Racers[i].Id));
            Start();
            Assert.That(lobby.Round, Is.EqualTo(2));
        }

        [Test] public void DisconnectDuringRaceAbortsToLobbyAndRemovesOnlyLostPeer()
        {
            lobby.AdmitPeer("friend");
            uint departed = Id("friend");
            Start();
            Assert.That(lobby.RemovePeer("friend"), Is.True);
            Assert.That(lobby.Phase, Is.EqualTo(OnlinePhase.Lobby));
            Assert.That(lobby.Snapshot().Racers.Length, Is.EqualTo(1));
            Assert.That(lobby.OwnsHuman("friend", departed), Is.False);
            Assert.That(lobby.Snapshot().Racers[0].Ready, Is.False);
            Assert.That(lobby.AdmitPeer("replacement"), Is.EqualTo(OnlineError.None));
            Assert.That(Id("replacement"), Is.GreaterThan(departed));
        }

        [Test] public void LastHumanCannotBeRemovedAndSnapshotsCannotMutateAuthority()
        {
            Assert.That(Apply("host", new LobbyCommand { Action = LobbyAction.RemoveHuman, RacerId = Id("host") }), Is.EqualTo(OnlineError.InvalidCommand));
            var copy = lobby.Snapshot();
            copy.Racers[0].Peer = "intruder";
            copy.Racers[0].Ready = true;
            copy.Peers[0] = "intruder";
            Assert.That(lobby.OwnsHuman("host", Id("host")), Is.True);
            Assert.That(lobby.Snapshot().Racers[0].Ready, Is.False);
        }

        private PeerInputFrame Frame(uint id, ulong sequence = 1)
        {
            return new PeerInputFrame { Epoch = 12, Round = lobby.Round, Sequence = sequence,
                Racers = new[] { new RacerInputCommand { RacerId = id, Sequence = sequence, Throttle = 800, Edges = InputEdges.Fire | InputEdges.Recover } } };
        }

        [Test] public void InputIsOwnerBoundDeduplicatedAndExpiresAt250Milliseconds()
        {
            lobby.AdmitPeer("friend"); Start();
            var input = new OnlineInputAuthority(lobby);
            uint id = Id("friend");
            var frame = Frame(id);
            Assert.That(input.Accept("host", frame, 1), Is.EqualTo(InputError.NotOwner));
            Assert.That(input.Accept("friend", frame, 1), Is.EqualTo(InputError.None));
            Assert.That(input.Accept("friend", frame, 1.01), Is.EqualTo(InputError.StaleSequence));
            Assert.That(input.Consume(id, 1.01).Edges, Is.EqualTo(InputEdges.Fire | InputEdges.Recover));
            Assert.That(input.Consume(id, 1.02).Edges, Is.EqualTo(InputEdges.None));
            Assert.That(input.Consume(id, 1.249).Throttle, Is.EqualTo(800));
            Assert.That(input.Consume(id, 1.250).Throttle, Is.Zero);
            Assert.That(input.Consume(id, 1.250).Sequence, Is.EqualTo(1));
        }

        [Test] public void InputFrameIsAtomicWhenOneEntryIsInvalid()
        {
            Apply("host", new LobbyCommand { Action = LobbyAction.AddHuman, Slot = 1 }); Start();
            var input = new OnlineInputAuthority(lobby);
            uint id = Id("host");
            var frame = Frame(id);
            frame.Racers = new[] { frame.Racers[0], new RacerInputCommand { RacerId = Id("host", 1), Sequence = 1, Steer = 1001 } };
            Assert.That(input.Accept("host", frame, 1), Is.EqualTo(InputError.InvalidFrame));
            Assert.That(input.Consume(id, 1).Throttle, Is.Zero);
            frame.Racers[1].Steer = 1000;
            Assert.That(input.Accept("host", frame, 1), Is.EqualTo(InputError.None));
            Assert.That(input.Consume(id, 1).Throttle, Is.EqualTo(800));
        }

        [Test] public void RejectsDuplicateRacerEntriesAndBotInput()
        {
            Apply("host", new LobbyCommand { Action = LobbyAction.SetBots, BotCount = 1 }); Start();
            var input = new OnlineInputAuthority(lobby);
            var frame = Frame(Id("host"));
            frame.Racers = new[] { frame.Racers[0], frame.Racers[0] };
            Assert.That(input.Accept("host", frame, 1), Is.EqualTo(InputError.InvalidFrame));
            frame = Frame(lobby.Snapshot().Racers[1].Id);
            Assert.That(input.Accept("host", frame, 1), Is.EqualTo(InputError.NotOwner));
        }

        [Test] public void RejectsOldEpochRoundIllegalPhaseAndNonfiniteClock()
        {
            var input = new OnlineInputAuthority(lobby);
            var frame = Frame(Id("host"));
            Assert.That(input.Accept("host", frame, 1), Is.EqualTo(InputError.IllegalPhase));
            Start(); input = new OnlineInputAuthority(lobby); frame = Frame(Id("host"));
            frame.Epoch++;
            Assert.That(input.Accept("host", frame, 1), Is.EqualTo(InputError.WrongEpoch));
            frame.Epoch--; frame.Round++;
            Assert.That(input.Accept("host", frame, 1), Is.EqualTo(InputError.WrongRound));
            frame.Round--;
            Assert.That(input.Accept("host", frame, double.NaN), Is.EqualTo(InputError.InvalidFrame));
            Assert.That(input.Accept("host", frame, double.PositiveInfinity), Is.EqualTo(InputError.InvalidFrame));
            Assert.That(input.Accept("host", frame, 1), Is.EqualTo(InputError.None));
            Assert.That(input.Accept("host", Frame(Id("host"), 2), .5), Is.EqualTo(InputError.InvalidFrame));
        }

        [Test] public void CapsInputRateAndRetiresGateAcrossRematch()
        {
            Start(); var input = new OnlineInputAuthority(lobby); uint id = Id("host");
            for (ulong i = 1; i <= 120; i++) Assert.That(input.Accept("host", Frame(id, i), 1), Is.EqualTo(InputError.None));
            Assert.That(input.Accept("host", Frame(id, 121), 1.5), Is.EqualTo(InputError.RateLimited));
            Assert.That(input.Accept("host", Frame(id, 121), 2), Is.EqualTo(InputError.None));
            lobby.CompleteRace(); Apply("host", new LobbyCommand { Action = LobbyAction.Rematch }); Start();
            Assert.That(input.Accept("host", Frame(id, 122), 3), Is.EqualTo(InputError.WrongRound));
            Assert.That(input.Consume(id, 2.01).Throttle, Is.Zero);
            Assert.That(new OnlineInputAuthority(lobby).Accept("host", Frame(id), 3), Is.EqualTo(InputError.None));
        }
    }
}
