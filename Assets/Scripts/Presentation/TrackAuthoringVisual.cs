using UnityEngine;

namespace Presentation
{
    /// <summary>Declares how a scene renderer participates in the existing visual-style controller.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class TrackAuthoringVisual : MonoBehaviour
    {
        [SerializeField] private bool outlineEligible;
        [SerializeField] private bool preserveSourceMaterial;

        public bool OutlineEligible => outlineEligible;
        public bool PreserveSourceMaterial => preserveSourceMaterial;

        public void Configure(bool includeOutline, bool preserveMaterial = false)
        {
            outlineEligible = includeOutline;
            preserveSourceMaterial = preserveMaterial;
        }
    }
}
