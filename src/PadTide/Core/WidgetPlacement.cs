using System;

namespace PadTide.Core
{
    /// <summary>Keeps a window whole inside a monitor's frame.</summary>
    public static class WidgetPlacement
    {
        public static double Clamp(double position, double size, double frameStart, double frameEnd) =>
            Math.Max(frameStart, Math.Min(position, frameEnd - size));
    }
}
