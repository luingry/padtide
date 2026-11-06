using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PadTide.Native;

namespace PadTide.Ui
{
    /// <summary>The floating badge. Never takes focus: drag to move, double-click for settings.</summary>
    internal sealed class WidgetWindow : Window
    {
        private const double EdgeMargin = 24;
        private readonly BadgeView _badge = new BadgeView();
        private double _opacity = 0.5;

        public WidgetWindow()
        {
            Title = "PadTide";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Content = _badge;
            SourceInitialized += (s, e) => NativeMethods.MakeNonActivatingToolWindow(new WindowInteropHelper(this).Handle);
            MouseLeftButtonDown += OnPress;
        }

        public event Action? SettingsRequested;
        public event Action? Moved;

        public void SetPercent(int percent)
        {
            _badge.Percent = percent;
            AutomationProperties.SetName(this, "Bateria do controle: " + percent + "%");
        }

        public void ApplyLook(int size, int opacityPercent)
        {
            Width = size;
            Height = size * Artwork.BadgeAspect;
            _opacity = opacityPercent / 100d;
            BeginAnimation(OpacityProperty, null);
            Opacity = _opacity;
        }

        public void PlaceAt(double? left, double? top)
        {
            var defaultLeft = SystemParameters.WorkArea.Right - Width - EdgeMargin;
            var defaultTop = SystemParameters.WorkArea.Bottom - Height - EdgeMargin;
            var x = left ?? defaultLeft;
            var y = top ?? defaultTop;

            // A monitor may be gone since the last session: never restore off-screen.
            var offScreen = x + Width < SystemParameters.VirtualScreenLeft || x > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth ||
                y + Height < SystemParameters.VirtualScreenTop || y > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
            Left = offScreen ? defaultLeft : x;
            Top = offScreen ? defaultTop : y;
        }

        public void Reveal()
        {
            if (IsVisible) return;
            Show();
            if (SystemParameters.ClientAreaAnimation)
            {
                var fade = new DoubleAnimation(0, _opacity, TimeSpan.FromMilliseconds(220)) { FillBehavior = FillBehavior.Stop };
                BeginAnimation(OpacityProperty, fade);
            }
        }

        /// <summary>Other topmost windows can bury the badge; reassert without stealing focus.</summary>
        public void KeepOnTop()
        {
            if (!IsVisible) return;
            NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, NativeMethods.HwndTopmost, 0, 0, 0, 0,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
        }

        private void OnPress(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                SettingsRequested?.Invoke();
                return;
            }
            try
            {
                DragMove();
                Moved?.Invoke();
            }
            catch (InvalidOperationException)
            {
                // The button was already released.
            }
        }

        private sealed class BadgeView : FrameworkElement
        {
            private int _percent;

            public int Percent
            {
                get => _percent;
                set
                {
                    if (_percent == value) return;
                    _percent = value;
                    InvalidateVisual();
                }
            }

            protected override void OnRender(DrawingContext dc) => Artwork.DrawBadge(dc, ActualWidth, _percent);
        }
    }
}
