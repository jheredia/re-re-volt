using System;

namespace Core
{
    /// <summary>Stable display order is ascending occupied player slot, not device ID.</summary>
    public static class LocalPlayerRules
    {
        public const int MaximumPlayers = 4;

        public static Viewport GetViewport(int playerCount, int displayIndex)
        {
            if (playerCount < 1 || playerCount > MaximumPlayers)
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            if (displayIndex < 0 || displayIndex >= playerCount)
                throw new ArgumentOutOfRangeException(nameof(displayIndex));

            if (playerCount == 1) return new Viewport(0, 0, 1, 1);
            if (playerCount == 2) return new Viewport(0, displayIndex == 0 ? .5f : 0, 1, .5f);
            if (playerCount == 3 && displayIndex == 0) return new Viewport(0, .5f, 1, .5f);
            int cell = playerCount == 3 ? displayIndex + 1 : displayIndex;
            return new Viewport(cell % 2 * .5f, cell < 2 ? .5f : 0, .5f, .5f);
        }
    }

    public readonly struct Viewport
    {
        public Viewport(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
    }
}
