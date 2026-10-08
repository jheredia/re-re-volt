using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Presentation
{
    /// <summary>
    /// Owns the session-wide rendering treatment for explicitly registered renderers.
    /// Source materials and property blocks are never mutated or owned by this component.
    /// </summary>
    public sealed class VisualStyleController : MonoBehaviour
    {
        private const string InkMaterialResource = "VisualStyles/InkLit";
        private const string OutlineMaterialResource = "VisualStyles/InkOutline";
        private const string OutlineSuffix = "_InkOutline";

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int BandCountId = Shader.PropertyToID("_BandCount");
        private static readonly int InkContrastId = Shader.PropertyToID("_InkContrast");
        private static readonly int AmbientFloorId = Shader.PropertyToID("_AmbientFloor");

        [SerializeField] private Material inkMaterialTemplate;
        [SerializeField] private Material outlineMaterial;

        private readonly Dictionary<Renderer, Registration> registrations = new();
        private readonly Dictionary<Material, Material> inkMaterials = new();
        private readonly List<Renderer> staleRenderers = new(8);
        private VisualStyleMode mode;

        public event Action<VisualStyleMode> StyleChanged;

        public VisualStyleMode Mode => mode;
        public int RegisteredRendererCount => registrations.Count;
        public int InkMaterialCount => inkMaterials.Count;

        public int OutlineObjectCount
        {
            get
            {
                int count = 0;
                foreach (Registration registration in registrations.Values)
                {
                    if (registration.OutlineObject != null) count++;
                }
                return count;
            }
        }

        private sealed class Registration
        {
            public Renderer Renderer;
            public Material[] SourceMaterials;
            public Material[] InkMaterials;
            public bool OutlineEligible;
            public bool PreserveSourceMaterial;
            public GameObject OutlineObject;
            public Renderer OutlineRenderer;
        }

        private void Awake()
        {
            mode = VisualStyleMode.Clean3D;
            LoadRetainedMaterials();
        }

        /// <summary>Overrides retained material assets before any renderer is registered.</summary>
        public void ConfigureTemplates(Material inkTemplate, Material sharedOutlineMaterial)
        {
            if (registrations.Count > 0 || inkMaterials.Count > 0)
                throw new InvalidOperationException("Visual style templates cannot change after registration.");
            inkMaterialTemplate = inkTemplate != null
                ? inkTemplate
                : throw new ArgumentNullException(nameof(inkTemplate));
            outlineMaterial = sharedOutlineMaterial != null
                ? sharedOutlineMaterial
                : throw new ArgumentNullException(nameof(sharedOutlineMaterial));
        }

        /// <summary>
        /// Registers a renderer and remembers its current shared material references as the Clean source.
        /// Re-registering is idempotent and never treats a controller-owned ink material as a new source.
        /// Outlined registration currently supports MeshRenderer only; skinned renderers may register without outlines.
        /// Presentation-only transparent surfaces can preserve their source material while remaining explicitly
        /// registered.
        /// </summary>
        public bool RegisterRenderer(Renderer renderer, bool outlineEligible, bool preserveSourceMaterial = false)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            ValidateOutlineSupport(renderer, outlineEligible);

            if (registrations.TryGetValue(renderer, out Registration existing))
            {
                existing.OutlineEligible = outlineEligible;
                if (existing.PreserveSourceMaterial != preserveSourceMaterial)
                {
                    existing.PreserveSourceMaterial = preserveSourceMaterial;
                    existing.InkMaterials = preserveSourceMaterial
                        ? existing.SourceMaterials
                        : GetInkMaterials(existing.SourceMaterials);
                }
                EnsureOutline(existing);
                ApplyRegistration(existing);
                return false;
            }

            Registration registration = new()
            {
                Renderer = renderer,
                SourceMaterials = renderer.sharedMaterials,
                OutlineEligible = outlineEligible,
                PreserveSourceMaterial = preserveSourceMaterial
            };
            registration.InkMaterials = preserveSourceMaterial
                ? registration.SourceMaterials
                : GetInkMaterials(registration.SourceMaterials);
            EnsureOutline(registration);
            registrations.Add(renderer, registration);
            ApplyRegistration(registration);
            return true;
        }

        /// <summary>Updates a single-material renderer's Clean source, for example a reused pickup shell.</summary>
        public bool UpdateSourceMaterial(Renderer renderer, Material sourceMaterial)
        {
            Registration registration = GetRegistration(renderer);
            if (registration.SourceMaterials.Length == 1 &&
                ReferenceEquals(registration.SourceMaterials[0], sourceMaterial))
                return false;

            ReplaceSourceMaterials(registration, new[] { sourceMaterial });
            return true;
        }

        /// <summary>
        /// Updates all Clean source material references without disturbing the active mode.
        /// Returns false without cloning or assigning when references and ordering are unchanged.
        /// </summary>
        public bool UpdateSourceMaterials(Renderer renderer, Material[] sourceMaterials)
        {
            if (sourceMaterials == null) throw new ArgumentNullException(nameof(sourceMaterials));
            Registration registration = GetRegistration(renderer);
            if (MaterialsMatch(registration.SourceMaterials, sourceMaterials)) return false;

            ReplaceSourceMaterials(registration, sourceMaterials);
            return true;
        }

        public bool UnregisterRenderer(Renderer renderer)
        {
            if (renderer == null || !registrations.TryGetValue(renderer, out Registration registration))
                return false;

            RestoreClean(registration);
            DestroyOwnedObject(registration.OutlineObject);
            registrations.Remove(renderer);
            return true;
        }

        /// <summary>
        /// Releases registrations whose source renderer was destroyed without an explicit unregister.
        /// Transient owners should still pair registration with <see cref="UnregisterRenderer"/>.
        /// </summary>
        public int PruneDestroyedRenderers()
        {
            staleRenderers.Clear();
            foreach (KeyValuePair<Renderer, Registration> pair in registrations)
            {
                if (pair.Value.Renderer == null) staleRenderers.Add(pair.Key);
            }

            int removed = 0;
            for (int i = 0; i < staleRenderers.Count; i++)
            {
                Renderer key = staleRenderers[i];
                if (!registrations.TryGetValue(key, out Registration registration)) continue;
                DestroyOwnedObject(registration.OutlineObject);
                if (registrations.Remove(key)) removed++;
            }
            staleRenderers.Clear();
            return removed;
        }

        public void SetMode(VisualStyleMode nextMode)
        {
            if (!Enum.IsDefined(typeof(VisualStyleMode), nextMode))
                throw new ArgumentOutOfRangeException(nameof(nextMode));
            PruneDestroyedRenderers();
            if (mode == nextMode) return;
            if (nextMode != VisualStyleMode.Clean3D) RequireInkTemplate();

            mode = nextMode;
            UpdateInkProfile();
            foreach (Registration registration in registrations.Values)
                ApplyRegistration(registration);
            StyleChanged?.Invoke(mode);
        }

        public void CycleMode()
        {
            SetMode(mode switch
            {
                VisualStyleMode.Clean3D => VisualStyleMode.InkLite,
                VisualStyleMode.InkLite => VisualStyleMode.FullInk,
                _ => VisualStyleMode.Clean3D
            });
        }

        private void LoadRetainedMaterials()
        {
            if (inkMaterialTemplate == null)
                inkMaterialTemplate = Resources.Load<Material>(InkMaterialResource);
            if (outlineMaterial == null)
                outlineMaterial = Resources.Load<Material>(OutlineMaterialResource);
        }

        private void ApplyRegistration(Registration registration)
        {
            if (registration.Renderer == null) return;

            if (mode == VisualStyleMode.Clean3D || registration.PreserveSourceMaterial)
                RestoreClean(registration);
            else
                registration.Renderer.sharedMaterials = registration.InkMaterials;

            bool showOutline = mode == VisualStyleMode.FullInk && registration.OutlineEligible;
            if (registration.OutlineObject != null)
                registration.OutlineObject.SetActive(showOutline);
        }

        private void RestoreClean(Registration registration)
        {
            if (registration.Renderer != null)
                registration.Renderer.sharedMaterials = registration.SourceMaterials;
            if (registration.OutlineObject != null)
                registration.OutlineObject.SetActive(false);
        }

        private Registration GetRegistration(Renderer renderer)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            if (!registrations.TryGetValue(renderer, out Registration registration))
                throw new InvalidOperationException("The renderer must be registered before its source material is updated.");
            return registration;
        }

        private static bool MaterialsMatch(Material[] current, Material[] incoming)
        {
            if (current.Length != incoming.Length) return false;
            for (int i = 0; i < current.Length; i++)
            {
                if (!ReferenceEquals(current[i], incoming[i])) return false;
            }
            return true;
        }

        private void ReplaceSourceMaterials(Registration registration, Material[] sourceMaterials)
        {
            registration.SourceMaterials = (Material[])sourceMaterials.Clone();
            registration.InkMaterials = registration.PreserveSourceMaterial
                ? registration.SourceMaterials
                : GetInkMaterials(registration.SourceMaterials);
            SetOutlineMaterials(registration);
            ApplyRegistration(registration);
        }

        private Material[] GetInkMaterials(Material[] sourceMaterials)
        {
            Material[] styled = new Material[sourceMaterials.Length];
            for (int i = 0; i < sourceMaterials.Length; i++)
                styled[i] = GetOrCreateInkMaterial(sourceMaterials[i]);
            return styled;
        }

        private Material GetOrCreateInkMaterial(Material source)
        {
            if (source == null) return null;
            if (inkMaterials.TryGetValue(source, out Material ink)) return ink;

            RequireInkTemplate();
            ink = new Material(inkMaterialTemplate)
            {
                name = source.name + " (Ink)",
                enableInstancing = source.enableInstancing,
                renderQueue = source.renderQueue,
                hideFlags = HideFlags.DontSave
            };
            CopySurfaceProperties(source, ink);
            ApplyInkProfile(ink);
            inkMaterials.Add(source, ink);
            return ink;
        }

        private static void CopySurfaceProperties(Material source, Material target)
        {
            Color color = Color.white;
            if (source.HasProperty(BaseColorId)) color = source.GetColor(BaseColorId);
            else if (source.HasProperty(LegacyColorId)) color = source.GetColor(LegacyColorId);
            target.SetColor(BaseColorId, color);

            if (source.HasProperty(BaseMapId))
            {
                target.SetTexture(BaseMapId, source.GetTexture(BaseMapId));
                target.SetTextureScale(BaseMapId, source.GetTextureScale(BaseMapId));
                target.SetTextureOffset(BaseMapId, source.GetTextureOffset(BaseMapId));
            }
            else if (source.mainTexture != null)
            {
                target.SetTexture(BaseMapId, source.mainTexture);
                target.SetTextureScale(BaseMapId, source.mainTextureScale);
                target.SetTextureOffset(BaseMapId, source.mainTextureOffset);
            }

            if (source.HasProperty(EmissionMapId))
                target.SetTexture(EmissionMapId, source.GetTexture(EmissionMapId));
            if (source.HasProperty(EmissionColorId))
                target.SetColor(EmissionColorId, source.GetColor(EmissionColorId));
            if (source.HasProperty(MetallicId))
                target.SetFloat(MetallicId, source.GetFloat(MetallicId));
            if (source.HasProperty(SmoothnessId))
                target.SetFloat(SmoothnessId, source.GetFloat(SmoothnessId));
        }

        private void UpdateInkProfile()
        {
            foreach (Material ink in inkMaterials.Values)
                ApplyInkProfile(ink);
        }

        private void ApplyInkProfile(Material ink)
        {
            bool fullInk = mode == VisualStyleMode.FullInk;
            ink.SetFloat(BandCountId, fullInk ? 4f : 3f);
            ink.SetFloat(InkContrastId, fullInk ? 1.2f : .82f);
            ink.SetFloat(AmbientFloorId, fullInk ? .2f : .28f);
        }

        private void RequireInkTemplate()
        {
            LoadRetainedMaterials();
            if (inkMaterialTemplate == null)
                throw new InvalidOperationException($"Missing retained material Resources/{InkMaterialResource}.");
        }

        private void EnsureOutline(Registration registration)
        {
            if (!registration.OutlineEligible)
            {
                if (registration.OutlineObject != null)
                    registration.OutlineObject.SetActive(false);
                return;
            }
            if (registration.OutlineObject != null) return;

            LoadRetainedMaterials();
            if (outlineMaterial == null)
                throw new InvalidOperationException($"Missing retained material Resources/{OutlineMaterialResource}.");

            GameObject outline = new GameObject(registration.Renderer.gameObject.name + OutlineSuffix)
            {
                layer = registration.Renderer.gameObject.layer,
                hideFlags = HideFlags.DontSave
            };
            outline.transform.SetParent(registration.Renderer.transform, false);

            Renderer outlineRenderer;
            if (registration.Renderer is MeshRenderer)
            {
                MeshFilter sourceFilter = registration.Renderer.GetComponent<MeshFilter>();
                if (sourceFilter == null)
                {
                    DestroyOwnedObject(outline);
                    throw new InvalidOperationException("A registered MeshRenderer needs a MeshFilter for outlines.");
                }
                MeshFilter outlineFilter = outline.AddComponent<MeshFilter>();
                outlineFilter.sharedMesh = sourceFilter.sharedMesh;
                outlineRenderer = outline.AddComponent<MeshRenderer>();
            }
            else
            {
                DestroyOwnedObject(outline);
                throw new NotSupportedException($"Outlines do not support {registration.Renderer.GetType().Name}.");
            }

            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
            outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            outline.SetActive(mode == VisualStyleMode.FullInk);
            registration.OutlineObject = outline;
            registration.OutlineRenderer = outlineRenderer;
            SetOutlineMaterials(registration);
        }

        private static void ValidateOutlineSupport(Renderer renderer, bool outlineEligible)
        {
            if (!outlineEligible) return;
            if (renderer is SkinnedMeshRenderer)
                throw new NotSupportedException(
                    "Outlined SkinnedMeshRenderer registration is deferred; register it without outline eligibility.");
            if (renderer is not MeshRenderer)
                throw new NotSupportedException($"Outlines do not support {renderer.GetType().Name}.");
        }

        private void SetOutlineMaterials(Registration registration)
        {
            if (registration.OutlineRenderer == null) return;
            int materialCount = Mathf.Max(1, registration.SourceMaterials.Length);
            Material[] outlineMaterials = new Material[materialCount];
            for (int i = 0; i < outlineMaterials.Length; i++) outlineMaterials[i] = outlineMaterial;
            registration.OutlineRenderer.sharedMaterials = outlineMaterials;
        }

        private void OnDestroy()
        {
            foreach (Registration registration in registrations.Values)
            {
                RestoreClean(registration);
                DestroyOwnedObject(registration.OutlineObject);
            }
            registrations.Clear();

            foreach (Material ink in inkMaterials.Values)
                DestroyOwnedObject(ink);
            inkMaterials.Clear();
        }

        private static void DestroyOwnedObject(UnityEngine.Object owned)
        {
            if (owned == null) return;
            if (Application.isPlaying) Destroy(owned);
            else DestroyImmediate(owned);
        }
    }
}
