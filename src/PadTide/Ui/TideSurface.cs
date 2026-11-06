using System;
using System.Windows;
using System.Windows.Media;

namespace PadTide.Ui
{
    /// <summary>
    /// The signature surface: the panel is the battery. A soft-edged tide rises to the real charge,
    /// separating areas by tone instead of borders. It only redraws when the level changes or while charging.
    /// </summary>
    public sealed class TideSurface : FrameworkElement
    {
        public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
            nameof(Level), typeof(double), typeof(TideSurface), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsLowProperty = DependencyProperty.Register(
            nameof(IsLow), typeof(bool), typeof(TideSurface), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
            nameof(CornerRadius), typeof(double), typeof(TideSurface), new FrameworkPropertyMetadata(26d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
            nameof(Phase), typeof(double), typeof(TideSurface), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Moves the swell; only animated while charging.</summary>
        public double Phase
        {
            get => (double)GetValue(PhaseProperty);
            set => SetValue(PhaseProperty, value);
        }

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
            var tide = Artwork.Wave(0, width, y, Math.Min(height * 0.022, 9), Phase + Math.PI, height);

            dc.PushClip(panel);
            dc.DrawGeometry((Brush)FindResource(IsLow ? "Brush.Tide.Low" : "Brush.Tide"), null, tide);
            dc.Pop();
        }
    }
}
