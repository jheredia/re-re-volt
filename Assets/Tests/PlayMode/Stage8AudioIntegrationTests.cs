using System.Collections;
using System.Collections.Generic;
using AudioPresentation;
using Core;
using NUnit.Framework;
using Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.TestTools;
using Vehicle;
using Weapons;

namespace Tests.PlayMode
{
    public sealed class Stage8AudioIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private RaceManager race;
        private VehicleManager roster;
        private Stage3RacePresentation hud;
        private WeaponManager combat;
        private RcAudioPresentation audio;
        private PrototypeCircuitBuilder.Result circuit;
        private Keyboard keyboard;
        private readonly List<AudioListener> externalListeners = new();

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
                if (listener.enabled)
                {
                    externalListeners.Add(listener);
                    listener.enabled = false;
                }
            root = new GameObject("Stage8AudioTest");
            race = root.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(1, PrototypeCircuitBuilder.OrderedCheckpointCount), Is.True);
            circuit = PrototypeCircuitBuilder.Build(race, root.transform);

            GameObject lobbyCameraObject = new GameObject("AudioTestLobbyCamera");
            lobbyCameraObject.transform.SetParent(root.transform, false);
            Camera lobbyCamera = lobbyCameraObject.AddComponent<Camera>();
            lobbyCameraObject.AddComponent<AudioListener>();

            roster = root.AddComponent<VehicleManager>();
            Assert.That(roster.Configure(race, circuit.Grid, null, lobbyCamera), Is.True);
            roster.PlayerJoined += player => player.Recovery.ConfigureGroundMask(circuit.DrivableMask);
            hud = root.AddComponent<Stage3RacePresentation>();
            hud.Configure(race, roster);
            combat = root.AddComponent<WeaponManager>();
            combat.Configure(race, roster, hud, circuit.DrivableMask);
            audio = root.AddComponent<RcAudioPresentation>();
            audio.Configure(race, roster, combat, hud, circuit.DrivableMask);
            keyboard = InputSystem.AddDevice<Keyboard>();
            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(root);
            circuit?.Dispose();
            Assert.That(InputUser.all.Count, Is.Zero, "Audio ownership must not retain player input users.");
            for (int i = 0; i < externalListeners.Count; i++)
                if (externalListeners[i] != null) externalListeners[i].enabled = true;
            externalListeners.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator FourPlayerMixUsesOneListenerAndBoundedOwnedPresentationObjects()
        {
            Assert.That(audio.IsConfigured, Is.True);
            Assert.That(audio.GeneratedClipCount, Is.EqualTo((int)AudioCueId.Count));
            Assert.That(audio.OneShotVoiceCapacity, Is.EqualTo(RcAudioPresentation.MaximumOneShotVoices));
            Assert.That(audio.EngineVoiceCapacity, Is.EqualTo(LocalPlayerRules.MaximumPlayers));
            Assert.That(EnabledListenerCount(), Is.EqualTo(1));

            Assert.That(roster.TryJoinKeyboard(keyboard, out _), Is.True);
            for (int i = 0; i < 3; i++)
                Assert.That(roster.TryJoinGamepad(InputSystem.AddDevice<Gamepad>(), out _), Is.True);
            yield return null;

            Assert.That(roster.Players.Count, Is.EqualTo(4));
            Assert.That(audio.ActiveEngineVoiceCount, Is.EqualTo(4));
            Assert.That(audio.ImpactEmitterCount, Is.EqualTo(4));
            Assert.That(EnabledListenerCount(), Is.EqualTo(1),
                "The central split-screen mix must not add a listener per player.");
            Assert.That(root.GetComponentsInChildren<AudioSource>(true).Length,
                Is.EqualTo(RcAudioPresentation.MaximumOneShotVoices + RcAudioPresentation.MaximumEngineVoices));

            for (int i = 0; i < roster.Players.Count; i++)
            {
                LocalPlayer player = roster.Players[i];
                Assert.That(player.Vehicle.GetComponents<VehicleImpactAudioEmitter>().Length, Is.EqualTo(1));
                Assert.That(player.Vehicle.GetComponent<Rigidbody>().mass, Is.EqualTo(150f));
                Assert.That(player.Vehicle.SelectedHandlingModel, Is.EqualTo(HandlingModelId.A));
            }

            yield return new WaitForSecondsRealtime(.1f);
            for (int i = 0; i < roster.Players.Count; i++)
                Assert.That(audio.TryPlayPlayerCue(AudioCueId.TireMat, roster.Players[i].PlayerIndex), Is.True);
            for (int i = 0; i < roster.Players.Count; i++)
                Assert.That(audio.GetActiveRecipientVoiceCount(roster.Players[i].PlayerIndex), Is.EqualTo(1),
                    $"Player {i + 1} must retain exactly one routed tire cue in the shared 2D mix.");
        }

        [UnityTest]
        public IEnumerator RaceEventsDriveCountdownGoAndFinishCuesWithoutPollingRaceState()
        {
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer player), Is.True);
            int afterJoin = audio.AcceptedCueCount;
            Assert.That(afterJoin, Is.GreaterThanOrEqualTo(1));

            Assert.That(race.StartRace(), Is.True);
            Assert.That(audio.LastCue, Is.EqualTo(AudioCueId.Countdown));
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Countdown));

            float deadline = Time.realtimeSinceStartup + 4.5f;
            while (race.CurrentState != RaceManager.RaceState.Racing && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Racing));
            Assert.That(audio.LastCue, Is.EqualTo(AudioCueId.Go));

            for (int checkpoint = 0; checkpoint < PrototypeCircuitBuilder.OrderedCheckpointCount; checkpoint++)
                Assert.That(race.ReportCheckpoint(player.Vehicle.gameObject, checkpoint), Is.True);
            Assert.That(race.CurrentState, Is.EqualTo(RaceManager.RaceState.Finished));
            Assert.That(audio.LastCue, Is.EqualTo(AudioCueId.Finish));
            Assert.That(player.Racer.IsFinished, Is.True);
            yield return null;
            Assert.That(audio.GetActiveRecipientVoiceCount(-1), Is.EqualTo(1),
                "The unscaled finish cue must survive the results transition pause edge.");
        }

        [UnityTest]
        public IEnumerator VoiceAndImpactBudgetsRateLimitAndCleanUpExplicitly()
        {
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer player), Is.True);
            yield return new WaitForSecondsRealtime(.1f);

            int beforeBurst = audio.AcceptedCueCount;
            for (int i = 0; i < RcAudioPresentation.MaximumOneShotVoices; i++)
                Assert.That(audio.TryPlayCue(AudioCueId.UiConfirm), Is.True);
            Assert.That(audio.AcceptedCueCount - beforeBurst,
                Is.EqualTo(RcAudioPresentation.MaximumOneShotVoices));
            Assert.That(audio.TryPlayCue(AudioCueId.UiConfirm), Is.False, "A seventh simultaneous voice is dropped.");

            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(audio.TryPlayImpactFeedback(player.PlayerIndex, 8f), Is.True);
            Assert.That(audio.TryPlayImpactFeedback(player.PlayerIndex, 8f), Is.False,
                "Repeated impacts inside the per-player cooldown are dropped.");

            GameObject ownedRoot = root.transform.Find("Stage8_RuntimeAudio").gameObject;
            VehicleImpactAudioEmitter sharedEmitter = player.Vehicle.GetComponent<VehicleImpactAudioEmitter>();
            audio.enabled = false;
            Assert.That(audio.TryPlayCue(AudioCueId.UiConfirm), Is.False);
            audio.enabled = true;
            Object.Destroy(audio);
            yield return null;

            Assert.That(ownedRoot == null, Is.True);
            Assert.That(player.Vehicle.GetComponent<VehicleImpactAudioEmitter>(), Is.SameAs(sharedEmitter),
                "Audio disposal must preserve the roster-owned camera/VFX collision adapter.");
            Assert.That(player.PresentationResponse.isActiveAndEnabled, Is.True);
            bool remainingObserverReceivedImpact = false;
            sharedEmitter.ImpactObserved += payload => remainingObserverReceivedImpact = true;
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.SetParent(root.transform, false);
            obstacle.transform.position = player.Vehicle.transform.position + player.Vehicle.transform.forward * 3f;
            obstacle.transform.localScale = new Vector3(2f, 2f, .4f);
            obstacle.transform.rotation = player.Vehicle.transform.rotation;
            Physics.SyncTransforms();
            player.Vehicle.GetComponent<Rigidbody>().linearVelocity = player.Vehicle.transform.forward * 20f;
            float collisionDeadline = Time.realtimeSinceStartup + 1f;
            while (!remainingObserverReceivedImpact && Time.realtimeSinceStartup < collisionDeadline)
                yield return new WaitForFixedUpdate();
            Assert.That(remainingObserverReceivedImpact, Is.True,
                "The retained shared adapter must still notify presentation observers after audio teardown.");
            Assert.That(EnabledListenerCount(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ImportantCuesStealLowerTiersAndKeepRecipientAndAccountingBounds()
        {
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer player), Is.True);
            yield return new WaitForSecondsRealtime(.1f);
            for (int i = 0; i < RcAudioPresentation.MaximumOneShotVoices; i++)
                Assert.That(audio.TryPlayCue(AudioCueId.UiConfirm), Is.True);
            Assert.That(audio.TryPlayCue(AudioCueId.Countdown), Is.True);
            Assert.That(audio.StolenCueCount, Is.EqualTo(1));
            Assert.That(audio.CurrentDuckScale, Is.EqualTo(.35f));
            Assert.That(audio.TryPlayImpactFeedback(player.PlayerIndex, 8f), Is.True);
            Assert.That(audio.LastCue, Is.EqualTo(AudioCueId.HeavyImpact));
            Assert.That(audio.LastCuePlayerIndex, Is.EqualTo(player.PlayerIndex));
            Assert.That(audio.GetActiveRecipientVoiceCount(player.PlayerIndex), Is.EqualTo(1));
            Assert.That(audio.GetActiveRecipientVoiceCount(1), Is.Zero);
            Assert.That(audio.StolenCueCount, Is.EqualTo(2));
            Assert.That(audio.TryPlayCue(AudioCueId.UiConfirm), Is.False);
            Assert.That(audio.CapacityDroppedCueCount, Is.EqualTo(1));
            Assert.That(audio.OneShotHighWaterCount, Is.EqualTo(RcAudioPresentation.MaximumOneShotVoices));
            Assert.That(audio.TryPlayImpactFeedback(player.PlayerIndex, 8f), Is.False);
            Assert.That(audio.CooldownDroppedCueCount, Is.EqualTo(1));
            Assert.That(audio.RequestedCueCount,
                Is.EqualTo(audio.AcceptedCueCount + audio.DroppedCueCount + audio.SuppressedCueCount));

            audio.enabled = false;
            Assert.That(audio.GetActiveRecipientVoiceCount(player.PlayerIndex), Is.Zero);
            Assert.That(audio.CurrentDuckScale, Is.EqualTo(1f));
            Assert.That(audio.TryPlayCue(AudioCueId.Recovery), Is.False);
            audio.enabled = true;
            Assert.That(audio.TryPlayPlayerCue(AudioCueId.OilHit, player.PlayerIndex), Is.True);
            Assert.That(audio.LastCuePlayerIndex, Is.EqualTo(player.PlayerIndex));
            Assert.That(audio.TryPlayPlayerCue(AudioCueId.BoltHit, 3), Is.False);
            Assert.That(EnabledListenerCount(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SuccessfulRecoveryClearsOldRecipientCuesAndRaceResetReleasesAudio()
        {
            Assert.That(roster.TryJoinKeyboard(keyboard, out LocalPlayer player), Is.True);
            Assert.That(race.StartRace(), Is.True);
            float deadline = Time.realtimeSinceStartup + 4.5f;
            while (!race.RaceRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.RaceRunning, Is.True);
            Assert.That(audio.TryPlayPlayerCue(AudioCueId.BoltHit, player.PlayerIndex), Is.True);
            Assert.That(player.Recovery.TryRecover(), Is.True);
            Assert.That(audio.LastCue, Is.EqualTo(AudioCueId.Recovery));
            Assert.That(audio.LastCuePlayerIndex, Is.EqualTo(player.PlayerIndex));
            Assert.That(audio.GetActiveRecipientVoiceCount(player.PlayerIndex), Is.EqualTo(1));
            race.ResetRace();
            Assert.That(audio.GetActiveRecipientVoiceCount(player.PlayerIndex), Is.Zero);
            Assert.That(audio.CurrentDuckScale, Is.EqualTo(1f));
            Assert.That(audio.OneShotVoiceCapacity, Is.EqualTo(6));
            Assert.That(root.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator ProceduralBankCachesOneClipPerCueAndReleasesOwnership()
        {
            ProceduralAudioBank isolatedBank = new ProceduralAudioBank();
            AudioClip engine = isolatedBank.Get(AudioCueId.Engine);
            Assert.That(engine, Is.Not.Null);
            Assert.That(isolatedBank.Get(AudioCueId.Engine), Is.SameAs(engine));
            Assert.That(isolatedBank.ClipCount, Is.EqualTo((int)AudioCueId.Count));
            isolatedBank.Dispose();
            Assert.That(isolatedBank.ClipCount, Is.Zero);
            Assert.That(isolatedBank.Get(AudioCueId.Engine), Is.Null);
            yield return null;
            Assert.That(engine == null, Is.True);
        }

        private static int EnabledListenerCount()
        {
            int count = 0;
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i].enabled && listeners[i].gameObject.activeInHierarchy) count++;
            return count;
        }
    }
}
