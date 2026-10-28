using System;
using System.Collections.Generic;
using Core;
using Presentation;
using ReReVolt.Online.Contracts;
using UnityEngine;
using Vehicle;

namespace ReReVolt.Online.Unity
{
    /// <summary>
    /// Client-only view of host-accepted driving snapshots. This component owns visual
    /// proxy cars and peer-local chase cameras; it never creates physics, race rules,
    /// input devices, or a client simulation world.
    /// </summary>
    public sealed class OnlineRaceClientPresentation : MonoBehaviour
    {
        private sealed class Proxy
        {
            public uint RacerId;
            public GameObject Car;
            public Renderer[] Renderers;
        }

        private sealed class LocalView
        {
            public uint RacerId;
            public Camera Camera;
            public ChaseCamera Chase;
        }

        private readonly Dictionary<uint, Proxy> proxies = new Dictionary<uint, Proxy>(4);
        private readonly List<LocalView> localViews = new List<LocalView>(4);
        private readonly List<LobbyRacer> localHumans = new List<LobbyRacer>(4);
        private OnlineRaceMessageRouting routing;
        private VisualStyleController visualStyle;
        private GameObject content;
        private LobbySnapshot roster;
        private bool initialized;

        public bool IsInitialized => initialized;
        public int ProxyCount => proxies.Count;
        public int LocalCameraCount => localViews.Count;

        /// <summary>
        /// Creates the static client presentation roster. The caller must pass the locked
        /// roster and its own authenticated peer id; no state in this class is authoritative.
        /// </summary>
        public void Initialize(LobbySnapshot lockedRoster, string localPeer, VisualStyleController style = null)
        {
            if (initialized || content != null) throw new InvalidOperationException("This client presentation already owns a round.");
            if (lockedRoster == null || lockedRoster.Phase == OnlinePhase.Lobby || lockedRoster.Round == 0 ||
                lockedRoster.Racers == null || lockedRoster.Racers.Length < 1 || lockedRoster.Racers.Length > LocalPlayerRules.MaximumPlayers ||
                string.IsNullOrWhiteSpace(localPeer))
                throw new ArgumentException("A locked race roster and authenticated local peer are required.");

            roster = Copy(lockedRoster);
            visualStyle = style;
            localHumans.Clear();
            var ids = new HashSet<uint>();
            foreach (LobbyRacer racer in roster.Racers)
            {
                if (racer == null || racer.Id == 0 || !ids.Add(racer.Id))
                    throw new ArgumentException("The locked roster contains an invalid racer.", nameof(lockedRoster));
                if (!racer.Bot && string.Equals(racer.Peer, localPeer, StringComparison.Ordinal))
                    localHumans.Add(racer);
            }
            localHumans.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            if (localHumans.Count == 0)
                throw new ArgumentException("The local peer does not own a human racer in this roster.", nameof(localPeer));

            try
            {
                content = new GameObject("Online Client Presentation " + roster.Round);
                content.transform.SetParent(transform, false);
                for (int i = 0; i < roster.Racers.Length; i++) CreateProxy(roster.Racers[i], i);
                for (int i = 0; i < localHumans.Count; i++) CreateLocalView(localHumans[i], i);
                RefreshViewports();
                initialized = true;
            }
            catch
            {
                Shutdown();
                throw;
            }
        }

        /// <summary>Connects this view only to snapshots already accepted by the routing contract.</summary>
        public void BindAcceptedSnapshots(OnlineRaceMessageRouting messageRouting)
        {
            if (!initialized) throw new InvalidOperationException("Initialize client presentation before binding snapshots.");
            if (routing != null) throw new InvalidOperationException("This client presentation is already bound to a router.");
            if (messageRouting == null || messageRouting.IsHost)
                throw new ArgumentException("Only a non-host routing client can feed this presentation.", nameof(messageRouting));
            routing = messageRouting;
            routing.SnapshotAccepted += ApplyAcceptedSnapshot;
            DrivingWorldSnapshot latest = routing.LatestSnapshot;
            if (latest != null) ApplyAcceptedSnapshot(latest);
        }

        /// <summary>
        /// Applies an already-validated snapshot. It only changes proxy transforms and never
        /// writes a Rigidbody, WheelCollider, race manager, or local input state.
        /// </summary>
        public void ApplyAcceptedSnapshot(DrivingWorldSnapshot snapshot)
        {
            if (!initialized || snapshot == null || snapshot.Racers == null) return;
            foreach (DrivingRacerState state in snapshot.Racers)
            {
                if (!proxies.TryGetValue(state.RacerId, out Proxy proxy) || proxy.Car == null) continue;
                proxy.Car.transform.SetPositionAndRotation(Vector(state.Position), Rotation(state.Rotation));
            }
        }

        public bool TryGetProxy(uint racerId, out Transform proxy)
        {
            if (proxies.TryGetValue(racerId, out Proxy value) && value.Car != null)
            {
                proxy = value.Car.transform;
                return true;
            }
            proxy = null;
            return false;
        }

        public bool TryGetLocalCamera(uint racerId, out Camera camera)
        {
            for (int i = 0; i < localViews.Count; i++)
            {
                if (localViews[i].RacerId != racerId) continue;
                camera = localViews[i].Camera;
                return camera != null;
            }
            camera = null;
            return false;
        }

        private void CreateProxy(LobbyRacer racer, int rosterIndex)
        {
            GameObject car = PrototypeVehicleFactory.CreatePresentationProxy(new Pose(Vector3.zero, Quaternion.identity), rosterIndex);
            car.name = "Online Presentation Racer " + racer.Id;
            car.transform.SetParent(content.transform, true);
            Renderer[] renderers = car.GetComponentsInChildren<Renderer>(true);
            try
            {
                if (visualStyle != null)
                    for (int i = 0; i < renderers.Length; i++)
                        visualStyle.RegisterRenderer(renderers[i], renderers[i] is MeshRenderer);
                proxies.Add(racer.Id, new Proxy { RacerId = racer.Id, Car = car, Renderers = renderers });
            }
            catch
            {
                if (visualStyle != null)
                    for (int i = 0; i < renderers.Length; i++) visualStyle.UnregisterRenderer(renderers[i]);
                Release(car);
                throw;
            }
        }

        private void CreateLocalView(LobbyRacer racer, int displayIndex)
        {
            if (!proxies.TryGetValue(racer.Id, out Proxy proxy) || proxy.Car == null)
                throw new InvalidOperationException("A local player camera requires a presentation proxy.");
            GameObject cameraObject = new GameObject("Online Player Camera " + (displayIndex + 1));
            cameraObject.transform.SetParent(content.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 500f;
            camera.fieldOfView = 65f;
            camera.depth = 10 + displayIndex;
            ChaseCamera chase = cameraObject.AddComponent<ChaseCamera>();
            chase.Initialize(proxy.Car.transform);
            localViews.Add(new LocalView { RacerId = racer.Id, Camera = camera, Chase = chase });
        }

        private void RefreshViewports()
        {
            for (int i = 0; i < localViews.Count; i++)
            {
                Viewport viewport = LocalPlayerRules.GetViewport(localViews.Count, i);
                localViews[i].Camera.rect = new Rect(viewport.X, viewport.Y, viewport.Width, viewport.Height);
            }
        }

        public void Shutdown()
        {
            if (routing != null) routing.SnapshotAccepted -= ApplyAcceptedSnapshot;
            routing = null;
            foreach (Proxy proxy in proxies.Values)
                if (visualStyle != null && proxy.Renderers != null)
                    for (int i = 0; i < proxy.Renderers.Length; i++) visualStyle.UnregisterRenderer(proxy.Renderers[i]);
            proxies.Clear();
            localViews.Clear();
            localHumans.Clear();
            if (content != null)
            {
                content.SetActive(false);
                Release(content);
            }
            content = null;
            roster = null;
            visualStyle = null;
            initialized = false;
        }

        private static Vector3 Vector(WorldVector value) => new Vector3(value.X, value.Y, value.Z);
        private static Quaternion Rotation(WorldRotation value) => new Quaternion(value.X, value.Y, value.Z, value.W);
        private static void Release(GameObject value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private static LobbySnapshot Copy(LobbySnapshot source)
        {
            LobbyRacer[] racers = new LobbyRacer[source.Racers.Length];
            for (int i = 0; i < racers.Length; i++)
            {
                LobbyRacer racer = source.Racers[i];
                racers[i] = racer == null ? null : new LobbyRacer { Id = racer.Id, Peer = racer.Peer, Slot = racer.Slot, Bot = racer.Bot, Ready = racer.Ready };
            }
            return new LobbySnapshot
            {
                Epoch = source.Epoch, Revision = source.Revision, Round = source.Round, HostPeer = source.HostPeer,
                TrackId = source.TrackId, Phase = source.Phase, Peers = source.Peers == null ? null : (string[])source.Peers.Clone(), Racers = racers
            };
        }

        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();
    }
}
