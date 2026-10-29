using Presentation;
using ReReVolt.Online.Contracts;
using UnityEngine;
using Weapons;

namespace ReReVolt.Online.Unity
{
    /// <summary>Host physics trigger and readable world marker for one authored pickup site.</summary>
    public sealed class OnlineHostPickup : MonoBehaviour
    {
        private OnlineRaceHostWorld owner;
        private VisualStyleController visualStyle;
        private BoxCollider trigger;
        private GameObject boltMarker;
        private GameObject oilMarker;
        private Renderer[] renderers;
        private int index;
        private bool available;
        private WeaponKind offering;

        public void Initialize(OnlineRaceHostWorld host, int pickupIndex, Vector3 position,
            VisualStyleController style)
        {
            if (owner != null) throw new System.InvalidOperationException("Pickup is already initialized.");
            owner = host != null ? host : throw new System.ArgumentNullException(nameof(host));
            index = pickupIndex;
            visualStyle = style;
            transform.position = position;
            trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.5f, 1.7f, 1.5f);

            boltMarker = CreateMarker("Bolt pickup", PrimitiveType.Cube, Vector3.zero,
                new Vector3(.72f, .72f, .72f));
            boltMarker.transform.localRotation = Quaternion.Euler(0, 45, 25);
            oilMarker = CreateMarker("Oil pickup", PrimitiveType.Sphere, Vector3.zero,
                new Vector3(.76f, .76f, .76f));
            renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers) visualStyle?.RegisterRenderer(renderer, true);
            offering = (WeaponKind)(-1);
            Refresh(false, WeaponKind.None);
        }

        public void Refresh(bool isAvailable, WeaponKind nextOffering)
        {
            if (trigger == null || (available == isAvailable && offering == nextOffering)) return;
            available = isAvailable;
            offering = nextOffering;
            trigger.enabled = available;
            boltMarker.SetActive(available && offering == WeaponKind.Bolt);
            oilMarker.SetActive(available && offering == WeaponKind.Oil);
        }

        private GameObject CreateMarker(string label, PrimitiveType shape, Vector3 localPosition, Vector3 scale)
        {
            GameObject marker = GameObject.CreatePrimitive(shape);
            marker.name = label;
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = scale;
            Collider collider = marker.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            return marker;
        }

        private void OnTriggerEnter(Collider other) => owner?.TryCollectPickup(index, other);
        private void OnTriggerStay(Collider other) => owner?.TryCollectPickup(index, other);

        private void OnDestroy()
        {
            if (visualStyle == null || renderers == null) return;
            foreach (Renderer renderer in renderers)
                if (renderer != null) visualStyle.UnregisterRenderer(renderer);
        }
    }
}
