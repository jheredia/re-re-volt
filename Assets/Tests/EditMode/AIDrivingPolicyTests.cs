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
    }
}
