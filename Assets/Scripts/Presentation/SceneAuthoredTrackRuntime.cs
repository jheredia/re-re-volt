using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Presentation
{
    public enum SceneTrackMode { Authoritative, VisualOnly }

    /// <summary>
    /// Owns one additively loaded, Editor-authored track scene. The existing code-built
    /// tracks keep their synchronous ITrackRuntime construction path.
    /// </summary>
    public sealed class SceneAuthoredTrackRuntime : ITrackRuntime
    {
        private static readonly HashSet<string> LoadingPaths = new(StringComparer.Ordinal);

        private readonly Scene scene;
        private readonly VisualStyleController visualStyle;
        private readonly Renderer[] registeredRenderers;
        private readonly RaceCheckpoint[] checkpoints;
        private readonly Transform[] grid;
        private readonly Vector3[] pickupPositions;
        private bool disposed;

        private SceneAuthoredTrackRuntime(Scene loadedScene, TrackAuthoringRoot authoring,
            Transform[] gridAnchors, RaceCheckpoint[] orderedCheckpoints, Vector3[] pickups,
            VisualStyleController style, Renderer[] registered)
        {
            scene = loadedScene;
            Root = authoring.gameObject;
            grid = gridAnchors;
            checkpoints = orderedCheckpoints;
            pickupPositions = pickups;
            DrivableMask = authoring.DrivableMask;
            visualStyle = style;
            registeredRenderers = registered;
        }

        public GameObject Root { get; private set; }
        public Transform[] Grid => grid;
        public RaceCheckpoint[] Checkpoints => checkpoints;
        public LayerMask DrivableMask { get; }
        public IReadOnlyList<Vector3> PickupPositions => pickupPositions;
        public AsyncOperation UnloadOperation { get; private set; }

        /// <summary>
        /// Loads a scene already included in the player build. Cancellation waits for Unity's
        /// non-cancelable load to finish, then unloads it before returning the cancellation.
        /// </summary>
        public static async Task<SceneAuthoredTrackRuntime> LoadAsync(string scenePath,
            string expectedId, RaceManager race, VisualStyleController style,
            SceneTrackMode mode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(scenePath)) throw new ArgumentException("A track scene path is required.", nameof(scenePath));
            await Awaitable.MainThreadAsync();
            if (!Application.isPlaying) throw new InvalidOperationException("Load authored track scenes during Play mode.");
            cancellationToken.ThrowIfCancellationRequested();
            if (!LoadingPaths.Add(scenePath))
                throw new InvalidOperationException("This track scene is already loading: " + scenePath);

            var before = new HashSet<Scene>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
                before.Add(SceneManager.GetSceneAt(index));
            Scene loaded = default;
            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                if (load == null)
                    throw new InvalidOperationException("Unity could not load the authored track scene: " + scenePath);
                while (!load.isDone) await Awaitable.NextFrameAsync();
                for (int index = 0; index < SceneManager.sceneCount; index++)
                {
                    Scene candidate = SceneManager.GetSceneAt(index);
                    if (candidate.isLoaded && candidate.path == scenePath && !before.Contains(candidate))
                    {
                        loaded = candidate;
                        break;
                    }
                }
                if (!loaded.IsValid())
                    throw new InvalidOperationException("Loaded track scene could not be identified: " + scenePath);
                cancellationToken.ThrowIfCancellationRequested();
                return BindLoadedScene(loaded, expectedId, race, style, mode);
            }
            catch
            {
                if (loaded.IsValid() && loaded.isLoaded)
                {
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(loaded);
                    if (unload != null)
                        while (!unload.isDone) await Awaitable.NextFrameAsync();
                }
                throw;
            }
            finally { LoadingPaths.Remove(scenePath); }
        }

        /// <summary>
        /// Binds a scene already loaded by the caller. This also lets Editor PlayMode tests
        /// validate the asset before it is added to the player build scene list. A failed
        /// binding leaves scene unloading to the caller; it never starts race authority.
        /// </summary>
        public static SceneAuthoredTrackRuntime BindLoadedScene(Scene loadedScene,
            string expectedId, RaceManager race, VisualStyleController style, SceneTrackMode mode)
        {
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                throw new ArgumentException("A loaded track scene is required.", nameof(loadedScene));
            if (string.IsNullOrWhiteSpace(expectedId))
                throw new ArgumentException("An expected stable track ID is required.", nameof(expectedId));
            if (!Enum.IsDefined(typeof(SceneTrackMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == SceneTrackMode.Authoritative && race == null)
                throw new ArgumentNullException(nameof(race));
            if (mode == SceneTrackMode.VisualOnly && race != null)
                throw new ArgumentException("Visual-only tracks cannot own a RaceManager.", nameof(race));

            GameObject[] roots = loadedScene.GetRootGameObjects();
            if (roots.Length != 1 || roots[0].GetComponent<TrackAuthoringRoot>() == null)
                throw new InvalidOperationException("A track scene needs exactly one TrackAuthoringRoot as its only scene root.");
            TrackAuthoringRoot authoring = roots[0].GetComponent<TrackAuthoringRoot>();
            if (!string.Equals(authoring.StableId, expectedId, StringComparison.Ordinal))
                throw new InvalidOperationException("The loaded track ID differs from the selected track ID.");
            IReadOnlyList<string> errors = authoring.Validate();
            if (errors.Count != 0)
                throw new InvalidOperationException("Authored track validation failed: " + string.Join("; ", errors));

            RaceCheckpoint[] found = authoring.GetComponentsInChildren<RaceCheckpoint>(true);
            if (mode == SceneTrackMode.Authoritative &&
                (race.TotalLaps != authoring.Laps || race.CheckpointCount != found.Length))
                throw new InvalidOperationException("Race rules do not match the authored track lap/checkpoint contract.");

            var ordered = new RaceCheckpoint[found.Length];
            foreach (RaceCheckpoint checkpoint in found) ordered[checkpoint.CheckpointIndex] = checkpoint;
            Transform[] grid = OrderedAnchors(authoring, TrackAnchorKind.Grid, 4);
            Transform[] recovery = OrderedAnchors(authoring, TrackAnchorKind.Recovery, ordered.Length);
            Transform[] pickup = OrderedAnchors(authoring, TrackAnchorKind.Pickup, 4);
            var pickupPositions = new Vector3[4];
            for (int index = 0; index < 4; index++) pickupPositions[index] = pickup[index].position;

            var registered = new List<Renderer>();
            try
            {
                if (mode == SceneTrackMode.VisualOnly)
                    foreach (Collider collider in authoring.GetComponentsInChildren<Collider>(true))
                        collider.enabled = false;

                if (style != null)
                    foreach (TrackAuthoringVisual visual in authoring.GetComponentsInChildren<TrackAuthoringVisual>(true))
                    {
                        Renderer renderer = visual.GetComponent<Renderer>();
                        if (style.RegisterRenderer(renderer, visual.OutlineEligible, visual.PreserveSourceMaterial))
                            registered.Add(renderer);
                    }

                for (int index = 0; index < ordered.Length; index++)
                {
                    RaceCheckpoint checkpoint = ordered[index];
                    checkpoint.Configure(mode == SceneTrackMode.Authoritative ? race : null, index, recovery[index]);
                    checkpoint.GetComponent<Collider>().enabled = mode == SceneTrackMode.Authoritative;
                }

                return new SceneAuthoredTrackRuntime(loadedScene, authoring, grid, ordered,
                    pickupPositions, style, registered.ToArray());
            }
            catch
            {
                foreach (RaceCheckpoint checkpoint in ordered)
                    if (checkpoint != null) checkpoint.GetComponent<Collider>().enabled = false;
                if (style != null)
                    for (int index = registered.Count - 1; index >= 0; index--)
                        style.UnregisterRenderer(registered[index]);
                throw;
            }
        }

        private static Transform[] OrderedAnchors(TrackAuthoringRoot root, TrackAnchorKind kind, int count)
        {
            var result = new Transform[count];
            foreach (TrackAuthoringAnchor anchor in root.GetComponentsInChildren<TrackAuthoringAnchor>(true))
                if (anchor.Kind == kind) result[anchor.Index] = anchor.transform;
            return result;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Root != null) Root.SetActive(false);
            for (int index = 0; index < checkpoints.Length; index++)
            {
                RaceCheckpoint checkpoint = checkpoints[index];
                if (checkpoint == null) continue;
                checkpoint.GetComponent<Collider>().enabled = false;
                checkpoint.Configure(null, checkpoint.CheckpointIndex);
            }
            if (visualStyle != null)
                for (int index = registeredRenderers.Length - 1; index >= 0; index--)
                    visualStyle.UnregisterRenderer(registeredRenderers[index]);
            if (scene.IsValid() && scene.isLoaded)
                UnloadOperation = SceneManager.UnloadSceneAsync(scene);
            Root = null;
        }

        public async Task DisposeAsync()
        {
            await Awaitable.MainThreadAsync();
            Dispose();
            AsyncOperation unload = UnloadOperation;
            if (unload != null)
                while (!unload.isDone) await Awaitable.NextFrameAsync();
        }
    }
}
