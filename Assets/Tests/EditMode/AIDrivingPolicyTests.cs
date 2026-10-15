using AI;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public sealed class AIDrivingPolicyTests
    {
        [Test]
        public void StraightGuidanceUsesNeutralForwardCommand()
        {
            AIDrivingDecision decision = AIDrivingPolicy.Evaluate(new Vector3(0f, 0f, 10f), 12f);

            Assert.That(decision.Throttle, Is.EqualTo(.92f).Within(.001f));
            Assert.That(decision.Steer, Is.Zero.Within(.001f));
            Assert.That(decision.Brake, Is.Zero);
        }

        [TestCase(-10f, -1f)]
        [TestCase(10f, 1f)]
        public void SideGuidanceSteersTowardTarget(float targetX, float expectedSign)
        {
            AIDrivingDecision decision = AIDrivingPolicy.Evaluate(new Vector3(targetX, 0f, 1f), 30f);

            Assert.That(Mathf.Sign(decision.Steer), Is.EqualTo(expectedSign));
            Assert.That(decision.Brake, Is.GreaterThan(0f));
            Assert.That(decision.Throttle, Is.LessThan(.6f));
        }

        [Test]
        public void BehindGuidanceRemainsBoundedAndTurnsDeterministically()
        {
            AIDrivingDecision decision = AIDrivingPolicy.Evaluate(new Vector3(.1f, 0f, -5f), 40f);

            Assert.That(decision.Throttle, Is.InRange(-1f, 1f));
            Assert.That(decision.Steer, Is.InRange(-1f, 1f));
            Assert.That(decision.Brake, Is.InRange(0f, 1f));
            Assert.That(decision.Steer, Is.GreaterThan(0f));
        }

        [Test]
        public void PerpendicularRouteLegsUseLaneAlignedCornerInsteadOfDiagonalBarrierCut()
        {
            bool found = AIDrivingPolicy.TryCalculateTurnPoint(
                new Vector3(-8f, .2f, -14.35f), Vector3.left,
                new Vector3(-15.65f, .2f, 0f), Vector3.forward,
                out Vector3 turnPoint);

            Assert.That(found, Is.True);
            Assert.That(Vector3.Distance(turnPoint, new Vector3(-15.65f, .2f, -14.35f)),
                Is.LessThan(.0001f));
        }

        [Test]
        public void ParallelRouteLegsContinueDirectlyToCheckpoint()
        {
            bool found = AIDrivingPolicy.TryCalculateTurnPoint(
                new Vector3(-15.65f, .2f, 0f), Vector3.forward,
                new Vector3(-15.65f, .2f, 10f), Vector3.forward,
                out _);

            Assert.That(found, Is.False);
        }

        [TestCase(-16.4f, -.65f)]
        [TestCase(-13.6f, .65f)]
        public void SpawnSideSelectsMatchingLaneWithoutCrossingTheGrid(float gridX, float expectedOffset)
        {
            float laneOffset = AIDrivingPolicy.CalculateSpawnLaneOffset(
                new Vector3(gridX, .76f, -7f),
                new Vector3(-15f, 1.2f, 0f),
                Vector3.right);

            Assert.That(laneOffset, Is.EqualTo(expectedOffset));
        }

        [Test]
        public void UpcomingPerpendicularTurnCapsApproachAndBrakesBeforeTheCheckpoint()
        {
            AIDrivingDecision decision = AIDrivingPolicy.Evaluate(Vector3.forward * 12f, 38f, true);

            Assert.That(decision.Throttle, Is.EqualTo(.52f));
            Assert.That(decision.Steer, Is.Zero);
            Assert.That(decision.Brake, Is.EqualTo(.65f));
        }
    }
}
