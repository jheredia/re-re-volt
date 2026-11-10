using System.Collections;
using Core;
using NUnit.Framework;
using Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class SceneAuthoredTrackRuntimeTests
    {
        private const string TemplatePath = "Assets/Scenes/Track_BetaTemplate.unity";
        private const string TemplateId = "custom-beta-track";
        private Scene loaded;
        private SceneAuthoredTrackRuntime runtime;
        private GameObject raceObject;

        [UnityTest]
        public IEnumerator SavedTemplateBindsAuthoritativeCheckpointsThenUnloads()
        {
            yield return LoadTemplate();
            raceObject = new GameObject("Authored track test race");
            RaceManager race = raceObject.AddComponent<RaceManager>();
            Assert.That(race.ConfigureRace(3, 8), Is.True);

            runtime = SceneAuthoredTrackRuntime.BindLoadedScene(loaded, TemplateId, race,
                null, SceneTrackMode.Authoritative);
            Assert.That(runtime.Root, Is.Not.Null);
            Assert.That(runtime.Grid, Has.Length.EqualTo(4));
            Assert.That(runtime.Checkpoints, Has.Length.EqualTo(8));
            Assert.That(runtime.PickupPositions.Count, Is.EqualTo(4));
            Assert.That(runtime.DrivableMask.value, Is.Not.Zero);
            for (int index = 0; index < runtime.Checkpoints.Length; index++)
            {
                Assert.That(runtime.Checkpoints[index].CheckpointIndex, Is.EqualTo(index));
                Assert.That(runtime.Checkpoints[index].GetComponent<Collider>().enabled, Is.True,
                    "Only an authoritative bound course should enable checkpoint triggers.");
            }

            runtime.Dispose();
            if (runtime.UnloadOperation != null) yield return runtime.UnloadOperation;
            Assert.That(loaded.isLoaded, Is.False);
            runtime = null;
        }

        [UnityTest]
        public IEnumerator SavedTemplateCanBindVisualOnlyAndRepeatAfterUnload()
        {
            for (int iteration = 0; iteration < 2; iteration++)
            {
                yield return LoadTemplate();
                Assert.Throws<System.InvalidOperationException>(() =>
                    SceneAuthoredTrackRuntime.BindLoadedScene(loaded, "wrong-id", null,
                        null, SceneTrackMode.VisualOnly));

                runtime = SceneAuthoredTrackRuntime.BindLoadedScene(loaded, TemplateId, null,
                    null, SceneTrackMode.VisualOnly);
                foreach (Collider collider in runtime.Root.GetComponentsInChildren<Collider>(true))
                    Assert.That(collider.enabled, Is.False,
                        "Client visual courses must have no active track physics or checkpoint triggers.");
                if (iteration == 0)
                {
                    var dispose = runtime.DisposeAsync();
                    while (!dispose.IsCompleted) yield return null;
                    if (dispose.IsFaulted) throw dispose.Exception;
                }
                else
                {
                    runtime.Dispose();
                    if (runtime.UnloadOperation != null) yield return runtime.UnloadOperation;
                }
                Assert.That(loaded.isLoaded, Is.False);
                runtime = null;
            }
        }

        private IEnumerator LoadTemplate()
        {
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(TemplatePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null);
            yield return load;
            loaded = SceneManager.GetSceneByPath(TemplatePath);
            Assert.That(loaded.IsValid() && loaded.isLoaded, Is.True);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (runtime != null)
            {
                runtime.Dispose();
                if (runtime.UnloadOperation != null) yield return runtime.UnloadOperation;
                runtime = null;
            }
            else if (loaded.IsValid() && loaded.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(loaded);
                if (unload != null) yield return unload;
            }
            loaded = default;
            if (raceObject != null) Object.Destroy(raceObject);
            yield return null;
        }
    }
}
