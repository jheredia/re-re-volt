using System;
using NUnit.Framework;
using Presentation;
using UnityEngine;

namespace Tests.EditMode
{
    public sealed class VisualStyleModeTests
    {
        private GameObject root;
        private VisualStyleController controller;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("VisualStyleModeTest");
            controller = root.AddComponent<VisualStyleController>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void DefaultsCleanAndCyclesInStableComparisonOrder()
        {
            Assert.That(controller.Mode, Is.EqualTo(VisualStyleMode.Clean3D));
            controller.CycleMode();
            Assert.That(controller.Mode, Is.EqualTo(VisualStyleMode.InkLite));
            controller.CycleMode();
            Assert.That(controller.Mode, Is.EqualTo(VisualStyleMode.FullInk));
            controller.CycleMode();
            Assert.That(controller.Mode, Is.EqualTo(VisualStyleMode.Clean3D));
        }

        [Test]
        public void SetModePublishesOnlyRealChangesAndRejectsUnknownValues()
        {
            int notifications = 0;
            VisualStyleMode observed = VisualStyleMode.Clean3D;
            controller.StyleChanged += next =>
            {
                notifications++;
                observed = next;
            };

            controller.SetMode(VisualStyleMode.Clean3D);
            controller.SetMode(VisualStyleMode.InkLite);
            controller.SetMode(VisualStyleMode.InkLite);

            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(observed, Is.EqualTo(VisualStyleMode.InkLite));
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.SetMode((VisualStyleMode)99));
        }
    }
}
