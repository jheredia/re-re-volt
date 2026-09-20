using NUnit.Framework;
using Weapons;
using Presentation;
using UnityEngine;

namespace Tests.EditMode
{
    public sealed class CombatRulesTests
    {
        [Test]
        public void PickupVolumesLeaveGridAndEveryRecoverySearchLaneClear()
        {
            for (int box = 0; box < 4; box++)
            {
                Vector3 position = WeaponManager.GetPickupPosition(box);
                for (int index = 0; index < PrototypeCircuitBuilder.OrderedCheckpointCount; index++)
                {
                    Pose checkpoint = PrototypeCircuitBuilder.GetCheckpointPose(index);
                    for (int attempt = 0; attempt < 7; attempt++)
                    {
                        float side = attempt == 0 ? 0f : attempt % 2 == 0 ? 2f : -2f;
                        float back = attempt < 3 ? 0f : -2.5f * ((attempt - 1) / 2);
                        Vector3 anchor = checkpoint.position + checkpoint.rotation * new Vector3(side, 0f, back);
                        Vector3 offset = Quaternion.Inverse(checkpoint.rotation) * (position - anchor);
                        Assert.That(Mathf.Abs(offset.x) > 1.47f || Mathf.Abs(offset.z) > 1.75f, Is.True,
                            $"Box {box} overlaps checkpoint {index}'s recovery search {attempt}.");
                    }
                }
                for (int grid = 0; grid < PrototypeCircuitBuilder.GridCount; grid++)
                    Assert.That(Vector3.Distance(position, PrototypeCircuitBuilder.GetGridPose(grid).position), Is.GreaterThan(3f));
            }
        }

        [Test]
        public void InventoryHasOneSlotAndAtomicConsumption()
        {
            var inventory = new CombatInventory();
            Assert.That(inventory.TryCollect(WeaponKind.None), Is.False);
            Assert.That(inventory.TryCollect((WeaponKind)99), Is.False);
            Assert.That(inventory.TryCollect(WeaponKind.Bolt), Is.True);
            Assert.That(inventory.TryCollect(WeaponKind.Oil), Is.False);
            Assert.That(inventory.TryFire(0, .35, out WeaponKind fired), Is.True);
            Assert.That(fired, Is.EqualTo(WeaponKind.Bolt));
            Assert.That(inventory.Held, Is.EqualTo(WeaponKind.None));
            Assert.That(inventory.TryFire(0, .35, out _), Is.False);
        }

        [Test]
        public void FireCooldownUsesOnlySuppliedSimulationTimeAndResetClearsIt()
        {
            var inventory = new CombatInventory();
            inventory.TryCollect(WeaponKind.Bolt);
            inventory.TryFire(10, .35, out _);
            inventory.TryCollect(WeaponKind.Oil);
            Assert.That(inventory.TryFire(10.349, .35, out _), Is.False);
            Assert.That(inventory.Held, Is.EqualTo(WeaponKind.Oil));
            Assert.That(inventory.TryFire(10.35, .35, out _), Is.True);
            inventory.Reset();
            inventory.TryCollect(WeaponKind.Bolt);
            Assert.That(inventory.TryFire(0, .35, out _), Is.True);
        }

        [TestCase(0, WeaponKind.Bolt)]
        [TestCase(1, WeaponKind.Oil)]
        [TestCase(2, WeaponKind.Bolt)]
        [TestCase(3, WeaponKind.Oil)]
        public void PickupAlternatesOnlyAfterSuccessfulCollectionAndResetRepeats(int index, WeaponKind first)
        {
            var box = new PickupCycle(index, 5);
            var a = new CombatInventory();
            var b = new CombatInventory();
            Assert.That(box.Offering, Is.EqualTo(first));
            Assert.That(box.TryCollect(a, 2), Is.True);
            Assert.That(a.Held, Is.EqualTo(first));
            Assert.That(box.TryCollect(b, 2), Is.False, "Two cars cannot consume one cycle.");
            Assert.That(box.IsAvailable(6.999), Is.False);
            Assert.That(box.IsAvailable(7), Is.True);
            WeaponKind second = first == WeaponKind.Bolt ? WeaponKind.Oil : WeaponKind.Bolt;
            Assert.That(box.Offering, Is.EqualTo(second));
            Assert.That(box.TryCollect(a, 7), Is.False, "Full inventory preserves box availability and selection.");
            Assert.That(box.TryCollect(b, 7), Is.True);
            Assert.That(b.Held, Is.EqualTo(second));
            box.Reset();
            Assert.That(box.IsAvailable(0), Is.True);
            Assert.That(box.Offering, Is.EqualTo(first));
        }

        [Test]
        public void PlayerInventoriesAndPickupCyclesAreIndependent()
        {
            var a = new CombatInventory();
            var b = new CombatInventory();
            var first = new PickupCycle(0, 5);
            var second = new PickupCycle(1, 5);
            first.TryCollect(a, 0);
            second.TryCollect(b, 0);
            a.TryFire(0, 1, out _);
            a.Reset();
            Assert.That(b.Held, Is.EqualTo(WeaponKind.Oil));
            Assert.That(second.IsAvailable(4), Is.False);
        }
    }
}
