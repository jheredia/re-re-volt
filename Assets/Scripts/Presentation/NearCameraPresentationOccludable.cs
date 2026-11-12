using System;
using System.Collections.Generic;
using UnityEngine;

namespace Presentation
{
    public enum NearCameraOcclusionKind
    {
        CombatOrPickup = 0,
        Decoration = 1
    }

    /// <summary>
    /// Marks presentation-only geometry that a chase camera may suppress for its own render.
    /// Gameplay colliders and authority stay on the unmodified root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NearCameraPresentationOccludable : MonoBehaviour
    {
        private static readonly HashSet<NearCameraPresentationOccludable> ActiveCandidates = new();
        [SerializeField] private NearCameraOcclusionKind kind;
        [SerializeField] private Transform visibilityOwner;
        private Renderer[] renderers = Array.Empty<Renderer>();

        public NearCameraOcclusionKind Kind => kind;
        public Transform VisibilityOwner => visibilityOwner;
        public int RendererCount => renderers.Length;
        internal static IEnumerable<NearCameraPresentationOccludable> Candidates => ActiveCandidates;
        internal Renderer[] Renderers => renderers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => ActiveCandidates.Clear();

        public void Configure(NearCameraOcclusionKind kind, Transform visibilityOwner = null)
        {
            this.kind = kind;
            this.visibilityOwner = visibilityOwner;
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        internal bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private void Awake()
        {
            if (renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void OnEnable() => ActiveCandidates.Add(this);
        private void OnDisable() => ActiveCandidates.Remove(this);
        private void OnDestroy() => ActiveCandidates.Remove(this);
    }
}
