using System;
using System.Windows;
using System.Windows.Media;

namespace PadTide.Ui
{
    /// <summary>
    /// The signature surface: the panel is the battery. A soft-edged tide rises to the real charge,
    /// separating areas by tone instead of borders. It only redraws when the level changes.
    /// </summary>
    public sealed class TideSurface : FrameworkElement
    {
        public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
            nameof(Level), typeof(double), typeof(TideSurface), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsLowProperty = DependencyProperty.Register(
            nameof(IsLow), typeof(bool), typeof(TideSurface), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
            nameof(CornerRadius), typeof(double), typeof(TideSurface), new FrameworkPropertyMetadata(26d, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Charge from 0 to 1.</summary>
        public double Level
        {
            get => (double)GetValue(LevelProperty);
            set => SetValue(LevelProperty, value);
        }

        public bool IsLow
        {
            get => (bool)GetValue(IsLowProperty);
            set => SetValue(IsLowProperty, value);
        }

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width = ActualWidth, height = ActualHeight;
            var panel = new RectangleGeometry(new Rect(0, 0, width, height), CornerRadius, CornerRadius);
            dc.DrawGeometry((Brush)FindResource("Brush.Surface"), null, panel);

            var level = Math.Max(0, Math.Min(1, Level));
            if (level <= 0) return;

            // A full tide has no crest: the swell would leave a dark sliver at the top edge.
            if (level >= 1)
            {
                dc.DrawGeometry((Brush)FindResource(IsLow ? "Brush.Tide.Low" : "Brush.Tide"), null, panel);
                return;
            }

            var y = height * (1 - level);
            var swell = Math.Min(height * 0.022, 9);
            var tide = new StreamGeometry();
            using (var context = tide.Open())
            {
                context.BeginFigure(new Point(0, y), true, true);
                context.BezierTo(new Point(width * 0.24, y - swell * 1.6), new Point(width * 0.40, y - swell * 1.6), new Point(width * 0.58, y), true, true);
                context.BezierTo(new Point(width * 0.74, y + swell * 1.4), new Point(width * 0.88, y + swell * 1.4), new Point(width, y - swell * 0.3), true, true);
                context.LineTo(new Point(width, height), false, false);
                context.LineTo(new Point(0, height), false, false);
            }
            tide.Freeze();

            dc.PushClip(panel);
            dc.DrawGeometry((Brush)FindResource(IsLow ? "Brush.Tide.Low" : "Brush.Tide"), null, tide);
            dc.Pop();
        }
    }
}
