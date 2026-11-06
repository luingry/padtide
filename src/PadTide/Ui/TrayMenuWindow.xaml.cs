using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PadTide.Native;

namespace PadTide.Ui
{
    /// <summary>The notification-area menu, drawn with the app's own components.</summary>
    public partial class TrayMenuWindow : Window
    {
        private readonly App _app;
        private bool _closing;

        internal TrayMenuWindow(App app)
        {
            _app = app;
            InitializeComponent();
            Deactivated += (s, e) => CloseOnce();
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) CloseOnce();
            };
            Closing += (s, e) => _closing = true;
        }

        /// <summary>Shows the menu so that its corner sits by the cursor, given in physical pixels.</summary>
        internal void ShowAt(int cursorX, int cursorY)
        {
            Opacity = 0;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -10000;
            Top = -10000;
            Show();

            var scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var cursor = scale.Transform(new Point(cursorX, cursorY));
            var work = SystemParameters.WorkArea;
            Left = Math.Max(work.Left, Math.Min(cursor.X - ActualWidth + 18, work.Right - ActualWidth));
            Top = Math.Max(work.Top, Math.Min(cursor.Y - ActualHeight + 18, work.Bottom - ActualHeight));
            Opacity = 1;

            Activate();
            NativeMethods.SetForegroundWindow(new WindowInteropHelper(this).Handle);
        }

        private void OnSettingsClicked(object sender, RoutedEventArgs e)
        {
            CloseOnce();
            _app.OpenSettings();
        }

        private void OnQuitClicked(object sender, RoutedEventArgs e) => _app.Quit();

        private void CloseOnce()
        {
            if (_closing) return;
            _closing = true;
            Close();
        }
    }
}
