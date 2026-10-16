using UnityEngine;
using Presentation;
using Weapons;

namespace Core
{
    /// <summary>A trigger and visual for a cycle owned by the configured WeaponManager.</summary>
    public sealed class PickupSpawner : MonoBehaviour
    {
        [SerializeField] private float respawnCooldown = 5f;
        private WeaponManager owner;
        private PickupCycle cycle;
        private Transform box;
        private Renderer boxRenderer;
        private Material boxSourceMaterial;
        private Collider trigger;
        private GameObject boltIdentity;
        private GameObject oilIdentity;

        // Three bolt-zig segments plus oil drop and puddle; Full Ink may add one outline per source.
        public const int IdentityRendererBudget = 5;

        public bool IsAvailable => cycle != null && cycle.IsAvailable(owner.SimulationTime);
        public WeaponKind Offering => cycle?.Offering ?? WeaponKind.None;
        public int PickupIndex { get; private set; } = -1;

        public void Configure(WeaponManager manager, int index)
        {
            owner = manager;
            PickupIndex = index;
            cycle = new PickupCycle(index, Mathf.Max(.1f, respawnCooldown));
            BoxCollider volume = gameObject.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(1.5f, 1.7f, 1.5f);
            trigger = volume;
            box = owner.CreateVisual(transform, "Pickup box", PrimitiveType.Cube,
                Vector3.zero, Vector3.one * .85f, owner.BoltMaterial, true).transform;
            boxRenderer = box.GetComponent<Renderer>();
            boxSourceMaterial = owner.BoltMaterial;
            owner.CreateVisual(box, "White band", PrimitiveType.Cube,
                Vector3.zero, new Vector3(1.05f, .2f, 1.05f), owner.FeedbackMaterial);
            BuildIdentityIcons();
            owner.CreateVisual(transform, "Pickup base", PrimitiveType.Cylinder,
                Vector3.down * .72f, new Vector3(1.7f, .045f, 1.7f), owner.FeedbackMaterial);
            Refresh();
            gameObject.AddComponent<NearCameraPresentationOccludable>()
                .Configure(NearCameraOcclusionKind.CombatOrPickup);
        }

        private void BuildIdentityIcons()
        {
            boltIdentity = new GameObject("Bolt pickup identity");
            boltIdentity.transform.SetParent(box, false);
            boltIdentity.transform.localPosition = Vector3.up * 1.02f;
            CreateBoltSegment("Bolt zig upper", new Vector3(.13f, .22f, 0f), 24f);
            CreateBoltSegment("Bolt zig middle", new Vector3(-.02f, 0f, 0f), -28f);
            CreateBoltSegment("Bolt zig lower", new Vector3(-.14f, -.22f, 0f), 24f);

            oilIdentity = new GameObject("Oil pickup identity");
            oilIdentity.transform.SetParent(box, false);
            oilIdentity.transform.localPosition = Vector3.up * 1.02f;
            GameObject drop = owner.CreateVisual(oilIdentity.transform, "Oil drop",
                PrimitiveType.Sphere, Vector3.up * .05f, new Vector3(.48f, .64f, .24f), owner.OilMaterial, true);
            drop.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            owner.CreateVisual(oilIdentity.transform, "Oil puddle mark", PrimitiveType.Cylinder,
                Vector3.down * .28f, new Vector3(.72f, .05f, .42f), owner.FeedbackMaterial, true);
        }

        private void CreateBoltSegment(string label, Vector3 position, float roll)
        {
            GameObject segment = owner.CreateVisual(boltIdentity.transform, label, PrimitiveType.Cube,
                position, new Vector3(.17f, .47f, .17f), owner.BoltMaterial, true);
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        }

        internal bool TryCollect(CombatInventory inventory)
        {
            if (cycle == null || !cycle.TryCollect(inventory, owner.SimulationTime)) return false;
            Refresh();
            return true;
        }

        internal void Step(float delta)
        {
            if (box == null) return;
            box.Rotate(0f, 65f * delta, 0f, Space.Self);
            box.localPosition = Vector3.up * (.1f * Mathf.Sin((float)owner.SimulationTime * 3f));
            Refresh();
        }

        internal void ResetPickup()
        {
            cycle?.Reset();
            Refresh();
        }

        private void Refresh()
        {
            if (box == null) return;
            bool available = IsAvailable;
            box.gameObject.SetActive(available);
            trigger.enabled = available;
            bool offersBolt = Offering == WeaponKind.Bolt;
            boltIdentity.SetActive(offersBolt);
            oilIdentity.SetActive(!offersBolt);
            Material nextMaterial = offersBolt ? owner.BoltMaterial : owner.OilMaterial;
            owner.UpdateVisualSourceMaterial(boxRenderer, nextMaterial, ref boxSourceMaterial);
        }

        private void OnTriggerEnter(Collider other) => owner?.TryCollect(this, other);
        private void OnTriggerStay(Collider other) => owner?.TryCollect(this, other);

        private void OnDestroy()
        {
            if (owner != null) owner.UnregisterVisualTree(gameObject);
        }
    }
}
