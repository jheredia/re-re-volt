using System;
using System.Collections.Generic;
using Presentation;
using ReReVolt.Online.Contracts;
using UnityEngine;
using Weapons;

namespace ReReVolt.Online.Unity
{
    /// <summary>Snapshot-only pickup and live-weapon visuals; it owns no colliders or combat rules.</summary>
    public sealed class OnlineCombatPresentation : MonoBehaviour
    {
        private sealed class WeaponView
        {
            internal GameObject Object;
            internal Renderer Renderer;
            internal Vector3 From, To;
            internal double ReceivedAt;
            internal float BlendSeconds;
        }

        private sealed class ImpactPulse
        {
            internal GameObject Object;
            internal Renderer Renderer;
            internal double BornAt;
        }

        private readonly Dictionary<uint, WeaponView> weaponViews = new Dictionary<uint, WeaponView>(16);
        private readonly List<ImpactPulse> pulses = new List<ImpactPulse>(16);
        private readonly HashSet<uint> seen = new HashSet<uint>();
        private readonly List<uint> stale = new List<uint>(16);
        private readonly GameObject[] boltPickups = new GameObject[4], oilPickups = new GameObject[4];
        private readonly Renderer[] pickupRenderers = new Renderer[8];
        private OnlineRaceMessageRouting routing;
        private VisualStyleController style;
        private GameObject root;
        private Material boltMaterial, oilMaterial;
        private bool host;
        private uint lastEventId;

        public int LiveWeaponViewCount => weaponViews.Count;
        public int ActivePulseCount => pulses.Count;
        public int VisiblePickupCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (boltPickups[i] != null && boltPickups[i].activeSelf) count++;
                    if (oilPickups[i] != null && oilPickups[i].activeSelf) count++;
                }
                return count;
            }
        }

        public void Initialize(IReadOnlyList<Vector3> pickupPositions, bool hostPeer,
            VisualStyleController visualStyle = null)
        {
            if (root != null) throw new InvalidOperationException("Combat presentation is already initialized.");
            if (pickupPositions == null || pickupPositions.Count != 4)
                throw new ArgumentException("Four authored pickup sites are required.", nameof(pickupPositions));
            host = hostPeer;
            style = visualStyle;
            try
            {
                root = new GameObject("Online Combat Presentation");
                root.transform.SetParent(transform, false);
                if (!host)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        boltPickups[i] = CreateVisual("Client Bolt pickup " + i, PrimitiveType.Cube,
                            pickupPositions[i], new Vector3(.72f, .72f, .72f), WeaponKind.Bolt,
                            out pickupRenderers[i * 2]);
                        boltPickups[i].transform.rotation = Quaternion.Euler(0, 45, 25);
                        oilPickups[i] = CreateVisual("Client Oil pickup " + i, PrimitiveType.Sphere,
                            pickupPositions[i], new Vector3(.76f, .76f, .76f), WeaponKind.Oil,
                            out pickupRenderers[i * 2 + 1]);
                        boltPickups[i].SetActive(false);
                        oilPickups[i].SetActive(false);
                    }
                }
            }
            catch { Shutdown(); throw; }
        }

        public void Bind(OnlineRaceMessageRouting messageRouting)
        {
            if (root == null || routing != null || messageRouting == null || messageRouting.IsHost != host)
                throw new InvalidOperationException("Combat visuals require their own round's routing mode.");
            routing = messageRouting;
            if (host) routing.HostSnapshotPublished += ApplySnapshot;
            else routing.SnapshotAccepted += ApplySnapshot;
            if (!host && routing.LatestSnapshot != null) ApplySnapshot(routing.LatestSnapshot);
        }

        public void ApplySnapshot(DrivingWorldSnapshot snapshot)
        {
            if (root == null || snapshot?.Pickups == null || snapshot.Pickups.Length != 4 ||
                snapshot.Weapons == null || snapshot.Weapons.Length > 16 ||
                snapshot.CombatEvents == null || snapshot.CombatEvents.Length > 16) return;
            if (!host)
                for (int i = 0; i < 4; i++)
                {
                    DrivingPickupState pickup = snapshot.Pickups[i];
                    boltPickups[i].SetActive(pickup.Available && pickup.Offering == WeaponKind.Bolt);
                    oilPickups[i].SetActive(pickup.Available && pickup.Offering == WeaponKind.Oil);
                }
            double now = Time.realtimeSinceStartupAsDouble;
            seen.Clear();
            foreach (DrivingWeaponState weapon in snapshot.Weapons)
            {
                seen.Add(weapon.Id);
                Vector3 position = new Vector3(weapon.Position.X, weapon.Position.Y, weapon.Position.Z);
                if (!weaponViews.TryGetValue(weapon.Id, out WeaponView view))
                {
                    GameObject visual = CreateVisual("Online " + weapon.Kind + " " + weapon.Id,
                        weapon.Kind == WeaponKind.Bolt ? PrimitiveType.Sphere : PrimitiveType.Cylinder,
                        position, weapon.Kind == WeaponKind.Bolt ? new Vector3(.48f, .48f, 1.2f) :
                        new Vector3(2.7f, .025f, 2.7f), weapon.Kind, out Renderer renderer);
                    visual.transform.rotation = weapon.Kind == WeaponKind.Bolt ?
                        Quaternion.LookRotation(new Vector3(weapon.Direction.X, weapon.Direction.Y, weapon.Direction.Z)) :
                        Quaternion.identity;
                    weaponViews.Add(weapon.Id, new WeaponView { Object = visual, Renderer = renderer,
                        From = position, To = position, ReceivedAt = now, BlendSeconds = 0 });
                    continue;
                }
                Advance(view, now);
                view.From = view.Object.transform.position;
                view.To = position;
                view.ReceivedAt = now;
                view.BlendSeconds = .05f;
            }
            // The host's snapshot is a complete bounded set, so absent IDs expire without a separate RPC.
            stale.Clear();
            foreach (uint id in weaponViews.Keys) if (!seen.Contains(id)) stale.Add(id);
            for (int i = 0; i < stale.Count; i++) RemoveWeapon(stale[i]);
            foreach (DrivingCombatEvent effect in snapshot.CombatEvents)
            {
                if (effect.Id <= lastEventId) continue;
                if (pulses.Count == 16) RemovePulse(0);
                Vector3 point = new Vector3(effect.Position.X, effect.Position.Y, effect.Position.Z);
                GameObject visual = CreateVisual("Online " + effect.Kind + " pulse " + effect.Id,
                    PrimitiveType.Sphere, point, Vector3.one * .35f, effect.Weapon, out Renderer renderer);
                pulses.Add(new ImpactPulse { Object = visual, Renderer = renderer, BornAt = now });
                lastEventId = effect.Id;
            }
        }

        private void LateUpdate()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (WeaponView view in weaponViews.Values) Advance(view, now);
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                float age = (float)(now - pulses[i].BornAt);
                if (age >= .4f) { RemovePulse(i); continue; }
                if (pulses[i].Object != null)
                    pulses[i].Object.transform.localScale = Vector3.one * (.35f + age * 3f);
            }
        }

        private static void Advance(WeaponView view, double now)
        {
            if (view.Object == null || view.BlendSeconds <= 0) return;
            float t = Mathf.Clamp01((float)((now - view.ReceivedAt) / view.BlendSeconds));
            view.Object.transform.position = Vector3.LerpUnclamped(view.From, view.To, t);
        }

        private GameObject CreateVisual(string name, PrimitiveType shape, Vector3 position, Vector3 scale,
            WeaponKind kind, out Renderer renderer)
        {
            GameObject visual = GameObject.CreatePrimitive(shape);
            visual.name = name;
            visual.transform.SetParent(root.transform, true);
            visual.transform.position = position;
            visual.transform.localScale = scale;
            Collider collider = visual.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            renderer = visual.GetComponent<Renderer>();
            Material material = kind == WeaponKind.Bolt ? boltMaterial : oilMaterial;
            if (material == null && renderer.sharedMaterial != null)
            {
                material = new Material(renderer.sharedMaterial);
                material.color = kind == WeaponKind.Bolt ? new Color(1f, .42f, .05f) :
                    new Color(.62f, .17f, .9f);
                if (kind == WeaponKind.Bolt) boltMaterial = material; else oilMaterial = material;
            }
            if (material != null) renderer.sharedMaterial = material;
            style?.RegisterRenderer(renderer, true);
            return visual;
        }

        private void RemoveWeapon(uint id)
        {
            WeaponView view = weaponViews[id];
            if (view.Renderer != null) style?.UnregisterRenderer(view.Renderer);
            if (view.Object != null) Release(view.Object);
            weaponViews.Remove(id);
        }

        private void RemovePulse(int index)
        {
            ImpactPulse pulse = pulses[index];
            if (pulse.Renderer != null) style?.UnregisterRenderer(pulse.Renderer);
            if (pulse.Object != null) Release(pulse.Object);
            pulses.RemoveAt(index);
        }

        public void Shutdown()
        {
            if (routing != null)
            {
                if (host) routing.HostSnapshotPublished -= ApplySnapshot;
                else routing.SnapshotAccepted -= ApplySnapshot;
            }
            routing = null;
            stale.Clear();
            foreach (uint id in weaponViews.Keys) stale.Add(id);
            for (int i = 0; i < stale.Count; i++) RemoveWeapon(stale[i]);
            for (int i = pulses.Count - 1; i >= 0; i--) RemovePulse(i);
            for (int i = 0; i < pickupRenderers.Length; i++)
                if (pickupRenderers[i] != null) style?.UnregisterRenderer(pickupRenderers[i]);
            Array.Clear(boltPickups, 0, boltPickups.Length);
            Array.Clear(oilPickups, 0, oilPickups.Length);
            Array.Clear(pickupRenderers, 0, pickupRenderers.Length);
            if (root != null) { root.SetActive(false); Release(root); }
            root = null;
            if (boltMaterial != null) Release(boltMaterial);
            if (oilMaterial != null) Release(oilMaterial);
            boltMaterial = oilMaterial = null;
            style = null;
            seen.Clear();
            stale.Clear();
            lastEventId = 0;
        }

        private static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();
    }
}
