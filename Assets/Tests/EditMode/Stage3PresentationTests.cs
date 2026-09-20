using System;
using NUnit.Framework;
using Presentation;
using UnityEngine;

namespace Tests.EditMode
{
    public sealed class Stage3PresentationTests
    {
        [Test]
        public void CircuitContractHasFourSafeGridPosesAndOrderedClosedRoute()
        {
            Assert.That(PrototypeCircuitBuilder.GridCount, Is.EqualTo(4));
            Assert.That(PrototypeCircuitBuilder.OrderedCheckpointCount, Is.EqualTo(8));

            for (int i = 0; i < PrototypeCircuitBuilder.GridCount; i++)
            {
                Pose grid = PrototypeCircuitBuilder.GetGridPose(i);
                Assert.That(grid.position.x, Is.InRange(-18.5f, -11.5f));
                Assert.That(grid.position.z, Is.InRange(-10.1f, -6.9f));
                Assert.That(Vector3.Dot(grid.forward, Vector3.forward), Is.GreaterThan(.99f));
                for (int other = i + 1; other < PrototypeCircuitBuilder.GridCount; other++)
                    Assert.That(Vector3.Distance(grid.position,
                        PrototypeCircuitBuilder.GetGridPose(other).position), Is.GreaterThan(2.5f));
            }

            Vector3[] expectedDirections =
            {
                Vector3.forward, Vector3.forward, Vector3.right, Vector3.right,
                Vector3.back, Vector3.back, Vector3.left, Vector3.left
            };
            for (int i = 0; i < PrototypeCircuitBuilder.OrderedCheckpointCount; i++)
            {
                Pose checkpoint = PrototypeCircuitBuilder.GetCheckpointPose(i);
                Assert.That(Vector3.Dot(checkpoint.forward, expectedDirections[i]), Is.GreaterThan(.99f),
                    $"Checkpoint {i} must face along the clockwise route.");
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => PrototypeCircuitBuilder.GetGridPose(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => PrototypeCircuitBuilder.GetCheckpointPose(8));
        }

        [TestCase(0f, "00:00.00")]
        [TestCase(65.25f, "01:05.25")]
        [TestCase(-2f, "00:00.00")]
        public void RaceClockFormattingIsStable(float seconds, string expected)
        {
            Assert.That(Stage3RacePresentation.FormatTime(seconds), Is.EqualTo(expected));
        }
    }
}
