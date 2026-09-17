using System;
using Core;
using NUnit.Framework;

namespace Tests.EditMode
{
    public sealed class LocalPlayerRulesTests
    {
        [TestCase(1, 0, 0f, 0f, 1f, 1f)]
        [TestCase(2, 0, 0f, .5f, 1f, .5f)]
        [TestCase(2, 1, 0f, 0f, 1f, .5f)]
        [TestCase(3, 0, 0f, .5f, 1f, .5f)]
        [TestCase(3, 1, 0f, 0f, .5f, .5f)]
        [TestCase(3, 2, .5f, 0f, .5f, .5f)]
        [TestCase(4, 0, 0f, .5f, .5f, .5f)]
        [TestCase(4, 1, .5f, .5f, .5f, .5f)]
        [TestCase(4, 2, 0f, 0f, .5f, .5f)]
        [TestCase(4, 3, .5f, 0f, .5f, .5f)]
        public void ProducesExactViewport(int count, int index, float x, float y, float width, float height)
        {
            Viewport viewport = LocalPlayerRules.GetViewport(count, index);
            Assert.That(new[] { viewport.X, viewport.Y, viewport.Width, viewport.Height }, Is.EqualTo(new[] { x, y, width, height }));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ViewportsTileScreenWithoutGapsOrOverlaps(int count)
        {
            float area = 0f;
            for (int i = 0; i < count; i++)
            {
                Viewport a = LocalPlayerRules.GetViewport(count, i);
                area += a.Width * a.Height;
                for (int j = 0; j < i; j++)
                {
                    Viewport b = LocalPlayerRules.GetViewport(count, j);
                    float width = Math.Max(0f, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X));
                    float height = Math.Max(0f, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
                    Assert.That(width * height, Is.Zero);
                }
            }
            Assert.That(area, Is.EqualTo(1f));
        }

        [TestCase(0, 0)] [TestCase(5, 0)] [TestCase(2, -1)] [TestCase(2, 2)]
        public void RejectsInvalidCountsOrIndices(int count, int index)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LocalPlayerRules.GetViewport(count, index));
        }
    }
}
