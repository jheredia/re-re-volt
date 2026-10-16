using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Presentation
{
    /// <summary>
    /// Temporarily suppresses explicitly marked near-camera presentation geometry for this camera only.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class NearCameraPresentationOcclusion : MonoBehaviour
    {
        private const float CombatOrPickupDistance = 7f;
        private const float DecorationDistance = 5f;
        private const float MinimumProjectedArea = .022f;
        private const float MinimumProjectedWidth = .14f;
        private const float MinimumProjectedHeight = .16f;
        private static readonly Rect ForwardRoadCorridor = new(.16f, .04f, .68f, .76f);

        private readonly List<RendererState> suppressedRenderers = new(16);
        private Camera ownedCamera;
        private Transform target;

        private readonly struct RendererState
        {
            internal RendererState(Renderer renderer)
            {
                Renderer = renderer;
                WasForcedOff = renderer.forceRenderingOff;
            }

            internal Renderer Renderer { get; }
            internal bool WasForcedOff { get; }
        }

        public int SuppressedRendererCount => suppressedRenderers.Count;

        public void Initialize(Transform owningVehicle)
        {
            target = owningVehicle;
            ownedCamera = GetComponent<Camera>();
        }

        /// <summary>Read-only policy query used by focused visual regression coverage.</summary>
        public bool WouldOcclude(NearCameraPresentationOccludable candidate)
        {
            if (candidate == null || target == null || ownedCamera == null ||
                !candidate.isActiveAndEnabled || !candidate.TryGetWorldBounds(out Bounds bounds)) return false;
            if (candidate.VisibilityOwner == target) return false;

            float maximumDistance = candidate.Kind == NearCameraOcclusionKind.Decoration
                ? DecorationDistance
                : CombatOrPickupDistance;
            if (bounds.SqrDistance(ownedCamera.transform.position) > maximumDistance * maximumDistance) return false;

            Vector3 targetViewport = ownedCamera.WorldToViewportPoint(target.position + Vector3.up * .4f);
            Vector3 centerViewport = ownedCamera.WorldToViewportPoint(bounds.center);
            if (targetViewport.z <= ownedCamera.nearClipPlane || centerViewport.z <= ownedCamera.nearClipPlane)
                return false;

            // A weapon or hazard centred beyond the owning car is forward information, not foreground clutter.
            // Bounds may extend back toward the car, so classify depth by centre rather than radius.
            if (centerViewport.z > targetViewport.z + .25f) return false;

            if (!TryProjectBounds(bounds, out Rect projected)) return false;
            if (!projected.Overlaps(ForwardRoadCorridor, true)) return false;
            return projected.width * projected.height >= MinimumProjectedArea ||
                   projected.width >= MinimumProjectedWidth || projected.height >= MinimumProjectedHeight;
        }

        private bool TryProjectBounds(Bounds bounds, out Rect projected)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            bool found = false;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 world = new(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 viewport = ownedCamera.WorldToViewportPoint(world);
                if (viewport.z <= ownedCamera.nearClipPlane) continue;
                minX = Mathf.Min(minX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
                maxX = Mathf.Max(maxX, viewport.x);
                maxY = Mathf.Max(maxY, viewport.y);
                found = true;
            }

            projected = found ? Rect.MinMaxRect(minX, minY, maxX, maxY) : default;
            return found;
        }

        private void OnEnable()
        {
            ownedCamera = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RestoreSuppressedRenderers();
        }

        private void OnDestroy() => RestoreSuppressedRenderers();

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera != ownedCamera || target == null) return;
            RestoreSuppressedRenderers();
            foreach (NearCameraPresentationOccludable candidate in NearCameraPresentationOccludable.Candidates)
            {
                if (!WouldOcclude(candidate)) continue;
                Renderer[] renderers = candidate.Renderers;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null) continue;
                    suppressedRenderers.Add(new RendererState(renderer));
                    renderer.forceRenderingOff = true;
                }
            }
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera == ownedCamera) RestoreSuppressedRenderers();
        }

        private void RestoreSuppressedRenderers()
        {
            for (int i = suppressedRenderers.Count - 1; i >= 0; i--)
            {
                RendererState state = suppressedRenderers[i];
                if (state.Renderer != null) state.Renderer.forceRenderingOff = state.WasForcedOff;
            }
            suppressedRenderers.Clear();
        }
    }
}
