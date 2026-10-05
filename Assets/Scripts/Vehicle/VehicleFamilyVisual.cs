using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vehicle
{
    /// <summary>
    /// Runtime identity and explicit color targets for a generated family silhouette.
    /// Stage 7 lobby integration can recolor these renderers without hierarchy-name searches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleFamilyVisual : MonoBehaviour
    {
        private Renderer[] colorableBodyRenderers = Array.Empty<Renderer>();
        private IReadOnlyList<Renderer> colorableBodyRenderersView = Array.Empty<Renderer>();

        public VehicleFamilyId FamilyId { get; private set; } = VehicleFamilyId.Touring;
        public IReadOnlyList<Renderer> ColorableBodyRenderers => colorableBodyRenderersView;

        internal void Initialize(VehicleFamilyId familyId, IReadOnlyList<Renderer> renderers)
        {
            if (!VehicleFamilyCatalog.IsDefined(familyId))
                throw new ArgumentOutOfRangeException(nameof(familyId), familyId, "Unknown vehicle family.");
            if (renderers == null) throw new ArgumentNullException(nameof(renderers));

            Renderer[] copy = new Renderer[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
            {
                copy[i] = renderers[i] != null
                    ? renderers[i]
                    : throw new ArgumentException("Colorable body renderers cannot contain null.", nameof(renderers));
            }

            FamilyId = familyId;
            colorableBodyRenderers = copy;
            colorableBodyRenderersView = Array.AsReadOnly(colorableBodyRenderers);
        }
    }
}
