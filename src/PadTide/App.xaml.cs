using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PadTide.Core;
using PadTide.Device;
using PadTide.Native;
using PadTide.Shell;
using PadTide.Ui;

namespace PadTide
{
    public partial class App : Application
    {
        private const string HostTitle = "PadTide.Host";
        private const int ShowSettingsMessage = NativeMethods.WmApp + 2;
        private const int HidNotificationType = 5;
        private const int FirstUpdateCheckDelaySeconds = 30;

        private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PadTide", "settings.ini");
        private Mutex? _instance;
        private HwndSource? _host;
        private IntPtr _deviceNotification;
        private int _taskbarCreated;
        private TrayIcon? _tray;
        private ControllerMonitor? _monitor;
        private WidgetWindow? _widget;
        private SettingsWindow? _settingsWindow;
        private TrayMenuWindow? _menu;
        private DispatcherTimer? _saveTimer;
        private bool _openSettingsIfOff;
        private bool _firstState = true;
        private readonly Updater _updater = new Updater();
        private DispatcherTimer? _updateTimer;
        private UpdateNoticeWindow? _updateNotice;

        internal AppSettings Settings { get; private set; } = new AppSettings();
        internal BatteryReport? Battery { get; private set; }
        internal static string Version => Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // A tiny layered window gains nothing from a GPU device, which costs tens of MB.
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            DispatcherUnhandledException += (s, args) => WriteCrashLog(args.Exception);

            if (e.Args.Length >= 2 && e.Args[0] == "--export-icon")
            {
                Artwork.WriteIconFile(e.Args[1]);
                Shutdown();
                return;
            }
            if (e.Args.Length >= 2 && e.Args[0] == "--snapshot")
            {
                Snapshot.Write(e.Args[1]);
                Shutdown();
                return;
            }

            _instance = new Mutex(true, @"Local\PadTide.SingleInstance", out var firstInstance);
            if (!firstInstance)
            {
                // Launching again is the way back to settings while the controller is off.
                var running = NativeMethods.FindWindow(null, HostTitle);
                if (running != IntPtr.Zero) NativeMethods.PostMessage(running, ShowSettingsMessage, IntPtr.Zero, IntPtr.Zero);
                _instance.Dispose();
                _instance = null;
                Shutdown();
                return;
            }

            Settings = AppSettings.Load(_settingsPath);
            _openSettingsIfOff = !e.Args.Contains(StartupRegistration.StartupArgument);
            CreateHost();

            _widget = new WidgetWindow();
            _widget.ApplyLook(Settings.Size, Settings.OpacityPercent);
            _widget.PlaceAt(Settings.Left, Settings.Top);
            _widget.SettingsRequested += OpenSettings;
            _widget.Moved += RememberPosition;

            _monitor = new ControllerMonitor();
            _monitor.Changed += report => Dispatcher.BeginInvoke(new Action(() => ApplyBattery(report)));
            _monitor.Start();

            // Waits out the network coming up at boot, then wakes rarely; a day between real checks.
            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(FirstUpdateCheckDelaySeconds) };
            _updateTimer.Tick += async (s, args) =>
            {
                _updateTimer.Interval = UpdatePolicy.MinimumCheckInterval;
                if (UpdatePolicy.IsDue(Settings.LastUpdateCheckUtc, DateTime.UtcNow)) await CheckForUpdatesAsync();
            };
            _updateTimer.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _updateTimer?.Stop();
            _updater.Dispose();
            _monitor?.Dispose();
            _tray?.Dispose();
            if (_deviceNotification != IntPtr.Zero) NativeMethods.UnregisterDeviceNotification(_deviceNotification);
            _host?.Dispose();
            if (_saveTimer != null && _saveTimer.IsEnabled) SaveSettings();
            _instance?.ReleaseMutex();
            _instance?.Dispose();
            base.OnExit(e);
        }

        internal void ApplyLook()
        {
            _widget?.ApplyLook(Settings.Size, Settings.OpacityPercent);
            if (_saveTimer == null)
            {
                _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _saveTimer.Tick += (s, e) => SaveSettings();
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        internal void OpenSettings()
        {
            _menu?.Close();
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow(this);
                _settingsWindow.Closed += (s, e) =>
                {
                    _settingsWindow = null;
                    ReleaseMemory();
                };
                _settingsWindow.Show();
            }
            _settingsWindow.Activate();
            NativeMethods.SetForegroundWindow(new WindowInteropHelper(_settingsWindow).Handle);
        }

        internal void Quit() => Shutdown();

        internal async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            Settings.LastUpdateCheckUtc = DateTime.UtcNow;
            SaveSettings();
            try
            {
                var offer = await _updater.CheckAsync(Version);
                if (offer == null) return UpdateCheckResult.UpToDate;
                ShowUpdateNotice(offer);
                return UpdateCheckResult.Available;
            }
            catch (Exception exception) when (exception is System.Net.Http.HttpRequestException || exception is TaskCanceledException || exception is IOException || exception is System.Runtime.Serialization.SerializationException)
            {
                WriteCrashLog(exception);
                return UpdateCheckResult.Failed;
            }
        }

        private void ShowUpdateNotice(UpdateOffer offer)
        {
            if (_updateNotice != null)
            {
                _updateNotice.Activate();
                return;
            }
            _updateNotice = new UpdateNoticeWindow(offer, _updater, Quit);
            _updateNotice.Closed += (s, e) =>
            {
                _updateNotice = null;
                ReleaseMemory();
            };
            _updateNotice.Show();
        }

        private void CreateHost()
        {
            _taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");
            _host = new HwndSource(new HwndSourceParameters(HostTitle)
            {
                WindowStyle = NativeMethods.WsPopup,
                ExtendedWindowStyle = NativeMethods.WsExToolWindow,
                Width = 0,
                Height = 0,
            });
            _host.AddHook(HostProc);
            _tray = new TrayIcon(_host.Handle);

            NativeMethods.HidD_GetHidGuid(out var hidGuid);
            var filter = new NativeMethods.DevBroadcastDeviceInterface
            {
                Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DevBroadcastDeviceInterface>(),
                DeviceType = HidNotificationType,
                ClassGuid = hidGuid,
            };
            _deviceNotification = NativeMethods.RegisterDeviceNotification(_host.Handle, ref filter, 0);
        }

        private IntPtr HostProc(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == NativeMethods.WmClose)
            {
                // The installer asks the running app to leave before replacing it.
                Quit();
                handled = true;
            }
            else if (message == NativeMethods.WmDeviceChange)
            {
                var change = wParam.ToInt64();
                if (change == NativeMethods.DbtDeviceArrival || change == NativeMethods.DbtDeviceRemoveComplete) _monitor?.DeviceChanged();
            }
            else if (message == TrayIcon.CallbackMessage)
            {
                var trayEvent = lParam.ToInt64() & 0xFFFF;
                if (trayEvent == NativeMethods.NinSelect || trayEvent == NativeMethods.NinKeySelect) OpenSettings();
                else if (trayEvent == NativeMethods.WmContextMenu) ShowTrayMenu();
                handled = true;
            }
            else if (message == ShowSettingsMessage)
            {
                OpenSettings();
                handled = true;
            }
            else if (message == _taskbarCreated)
            {
                _tray?.Restore();
            }
            return IntPtr.Zero;
        }

        private void ApplyBattery(BatteryReport? report)
        {
            Battery = report;
            if (report is BatteryReport battery)
            {
                _widget!.SetPercent(battery.Percent);
                _widget.Reveal();
                _widget.KeepOnTop();
                _tray!.Show(Artwork.CreateTrayIconHandle(battery.Percent), "Bateria do controle: " + battery.Percent + "%");
            }
            else
            {
                _widget!.Hide();
                _tray!.Hide();
                if (_firstState && _openSettingsIfOff) OpenSettings();
            }
            var wasFirstState = _firstState;
            _firstState = false;
            _settingsWindow?.ShowBattery(report);
            if (wasFirstState) ReleaseMemory();
        }

        /// <summary>Resident apps should not sit on what a closed screen used.</summary>
        private void ReleaseMemory()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                NativeMethods.SetProcessWorkingSetSize(NativeMethods.GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
            }), DispatcherPriority.ApplicationIdle);
        }

        private void ShowTrayMenu()
        {
            _menu?.Close();
            NativeMethods.GetCursorPos(out var cursor);
            _menu = new TrayMenuWindow(this);
            _menu.Closed += (s, e) => _menu = null;
            _menu.ShowAt(cursor.X, cursor.Y);
        }

        private void RememberPosition()
        {
            Settings.Left = _widget!.Left;
            Settings.Top = _widget.Top;
            SaveSettings();
        }

        private void SaveSettings()
        {
            _saveTimer?.Stop();
            try
            {
                Settings.Save(_settingsPath);
            }
            catch (IOException exception)
            {
                WriteCrashLog(exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                WriteCrashLog(exception);
            }
        }

        private void WriteCrashLog(Exception exception)
        {
            try
            {
                File.AppendAllText(Path.ChangeExtension(_settingsPath, ".log"), DateTime.Now.ToString("s") + " " + exception + Environment.NewLine);
            }
            catch (IOException)
            {
                // Logging must never take the app down.
            }
            catch (UnauthorizedAccessException)
            {
                // Logging must never take the app down.
            }
        }
    }
}
