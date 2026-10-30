using System.Collections;
using AudioPresentation;
using Core;
using NUnit.Framework;
using ReReVolt.Online.Contracts;
using ReReVolt.Online.Unity;
using UnityEngine;
using UnityEngine.TestTools;
using Vehicle;
using Weapons;

namespace ReReVolt.Tests.Online
{
    public sealed class OnlineAudioPresentationTests
    {
        [UnityTest]
        public IEnumerator LocalSnapshotCuesReuseBoundedSessionMixAndRestoreOfflineOwnership()
        {
            Time.timeScale = 1f;
            var root = new GameObject("Online audio test");
            var race = root.AddComponent<RaceManager>();
            var roster = root.AddComponent<VehicleManager>();
            var audio = root.AddComponent<RcAudioPresentation>();
            var online = root.AddComponent<OnlineAudioPresentation>();
            var locked = new LobbySnapshot
            {
                Epoch = 5, Round = 2, TrackId = "rc-club-workshop", Phase = OnlinePhase.Countdown,
                Racers = new[]
                {
                    new LobbyRacer { Id = 11, Peer = "host", Slot = 1 },
                    new LobbyRacer { Id = 12, Peer = "friend", Slot = 0 },
                    new LobbyRacer { Id = 13, Peer = "host", Slot = 0 }
                }
            };
            try
            {
                audio.Configure(race, roster);
                online.Initialize(locked, "host", audio);
                Assert.That(audio.IsOnlineRound, Is.True);
                Assert.That(audio.ActiveEngineVoiceCount, Is.EqualTo(2));
                Assert.That(root.GetComponentsInChildren<AudioSource>(true).Length,
                    Is.EqualTo(RcAudioPresentation.MaximumOneShotVoices + RcAudioPresentation.MaximumEngineVoices));

                var first = Snapshot(locked, 1, DrivingPhase.Countdown);
                first.Countdown = 3;
                online.ApplySnapshot(first);
                Assert.That(audio.LastCue, Is.EqualTo(AudioCueId.Countdown));

                var second = Snapshot(locked, 2, DrivingPhase.Racing);
                second.Racers[0].HeldWeapon = WeaponKind.Bolt;
                second.Racers[0].Velocity = new WorldVector(12, 0, 0);
                online.ApplySnapshot(second);
                Assert.That(audio.LastCue, Is.EqualTo(AudioCueId.Pickup));
                Assert.That(audio.LastCuePlayerIndex, Is.EqualTo(1), "Audio recipients follow local slot order, not roster insertion order.");

                var remoteShot = Snapshot(locked, 3, DrivingPhase.Racing);
                remoteShot.Racers[0].HeldWeapon = WeaponKind.Bolt;
                remoteShot.Weapons = new[] { new DrivingWeaponState { Id = 7, ShooterId = 12, Kind = WeaponKind.Oil } };
                online.ApplySnapshot(remoteShot);
                Assert.That(audio.LastCue, Is.EqualTo(AudioCueId.Pickup), "Remote weapons do not claim a local cue recipient.");

                var localShot = Snapshot(locked, 4, DrivingPhase.Racing);
                localShot.Weapons = new[] { new DrivingWeaponState { Id = 8, ShooterId = 11, Kind = WeaponKind.Bolt } };
                online.ApplySnapshot(localShot);
                Assert.That(audio.LastCue, Is.EqualTo(AudioCueId.BoltLaunch));
                Assert.That(audio.LastCuePlayerIndex, Is.EqualTo(1));

                var hit = Snapshot(locked, 5, DrivingPhase.Racing);
                hit.CombatEvents = new[] { new DrivingCombatEvent
                {
                    Id = 1, WeaponId = 8, ShooterId = 12, TargetId = 11,
                    Weapon = WeaponKind.Bolt, Kind = DrivingCombatEventKind.Hit
                } };
                online.ApplySnapshot(hit);
                Assert.That(audio.LastCue, Is.EqualTo(AudioCueId.BoltHit));
                int accepted = audio.AcceptedCueCount;
                hit.Sequence = 6;
                online.ApplySnapshot(hit);
                Assert.That(audio.AcceptedCueCount, Is.EqualTo(accepted), "Repair history must not replay impacts.");

                var results = Snapshot(locked, 7, DrivingPhase.Results);
                online.ApplySnapshot(results);
                Assert.That(audio.LastCue, Is.EqualTo(AudioCueId.Finish));
                int afterFinish = audio.AcceptedCueCount;
                results.Epoch++;
                results.Sequence++;
                online.ApplySnapshot(results);
                Assert.That(audio.AcceptedCueCount, Is.EqualTo(afterFinish),
                    "A different session must not play audio in the current round.");

                online.Shutdown();
                Assert.That(audio.IsOnlineRound, Is.False);
                Assert.That(audio.ActiveEngineVoiceCount, Is.Zero);
                Assert.That(root.GetComponentsInChildren<AudioSource>(true).Length,
                    Is.EqualTo(RcAudioPresentation.MaximumOneShotVoices + RcAudioPresentation.MaximumEngineVoices));
            }
            finally
            {
                online.Shutdown();
                Object.Destroy(root);
            }
            yield return null;
        }

        private static DrivingWorldSnapshot Snapshot(LobbySnapshot roster, ulong sequence, DrivingPhase phase) =>
            new DrivingWorldSnapshot
            {
                Epoch = roster.Epoch, Round = roster.Round, TrackId = roster.TrackId,
                Sequence = sequence, Phase = phase, Racers = new[]
                {
                    new DrivingRacerState { RacerId = 11 },
                    new DrivingRacerState { RacerId = 12 },
                    new DrivingRacerState { RacerId = 13 }
                },
                Weapons = new DrivingWeaponState[0], CombatEvents = new DrivingCombatEvent[0]
            };
    }
}
