using System;
using System.Collections.Generic;
using Core;
using ReReVolt.Online.Contracts;
using UnityEngine;
using Vehicle;

namespace ReReVolt.Online.Unity
{
    /// <summary>Peer-local chase views of the host's real cars; never owns race or vehicle state.</summary>
    public sealed class OnlineHostLocalPresentation : MonoBehaviour
    {
        private readonly List<Camera> cameras = new List<Camera>(4);
        private GameObject content;

        public int LocalCameraCount => cameras.Count;

        public void Initialize(LobbySnapshot roster, string localPeer, OnlineRaceHostWorld world)
        {
            if (content != null) throw new InvalidOperationException("Host views are already initialized.");
            if (roster == null || roster.Racers == null || string.IsNullOrWhiteSpace(localPeer) ||
                world == null || !world.IsInitialized)
                throw new ArgumentException("Host views require a locked roster and initialized host world.");
            var owned = new List<LobbyRacer>(4);
            foreach (LobbyRacer racer in roster.Racers)
                if (racer != null && !racer.Bot && string.Equals(racer.Peer, localPeer, StringComparison.Ordinal))
                    owned.Add(racer);
            owned.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            if (owned.Count == 0) throw new InvalidOperationException("The host must own a human racer.");

            try
            {
                content = new GameObject("Online Host Local Views " + roster.Round);
                content.transform.SetParent(transform, false);
                for (int i = 0; i < owned.Count; i++)
                {
                    if (!world.TryGetVehicle(owned[i].Id, out VehicleController vehicle) || vehicle == null)
                        throw new InvalidOperationException("A host camera requires its authoritative vehicle.");
                    GameObject cameraObject = new GameObject("Online Host Player Camera " + (i + 1));
                    cameraObject.transform.SetParent(content.transform, false);
                    Camera camera = cameraObject.AddComponent<Camera>();
                    camera.nearClipPlane = .1f;
                    camera.farClipPlane = 500f;
                    camera.fieldOfView = 65f;
                    camera.depth = 10 + i;
                    Viewport viewport = LocalPlayerRules.GetViewport(owned.Count, i);
                    camera.rect = new Rect(viewport.X, viewport.Y, viewport.Width, viewport.Height);
                    cameraObject.AddComponent<ChaseCamera>().Initialize(vehicle.transform);
                    cameras.Add(camera);
                }
            }
            catch
            {
                Shutdown();
                throw;
            }
        }

        public void Shutdown()
        {
            cameras.Clear();
            if (content == null) return;
            content.SetActive(false);
            if (Application.isPlaying) Destroy(content); else DestroyImmediate(content);
            content = null;
        }

        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();
    }
}
