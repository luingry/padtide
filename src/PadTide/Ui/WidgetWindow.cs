using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PadTide.Core;
using PadTide.Native;

namespace PadTide.Ui
{
    /// <summary>The floating badge. Never takes focus: drag to move, double-click for settings.</summary>
    internal sealed class WidgetWindow : Window
    {
        private const double EdgeMargin = 24;
        private readonly BadgeView _badge = new BadgeView();
        private double _opacity = 0.5;
        private bool _waving;
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

        /// <summary>Shows a percentage, or the charging bolt when the number is null, over a tide at the given level.</summary>
        public void SetLevel(int? number, int? tide, bool charging, string accessibleName)
        {
            _badge.Number = number;
            _badge.Tide = tide;
            SetWaving(charging && tide.HasValue);
            AutomationProperties.SetName(this, accessibleName);
        }

        public void Conceal()
        {
            SetWaving(false);
            Hide();
        }

        private void SetWaving(bool waving)
        {
            if (waving && !SystemParameters.ClientAreaAnimation) waving = false;
            if (waving == _waving) return;
            _waving = waving;
            _badge.BeginAnimation(BadgeView.PhaseProperty, waving ? WaveMotion.Create() : null);
        }

        public void ApplyLook(int size, int opacityPercent)
        {
            Width = size;
            Height = size * Artwork.BadgeAspect;
            _opacity = opacityPercent / 100d;
            _badge.WindowOpacity = _opacity;
            BeginAnimation(OpacityProperty, null);
            Opacity = _opacity;
            KeepInsideMonitor();
        }

        public void PlaceAt(double? left, double? top)
        {
            Left = left ?? SystemParameters.WorkArea.Right - Width - EdgeMargin;
            Top = top ?? SystemParameters.WorkArea.Bottom - Height - EdgeMargin;

            // A monitor may be gone since the last session: this lands on the nearest one.
            KeepInsideMonitor();
        }

        public void Reveal()
        {
            if (IsVisible) return;
            Show();
            KeepInsideMonitor();
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

            // The monitor under the cursor sets the limit, so the widget can still cross to another screen.
            NativeMethods.GetCursorPos(out var cursor);
            var frame = MonitorFrame(cursor);
            Left = WidgetPlacement.Clamp(_pressLeft + offset.X, Width, frame.Left, frame.Right);
            Top = WidgetPlacement.Clamp(_pressTop + offset.Y, Height, frame.Top, frame.Bottom);
        }

        private void KeepInsideMonitor()
        {
            if (double.IsNaN(Left) || double.IsNaN(Top)) return;
            var center = ToDevice().Transform(new Point(Left + Width / 2, Top + Height / 2));
            var frame = MonitorFrame(new NativeMethods.Point { X = (int)center.X, Y = (int)center.Y });
            Left = WidgetPlacement.Clamp(Left, Width, frame.Left, frame.Right);
            Top = WidgetPlacement.Clamp(Top, Height, frame.Top, frame.Bottom);
        }

        private Rect MonitorFrame(NativeMethods.Point devicePoint)
        {
            var monitor = NativeMethods.MonitorFromPoint(devicePoint, NativeMethods.MonitorDefaultToNearest);
            var info = new NativeMethods.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfo>() };
            NativeMethods.GetMonitorInfo(monitor, ref info);
            var fromDevice = ToDevice();
            fromDevice.Invert();
            return new Rect(
                fromDevice.Transform(new Point(info.Frame.Left, info.Frame.Top)),
                fromDevice.Transform(new Point(info.Frame.Right, info.Frame.Bottom)));
        }

        private Matrix ToDevice() => PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;

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
            public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
                nameof(Phase), typeof(double), typeof(BadgeView), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

            private int? _number = 0;
            private int? _tide = 0;
            private double _windowOpacity = 1;

            public double Phase
            {
                get => (double)GetValue(PhaseProperty);
                set => SetValue(PhaseProperty, value);
            }

            public int? Number
            {
                get => _number;
                set
                {
                    if (_number == value) return;
                    _number = value;
                    InvalidateVisual();
                }
            }

            public int? Tide
            {
                get => _tide;
                set
                {
                    if (_tide == value) return;
                    _tide = value;
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

            protected override void OnRender(DrawingContext dc) => Artwork.DrawBadge(dc, ActualWidth, _number, _tide, Phase, _windowOpacity);
        }
    }
}
