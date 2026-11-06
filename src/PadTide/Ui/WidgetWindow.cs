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
        private bool _pressed;
        private bool _dragging;
        private Point _pressCursor;
        private double _pressLeft;
        private double _pressTop;

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
            MouseMove += OnDrag;
            MouseLeftButtonUp += OnRelease;
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
            _badge.WindowOpacity = _opacity;
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

        // DragMove's modal loop swallows the second press of a double-click, so the drag is done by hand.
        private void OnPress(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _pressed = false;
                SettingsRequested?.Invoke();
                return;
            }
            _pressed = true;
            _dragging = false;
            _pressCursor = CursorPosition();
            _pressLeft = Left;
            _pressTop = Top;
            CaptureMouse();
        }

        private void OnDrag(object sender, MouseEventArgs e)
        {
            if (!_pressed) return;
            var offset = CursorPosition() - _pressCursor;
            if (!_dragging && Math.Abs(offset.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(offset.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _dragging = true;
            Left = _pressLeft + offset.X;
            Top = _pressTop + offset.Y;
        }

        private void OnRelease(object sender, MouseButtonEventArgs e)
        {
            if (!_pressed) return;
            _pressed = false;
            ReleaseMouseCapture();
            if (_dragging) Moved?.Invoke();
        }

        private Point CursorPosition()
        {
            NativeMethods.GetCursorPos(out var cursor);
            var fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            return fromDevice.Transform(new Point(cursor.X, cursor.Y));
        }

        private sealed class BadgeView : FrameworkElement
        {
            private int _percent;
            private double _windowOpacity = 1;

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

            public double WindowOpacity
            {
                get => _windowOpacity;
                set
                {
                    _windowOpacity = value;
                    InvalidateVisual();
                }
            }

            protected override void OnRender(DrawingContext dc) => Artwork.DrawBadge(dc, ActualWidth, _percent, _windowOpacity);
        }
    }
}
