using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vehicle
{
    /// <summary>
    /// Stages a generated family silhouette beside the current one so its rendering dependencies can be
    /// registered before the old silhouette is destroyed. Dispose without committing to leave the car unchanged.
    /// </summary>
    public sealed class VehicleFamilyVisualReplacement : IDisposable
    {
        private readonly VehicleFamilyVisual target;
        private readonly VehicleFamilyId oldFamilyId;
        private readonly Renderer[] oldSourceRenderers;
        private readonly Renderer[] oldColorableBodyRenderers;
        private readonly Renderer[] newSourceRenderers;
        private readonly Renderer[] newColorableBodyRenderers;
        private readonly IReadOnlyList<Renderer> newSourceRenderersView;
        private readonly IReadOnlyList<Renderer> newColorableBodyRenderersView;
        private bool completed;

        internal VehicleFamilyVisualReplacement(VehicleFamilyVisual targetVisual, VehicleFamilyId targetFamilyId,
            IReadOnlyList<Renderer> stagedSourceRenderers, IReadOnlyList<Renderer> stagedColorableBodyRenderers)
        {
            target = targetVisual ?? throw new ArgumentNullException(nameof(targetVisual));
            TargetFamilyId = targetFamilyId;
            oldFamilyId = targetVisual.FamilyId;
            oldSourceRenderers = Copy(targetVisual.SourceRenderers);
            oldColorableBodyRenderers = Copy(targetVisual.ColorableBodyRenderers);
            newSourceRenderers = Copy(stagedSourceRenderers);
            newColorableBodyRenderers = Copy(stagedColorableBodyRenderers);
            newSourceRenderersView = Array.AsReadOnly(newSourceRenderers);
            newColorableBodyRenderersView = Array.AsReadOnly(newColorableBodyRenderers);
        }

        public VehicleFamilyId TargetFamilyId { get; }
        public IReadOnlyList<Renderer> NewSourceRenderers => newSourceRenderersView;
        public IReadOnlyList<Renderer> NewColorableBodyRenderers => newColorableBodyRenderersView;

        /// <summary>Promotes the staged parts and retires only the previous family-owned visual objects.</summary>
        public void Commit()
        {
            if (completed) throw new InvalidOperationException("This vehicle-family replacement is already complete.");
            if (target == null) throw new InvalidOperationException("The target vehicle was destroyed before replacement.");

            try
            {
                // Validate and publish the marker first. The remaining operations are local GameObject state changes.
                target.Initialize(TargetFamilyId, newSourceRenderers, newColorableBodyRenderers);
                for (int i = 0; i < oldSourceRenderers.Length; i++)
                {
                    Renderer renderer = oldSourceRenderers[i];
                    if (renderer == null) continue;
                    renderer.gameObject.name = PrototypeVehicleFactory.RetiredPartPrefix + renderer.gameObject.name;
                    renderer.gameObject.SetActive(false);
                }
                for (int i = 0; i < newSourceRenderers.Length; i++)
                {
                    Renderer renderer = newSourceRenderers[i];
                    if (renderer == null) continue;
                    string name = renderer.gameObject.name;
                    if (name.StartsWith(PrototypeVehicleFactory.StagedPartPrefix, StringComparison.Ordinal))
                        renderer.gameObject.name = name.Substring(PrototypeVehicleFactory.StagedPartPrefix.Length);
                    renderer.gameObject.SetActive(true);
                }

                DestroyRendererObjects(oldSourceRenderers);
                completed = true;
            }
            catch
            {
                target.Initialize(oldFamilyId, oldSourceRenderers, oldColorableBodyRenderers);
                for (int i = 0; i < oldSourceRenderers.Length; i++)
                {
                    Renderer renderer = oldSourceRenderers[i];
                    if (renderer == null) continue;
                    string name = renderer.gameObject.name;
                    if (name.StartsWith(PrototypeVehicleFactory.RetiredPartPrefix, StringComparison.Ordinal))
                        renderer.gameObject.name = name.Substring(PrototypeVehicleFactory.RetiredPartPrefix.Length);
                    renderer.gameObject.SetActive(true);
                }
                for (int i = 0; i < newSourceRenderers.Length; i++)
                {
                    Renderer renderer = newSourceRenderers[i];
                    if (renderer == null) continue;
                    if (!renderer.gameObject.name.StartsWith(PrototypeVehicleFactory.StagedPartPrefix,
                            StringComparison.Ordinal))
                        renderer.gameObject.name = PrototypeVehicleFactory.StagedPartPrefix + renderer.gameObject.name;
                    renderer.gameObject.SetActive(false);
                }
                throw;
            }
        }

        public void Dispose()
        {
            if (completed) return;
            DestroyRendererObjects(newSourceRenderers);
            completed = true;
        }

        private static Renderer[] Copy(IReadOnlyList<Renderer> renderers)
        {
            Renderer[] copy = new Renderer[renderers.Count];
            for (int i = 0; i < renderers.Count; i++) copy[i] = renderers[i];
            return copy;
        }

        private static void DestroyRendererObjects(IReadOnlyList<Renderer> renderers)
        {
            HashSet<GameObject> destroyed = new HashSet<GameObject>();
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !destroyed.Add(renderer.gameObject)) continue;
                renderer.gameObject.SetActive(false);
                PrototypeVehicleFactory.DestroyGeneratedObject(renderer.gameObject);
            }
        }
    }

    /// <summary>Minimal usable car for code-built scenes; authored prefabs can replace it.</summary>
    public static class PrototypeVehicleFactory
    {
        internal const string StagedPartPrefix = "__StagedFamily__";
        internal const string RetiredPartPrefix = "__RetiredFamily__";
        private static readonly Vector3 ChassisSize = new(1.1f, .4f, 1.7f);
        private static readonly Color DarkDetailColor = new(.035f, .045f, .055f);
        private static readonly Color GlassColor = new(.06f, .14f, .18f);

        public static GameObject Create(Pose pose)
        {
            return Create(pose, VehicleFamilyCatalog.DefaultId);
        }

        /// <summary>
        /// Builds one of the four visual families without changing the shared first-pass physics or handling setup.
        /// </summary>
        public static GameObject Create(Pose pose, VehicleFamilyId familyId)
        {
            if (!VehicleFamilyCatalog.IsDefined(familyId))
                throw new ArgumentOutOfRangeException(nameof(familyId), familyId, "Unknown vehicle family.");

            GameObject car = new GameObject("PrototypeVehicle");
            car.transform.SetPositionAndRotation(pose.position, pose.rotation);
            Rigidbody body = car.AddComponent<Rigidbody>();
            body.mass = 150f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            BoxCollider chassis = car.AddComponent<BoxCollider>();
            chassis.size = ChassisSize;

            List<Renderer> sourceRenderers = new(10);
            List<Renderer> colorableBodyRenderers = new(8);
            CreateSilhouette(car.transform, familyId, sourceRenderers, colorableBodyRenderers, string.Empty, true);

            WheelCollider fl = CreateWheel(car.transform, "Wheel_FL", -.6f, .55f);
            WheelCollider fr = CreateWheel(car.transform, "Wheel_FR", .6f, .55f);
            WheelCollider rl = CreateWheel(car.transform, "Wheel_RL", -.6f, -.55f);
            WheelCollider rr = CreateWheel(car.transform, "Wheel_RR", .6f, -.55f);
            car.AddComponent<VehicleController>().ConfigureWheels(fl, fr, rl, rr);
            car.AddComponent<VehicleFamilyVisual>().Initialize(familyId, sourceRenderers, colorableBodyRenderers);
            return car;
        }

        /// <summary>
        /// Builds an inactive replacement silhouette while leaving the current visual and all root gameplay state intact.
        /// The caller may register <see cref="VehicleFamilyVisualReplacement.NewSourceRenderers"/> transactionally,
        /// then commit, or dispose to roll back.
        /// </summary>
        public static VehicleFamilyVisualReplacement PrepareFamilyVisualReplacement(GameObject vehicle,
            VehicleFamilyId familyId)
        {
            if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
            if (!VehicleFamilyCatalog.IsDefined(familyId))
                throw new ArgumentOutOfRangeException(nameof(familyId), familyId, "Unknown vehicle family.");

            VehicleFamilyVisual current = vehicle.GetComponent<VehicleFamilyVisual>();
            if (current == null || current.SourceRenderers.Count == 0)
                throw new InvalidOperationException("Only vehicles with a generated VehicleFamilyVisual can replace families.");
            for (int i = 0; i < current.SourceRenderers.Count; i++)
            {
                Renderer source = current.SourceRenderers[i];
                if (source == null || source.gameObject == vehicle || !source.transform.IsChildOf(vehicle.transform))
                    throw new InvalidOperationException("The current family visual no longer owns a valid child renderer.");
            }

            List<Renderer> stagedSources = new(10);
            List<Renderer> stagedColorable = new(8);
            try
            {
                CreateSilhouette(vehicle.transform, familyId, stagedSources, stagedColorable, StagedPartPrefix, false);
                return new VehicleFamilyVisualReplacement(current, familyId, stagedSources, stagedColorable);
            }
            catch
            {
                for (int i = vehicle.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = vehicle.transform.GetChild(i).gameObject;
                    if (child.name.StartsWith(StagedPartPrefix, StringComparison.Ordinal))
                        DestroyGeneratedObject(child);
                }
                throw;
            }
        }

        private static void CreateSilhouette(Transform parent, VehicleFamilyId familyId,
            List<Renderer> sourceRenderers, List<Renderer> colorableBodyRenderers, string namePrefix, bool active)
        {
            switch (familyId)
            {
                case VehicleFamilyId.Touring:
                    CreateTouring(parent, sourceRenderers, colorableBodyRenderers, namePrefix, active);
                    return;
                case VehicleFamilyId.Buggy:
                    CreateBuggy(parent, sourceRenderers, colorableBodyRenderers, namePrefix, active);
                    return;
                case VehicleFamilyId.RcTruck:
                    CreateTruck(parent, sourceRenderers, colorableBodyRenderers, namePrefix, active);
                    return;
                case VehicleFamilyId.Drift:
                    CreateDrift(parent, sourceRenderers, colorableBodyRenderers, namePrefix, active);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(familyId), familyId, "Unknown vehicle family.");
            }
        }

        private static void CreateTouring(Transform parent, List<Renderer> sourceRenderers,
            List<Renderer> bodyRenderers, string namePrefix, bool active)
        {
            CreateBodyPart(parent, "Body", new Vector3(0f, 0f, 0f), new Vector3(1.1f, .28f, 1.7f),
                sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_TouringCabin", new Vector3(0f, .24f, -.08f),
                new Vector3(.82f, .3f, .72f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_TouringSpoiler", new Vector3(0f, .27f, -.72f),
                new Vector3(1f, .05f, .16f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateFamilyDetail(parent, "Detail_TouringWindshield", PrimitiveType.Cube,
                new Vector3(0f, .26f, .285f), new Vector3(.7f, .2f, .035f),
                new Vector3(-8f, 0f, 0f), GlassColor, sourceRenderers, namePrefix, active);
        }

        private static void CreateBuggy(Transform parent, List<Renderer> sourceRenderers,
            List<Renderer> bodyRenderers, string namePrefix, bool active)
        {
            CreateBodyPart(parent, "Body", new Vector3(0f, -.015f, 0f), new Vector3(.78f, .24f, 1.4f),
                sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_BuggyNose", new Vector3(0f, .105f, .48f),
                new Vector3(.62f, .16f, .5f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_BuggyCockpit", new Vector3(0f, .205f, -.12f),
                new Vector3(.58f, .22f, .5f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_BuggyFenderFL", new Vector3(-.48f, .06f, .52f),
                new Vector3(.2f, .11f, .36f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_BuggyFenderFR", new Vector3(.48f, .06f, .52f),
                new Vector3(.2f, .11f, .36f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_BuggyFenderRL", new Vector3(-.48f, .06f, -.52f),
                new Vector3(.2f, .11f, .36f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_BuggyFenderRR", new Vector3(.48f, .06f, -.52f),
                new Vector3(.2f, .11f, .36f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateFamilyDetail(parent, "Detail_BuggyRollBarLeft", PrimitiveType.Cube,
                new Vector3(-.27f, .36f, -.12f), new Vector3(.055f, .34f, .055f),
                new Vector3(0f, 0f, -16f), DarkDetailColor, sourceRenderers, namePrefix, active);
            CreateFamilyDetail(parent, "Detail_BuggyRollBarRight", PrimitiveType.Cube,
                new Vector3(.27f, .36f, -.12f), new Vector3(.055f, .34f, .055f),
                new Vector3(0f, 0f, 16f), DarkDetailColor, sourceRenderers, namePrefix, active);
        }

        private static void CreateTruck(Transform parent, List<Renderer> sourceRenderers,
            List<Renderer> bodyRenderers, string namePrefix, bool active)
        {
            CreateBodyPart(parent, "Body", new Vector3(0f, 0f, 0f), new Vector3(1.08f, .3f, 1.62f),
                sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_TruckCab", new Vector3(0f, .28f, .36f),
                new Vector3(.94f, .43f, .66f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_TruckBedLeft", new Vector3(-.45f, .23f, -.43f),
                new Vector3(.12f, .24f, .68f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_TruckBedRight", new Vector3(.45f, .23f, -.43f),
                new Vector3(.12f, .24f, .68f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_TruckTailgate", new Vector3(0f, .23f, -.72f),
                new Vector3(.9f, .24f, .1f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateFamilyDetail(parent, "Detail_TruckWindshield", PrimitiveType.Cube,
                new Vector3(0f, .33f, .68f), new Vector3(.76f, .25f, .035f),
                new Vector3(-5f, 0f, 0f), GlassColor, sourceRenderers, namePrefix, active);
            CreateFamilyDetail(parent, "Detail_TruckBed", PrimitiveType.Cube, new Vector3(0f, .16f, -.43f),
                new Vector3(.78f, .055f, .58f), Vector3.zero, DarkDetailColor, sourceRenderers, namePrefix, active);
        }

        private static void CreateDrift(Transform parent, List<Renderer> sourceRenderers,
            List<Renderer> bodyRenderers, string namePrefix, bool active)
        {
            CreateBodyPart(parent, "Body", new Vector3(0f, -.03f, 0f), new Vector3(1.08f, .22f, 1.72f),
                sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_DriftCabin", new Vector3(0f, .175f, -.13f),
                new Vector3(.78f, .24f, .66f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_DriftHood", new Vector3(0f, .115f, .52f),
                new Vector3(.98f, .1f, .58f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateBodyPart(parent, "Body_DriftWing", new Vector3(0f, .36f, -.72f),
                new Vector3(1.08f, .055f, .17f), sourceRenderers, bodyRenderers, namePrefix, active);
            CreateFamilyDetail(parent, "Detail_DriftWingLeft", PrimitiveType.Cube,
                new Vector3(-.38f, .25f, -.72f), new Vector3(.055f, .23f, .055f), Vector3.zero,
                DarkDetailColor, sourceRenderers, namePrefix, active);
            CreateFamilyDetail(parent, "Detail_DriftWingRight", PrimitiveType.Cube,
                new Vector3(.38f, .25f, -.72f), new Vector3(.055f, .23f, .055f), Vector3.zero,
                DarkDetailColor, sourceRenderers, namePrefix, active);
            CreateFamilyDetail(parent, "Detail_DriftWindshield", PrimitiveType.Cube,
                new Vector3(0f, .205f, .185f), new Vector3(.66f, .16f, .035f),
                new Vector3(-10f, 0f, 0f), GlassColor, sourceRenderers, namePrefix, active);
        }

        private static Renderer CreateBodyPart(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, List<Renderer> sourceRenderers, List<Renderer> bodyRenderers,
            string namePrefix, bool active)
        {
            Renderer renderer = CreatePart(parent, name, PrimitiveType.Cube, localPosition, localScale,
                Vector3.zero, null, namePrefix, active);
            sourceRenderers.Add(renderer);
            bodyRenderers.Add(renderer);
            return renderer;
        }

        private static Renderer CreateFamilyDetail(Transform parent, string name, PrimitiveType primitive,
            Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Color color,
            List<Renderer> sourceRenderers, string namePrefix, bool active)
        {
            Renderer renderer = CreatePart(parent, name, primitive, localPosition, localScale, localEulerAngles,
                color, namePrefix, active);
            sourceRenderers.Add(renderer);
            return renderer;
        }

        private static Renderer CreateDetail(Transform parent, string name, PrimitiveType primitive,
            Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Color color)
        {
            return CreatePart(parent, name, primitive, localPosition, localScale, localEulerAngles, color);
        }

        private static Renderer CreatePart(Transform parent, string name, PrimitiveType primitive,
            Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Color? color,
            string namePrefix = "", bool active = true)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = namePrefix + name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEulerAngles);
            part.transform.localScale = localScale;
            DisableAndRemovePrimitiveCollider(part);

            Renderer renderer = part.GetComponent<Renderer>();
            if (color.HasValue)
            {
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", color.Value);
                properties.SetColor("_Color", color.Value);
                renderer.SetPropertyBlock(properties);
            }
            part.SetActive(active);
            return renderer;
        }

        private static WheelCollider CreateWheel(Transform parent, string name, float x, float z)
        {
            GameObject wheel = new GameObject(name);
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = new Vector3(x, -.1f, z);
            WheelCollider collider = wheel.AddComponent<WheelCollider>();
            collider.mass = 5f;
            collider.radius = .25f;
            collider.suspensionDistance = .2f;
            JointSpring suspension = collider.suspensionSpring;
            suspension.spring = 9000f;
            suspension.damper = 1000f;
            suspension.targetPosition = .5f;
            collider.suspensionSpring = suspension;

            CreateDetail(wheel.transform, "WheelVisual", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(.5f, .09f, .5f), new Vector3(0f, 0f, 90f), DarkDetailColor);
            return collider;
        }

        private static void DisableAndRemovePrimitiveCollider(GameObject primitive)
        {
            Collider primitiveCollider = primitive.GetComponent<Collider>();
            if (primitiveCollider == null) return;
            // Runtime removal is deferred, so disable immediately to keep all family collision footprints identical.
            primitiveCollider.enabled = false;
            if (Application.isPlaying) UnityEngine.Object.Destroy(primitiveCollider);
            else UnityEngine.Object.DestroyImmediate(primitiveCollider);
        }

        internal static void DestroyGeneratedObject(UnityEngine.Object generated)
        {
            if (generated == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(generated);
            else UnityEngine.Object.DestroyImmediate(generated);
        }
    }
}
