using System;
using System.Windows.Media.Animation;

namespace PadTide.Ui
{
    /// <summary>The travelling swell shown while charging. Capped at a low frame rate: a slow wave needs no more.</summary>
    internal static class WaveMotion
    {
        public static DoubleAnimation Create()
        {
            var motion = new DoubleAnimation(0, -2 * Math.PI, TimeSpan.FromSeconds(3.2)) { RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(motion, 12);
            return motion;
        }
    }
}