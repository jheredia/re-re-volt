using System.Collections;
using AI;
using Core;
using NUnit.Framework;
using Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Vehicle;

namespace Tests.PlayMode
{
    public sealed class LocalSceneTrackSelectionIntegrationTests : InputTestFixture
    {
        private GameObject root;
        private string savedPreferences;
        private bool hadPreferences;

        public override void Setup()
        {
            base.Setup();
            hadPreferences = PlayerPrefs.HasKey(DemoFlowPreferences.StorageKey);
            savedPreferences = hadPreferences ? PlayerPrefs.GetString(DemoFlowPreferences.StorageKey) : null;
            PlayerPrefs.DeleteKey(DemoFlowPreferences.StorageKey);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator LocalSetupLoadsAuthoredSceneAndSwitchesBackAfterRace()
        {
            root = new GameObject("Local scene track selection test");
            Stage3Bootstrap bootstrap = root.AddComponent<Stage3Bootstrap>();
            Assert.That(bootstrap.InitializeNow(), Is.True);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(bootstrap.VehicleManager.TryJoinKeyboard(keyboard, out LocalPlayer human), Is.True);
            Assert.That(bootstrap.AIManager.TrySetSelectedBotCount(1), Is.True);

            for (int index = 0; index < 4; index++) Assert.That(bootstrap.TryCycleTrack(1), Is.True);
            Assert.That(bootstrap.TrackSession.SelectedTrackId, Is.EqualTo(TrackCatalog.BetaTemplateId));
            Assert.That(bootstrap.TrackLoadPending, Is.True);
            Assert.That(bootstrap.Presentation.SelectedTrackAvailable, Is.False);
            Assert.That(bootstrap.Presentation.LobbyText, Does.Contain("START LOCKED"));
            Assert.That(bootstrap.TryCycleTrack(1), Is.False, "Selection is locked while the additive scene loads.");
            yield return WaitForTrack(bootstrap);

            Assert.That(bootstrap.ActiveTrackId, Is.EqualTo(TrackCatalog.BetaTemplateId));
            Assert.That(bootstrap.CircuitRoot.GetComponent<TrackAuthoringRoot>(), Is.Not.Null);
            Assert.That(bootstrap.Grid, Has.Length.EqualTo(4));
            Assert.That(bootstrap.Checkpoints, Has.Length.EqualTo(8));
            Assert.That(bootstrap.RaceManager.TotalLaps, Is.EqualTo(3));
            Assert.That(bootstrap.Presentation.SelectedTrackAvailable, Is.True);
            Assert.That(bootstrap.DemoPreferences.TrackId, Is.EqualTo(TrackCatalog.BetaTemplateId));
            Assert.That(bootstrap.AIManager.SelectedBotCount, Is.EqualTo(1));
            Assert.That(bootstrap.WeaponManager.Pickups, Has.Count.EqualTo(4));
            Assert.That(SceneManager.GetSceneByPath(TrackCatalog.BetaTemplateScenePath).isLoaded, Is.True);

            Assert.That(bootstrap.AIManager.PrepareRaceRoster(), Is.True);
            Assert.That(bootstrap.AIManager.ActiveBotCount, Is.EqualTo(1));
            Assert.That(bootstrap.RaceManager.StartRace(), Is.True);
            bootstrap.RaceManager.ResetRace();
            Assert.That(bootstrap.AIManager.ReleaseBotsForLobby(), Is.True);
            Assert.That(bootstrap.TryCycleTrack(1), Is.True);
            yield return WaitForTrack(bootstrap);
            Assert.That(bootstrap.ActiveTrackId, Is.EqualTo(TrackCatalog.WorkshopCircuitId));
            Assert.That(bootstrap.CircuitRoot.GetComponent<TrackAuthoringRoot>(), Is.Null);
            Assert.That(SceneManager.GetSceneByPath(TrackCatalog.BetaTemplateScenePath).isLoaded, Is.False);
            Assert.That(bootstrap.DemoPreferences.TrackId, Is.EqualTo(TrackCatalog.WorkshopCircuitId));
            Assert.That(bootstrap.VehicleManager.Players, Has.Count.EqualTo(1));
            Assert.That(human.IsConnected, Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyingSetupDuringSceneLoadCancelsAndUnloadsIt()
        {
            root = new GameObject("Cancel local scene track test");
            Stage3Bootstrap bootstrap = root.AddComponent<Stage3Bootstrap>();
            Assert.That(bootstrap.InitializeNow(), Is.True);
            for (int index = 0; index < 4; index++) Assert.That(bootstrap.TryCycleTrack(1), Is.True);
            Assert.That(bootstrap.TrackLoadPending, Is.True);

            Object.Destroy(root);
            root = null;
            yield return new WaitForSecondsRealtime(.5f);
            Scene scene = SceneManager.GetSceneByPath(TrackCatalog.BetaTemplateScenePath);
            Assert.That(!scene.IsValid() || !scene.isLoaded, Is.True,
                "Canceled additive loading must release the authored scene.");
        }

        private static IEnumerator WaitForTrack(Stage3Bootstrap bootstrap)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (bootstrap.TrackLoadPending && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(bootstrap.TrackLoadPending, Is.False, "Selected scene track did not finish loading or unloading.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            yield return null;
            Scene scene = SceneManager.GetSceneByPath(TrackCatalog.BetaTemplateScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) yield return unload;
            }
            if (hadPreferences) PlayerPrefs.SetString(DemoFlowPreferences.StorageKey, savedPreferences);
            else PlayerPrefs.DeleteKey(DemoFlowPreferences.StorageKey);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            Assert.That(InputUser.all.Count, Is.Zero);
            base.TearDown();
        }
    }
}
