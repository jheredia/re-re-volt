using UnityEngine;
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
        private Collider trigger;

        public bool IsAvailable => cycle != null && cycle.IsAvailable(owner.SimulationTime);
        public WeaponKind Offering => cycle?.Offering ?? WeaponKind.None;

        public void Configure(WeaponManager manager, int index)
        {
            owner = manager;
            cycle = new PickupCycle(index, Mathf.Max(.1f, respawnCooldown));
            BoxCollider volume = gameObject.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(1.5f, 1.7f, 1.5f);
            trigger = volume;
            box = owner.CreateVisual(transform, "Pickup box", PrimitiveType.Cube,
                Vector3.zero, Vector3.one * .85f, owner.BoltMaterial).transform;
            boxRenderer = box.GetComponent<Renderer>();
            owner.CreateVisual(box, "White band", PrimitiveType.Cube,
                Vector3.zero, new Vector3(1.05f, .2f, 1.05f), owner.FeedbackMaterial);
            owner.CreateVisual(transform, "Pickup base", PrimitiveType.Cylinder,
                Vector3.down * .72f, new Vector3(1.7f, .045f, 1.7f), owner.FeedbackMaterial);
            Refresh();
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
            boxRenderer.sharedMaterial = Offering == WeaponKind.Bolt ? owner.BoltMaterial : owner.OilMaterial;
        }

        private void OnTriggerEnter(Collider other) => owner?.TryCollect(this, other);
        private void OnTriggerStay(Collider other) => owner?.TryCollect(this, other);
    }
}
