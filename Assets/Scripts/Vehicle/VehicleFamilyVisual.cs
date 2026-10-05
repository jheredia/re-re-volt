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
        private Renderer[] sourceRenderers = Array.Empty<Renderer>();
        private Renderer[] colorableBodyRenderers = Array.Empty<Renderer>();
        private IReadOnlyList<Renderer> sourceRenderersView = Array.Empty<Renderer>();
        private IReadOnlyList<Renderer> colorableBodyRenderersView = Array.Empty<Renderer>();

        public VehicleFamilyId FamilyId { get; private set; } = VehicleFamilyId.Touring;
        /// <summary>Every renderer owned by the replaceable family silhouette. Wheel visuals are excluded.</summary>
        public IReadOnlyList<Renderer> SourceRenderers => sourceRenderersView;
        public IReadOnlyList<Renderer> ColorableBodyRenderers => colorableBodyRenderersView;

        internal void Initialize(VehicleFamilyId familyId, IReadOnlyList<Renderer> familySourceRenderers,
            IReadOnlyList<Renderer> colorableRenderers)
        {
            if (!VehicleFamilyCatalog.IsDefined(familyId))
                throw new ArgumentOutOfRangeException(nameof(familyId), familyId, "Unknown vehicle family.");
            if (familySourceRenderers == null) throw new ArgumentNullException(nameof(familySourceRenderers));
            if (colorableRenderers == null) throw new ArgumentNullException(nameof(colorableRenderers));

            Renderer[] sourceCopy = CopyAndValidate(familySourceRenderers, nameof(familySourceRenderers));
            Renderer[] colorableCopy = CopyAndValidate(colorableRenderers, nameof(colorableRenderers));
            for (int i = 0; i < colorableCopy.Length; i++)
                if (Array.IndexOf(sourceCopy, colorableCopy[i]) < 0)
                    throw new ArgumentException("Every colorable body renderer must be a family source renderer.",
                        nameof(colorableRenderers));

            FamilyId = familyId;
            sourceRenderers = sourceCopy;
            colorableBodyRenderers = colorableCopy;
            sourceRenderersView = Array.AsReadOnly(sourceRenderers);
            colorableBodyRenderersView = Array.AsReadOnly(colorableBodyRenderers);
        }

        private static Renderer[] CopyAndValidate(IReadOnlyList<Renderer> renderers, string parameterName)
        {
            Renderer[] copy = new Renderer[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    throw new ArgumentException("Vehicle family renderer collections cannot contain null.", parameterName);
                if (Array.IndexOf(copy, renderer, 0, i) >= 0)
                    throw new ArgumentException("Vehicle family renderer collections cannot contain duplicates.", parameterName);
                copy[i] = renderer;
            }
            return copy;
        }
    }
}
