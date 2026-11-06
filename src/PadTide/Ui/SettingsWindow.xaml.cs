using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using PadTide.Core;
using PadTide.Shell;

namespace PadTide.Ui
{
    public partial class SettingsWindow : Window
    {
        private readonly App _app;

        internal SettingsWindow(App app)
        {
            _app = app;
            InitializeComponent();

            BrandIcon.Source = Artwork.AppIconImage();
            Icon = Artwork.Render(32, 32, dc => Artwork.DrawAppIcon(dc, 32));
            VersionText.Text = "v" + App.Version;
            OpacitySlider.Value = _app.Settings.OpacityPercent;
            SizeSlider.Value = _app.Settings.Size;
            StartupToggle.IsChecked = StartupRegistration.IsEnabled;
            StartupToggle.Checked += OnStartupChanged;
            StartupToggle.Unchecked += OnStartupChanged;
            UpdateValues();
            ShowBattery(_app.Battery, animate: false);

            MouseLeftButtonDown += (s, e) =>
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    // The button was already released.
                }
            };
        }

        internal void ShowBattery(BatteryReport? report) => ShowBattery(report, animate: true);

        internal void ShowBatteryForSnapshot(BatteryReport? report) => ShowBattery(report, animate: false);

        private void ShowBattery(BatteryReport? report, bool animate)
        {
            var level = report?.Percent / 100d ?? 0;
            Tide.IsLow = report?.IsLow ?? false;
            BatteryText.Text = report is BatteryReport battery
                ? "Bateria em " + battery.Percent.ToString(CultureInfo.InvariantCulture) + "%"
                : "Controle desligado";

            if (animate && SystemParameters.ClientAreaAnimation)
            {
                var rise = new DoubleAnimation(level, TimeSpan.FromMilliseconds(600)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                Tide.BeginAnimation(TideSurface.LevelProperty, rise);
            }
            else
            {
                Tide.BeginAnimation(TideSurface.LevelProperty, null);
                Tide.Level = level;
            }
        }

        private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;
            _app.Settings.OpacityPercent = (int)Math.Round(e.NewValue);
            UpdateValues();
            _app.ApplyLook();
        }

        private void OnSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;
            _app.Settings.Size = (int)Math.Round(e.NewValue);
            UpdateValues();
            _app.ApplyLook();
        }

        private void OnStartupChanged(object sender, RoutedEventArgs e) => StartupRegistration.Set(StartupToggle.IsChecked == true);

        private async void OnUpdateClicked(object sender, RoutedEventArgs e)
        {
            UpdateButton.IsEnabled = false;
            UpdateButton.Content = "Verificando…";
            UpdateButton.Content = await _app.CheckForUpdatesAsync() switch
            {
                UpdateCheckResult.UpToDate => "Você está em dia",
                UpdateCheckResult.Available => "Atualização disponível",
                _ => "Sem conexão. Tentar de novo",
            };
            UpdateButton.IsEnabled = true;
        }

        private void OnQuitClicked(object sender, RoutedEventArgs e) => _app.Quit();

        private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

        private void UpdateValues()
        {
            if (OpacityValue == null || SizeValue == null) return;
            OpacityValue.Text = _app.Settings.OpacityPercent.ToString(CultureInfo.InvariantCulture) + "%";
            SizeValue.Text = _app.Settings.Size.ToString(CultureInfo.InvariantCulture) + " px";
        }
    }
}
