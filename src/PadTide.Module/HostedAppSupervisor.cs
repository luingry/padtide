using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SwissTechKnife.Sdk;

namespace PadTide.Module
{
    public enum HostedAppState
    {
        Stopped,
        Starting,
        Running,

        /// <summary>A standalone PadTide owns the single-instance mutex; the module waits for it to exit.</summary>
        RunningOutside,

        /// <summary>The app exited repeatedly; restarts stopped until the user opens it again.</summary>
        Failed,
    }

    /// <summary>
    /// Runs PadTide in its own process (--hosted). Restarts it after a crash (bounded) and stops it with the
    /// module. Thread-safe; <see cref="StateChanged"/> is raised on arbitrary threads.
    /// </summary>
    internal sealed class HostedAppSupervisor : IDisposable
    {
        // These must match PadTide's App and HostedOptions.
        private const string MutexName = @"Local\PadTide.SingleInstance";
        private const string HostWindowTitle = "PadTide.Host";
        private const int ShowSettingsMessage = 0x8000 + 2;
        private const int CloseMessage = 0x0010;
        private const int AlreadyRunningExitCode = 3;

        private const int MaxCrashesPerWindow = 3;
        private const int ExitTimeoutMilliseconds = 4000;
        private const int AllowAnyProcess = -1;
        private static readonly TimeSpan CrashWindow = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan OutsidePollInterval = TimeSpan.FromSeconds(5);

        private readonly object _gate = new object();
        private readonly string _exePath;
        private readonly string _dataDirectory;
        private readonly IModuleLog _log;
        private readonly Queue<DateTime> _crashes = new Queue<DateTime>();
        private Process? _process;
        private Timer? _retry;
        private bool _stopping;
        private bool _disposed;
        private HostedAppState _state = HostedAppState.Stopped;

        public HostedAppSupervisor(string exePath, string dataDirectory, IModuleLog log)
        {
            _exePath = exePath;
            _dataDirectory = dataDirectory;
            _log = log;
        }

        public event Action<HostedAppState>? StateChanged;

        public HostedAppState State
        {
            get
            {
                lock (_gate) return _state;
            }
        }

        /// <summary>Launches the app on a pool thread; returns at once.</summary>
        public void Start()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _stopping = false;
            }
            ThreadPool.QueueUserWorkItem(_ => Launch(show: false));
        }

        /// <summary>Opens the settings of the running PadTide (hosted or standalone), starting it when nothing runs.</summary>
        public void OpenSettings()
        {
            lock (_gate)
            {
                if (_disposed || _stopping) return;
            }

            // The host is in the foreground (the user clicked), so PadTide may take focus.
            AllowSetForegroundWindow(AllowAnyProcess);
            if (PostToApp(ShowSettingsMessage)) return;

            lock (_gate) _crashes.Clear();
            ThreadPool.QueueUserWorkItem(_ => Launch(show: true));
        }

        /// <summary>Asks the app to save and exit; kills it after a short timeout. Blocks at most about 5 s.</summary>
        public void Stop()
        {
            Process? process;
            lock (_gate)
            {
                _stopping = true;
                _retry?.Dispose();
                _retry = null;
                process = _process;
            }

            if (process != null)
            {
                try
                {
                    PostToApp(CloseMessage);
                    if (!process.WaitForExit(ExitTimeoutMilliseconds))
                    {
                        _log.Warn("PadTide did not exit in time; terminating it.");
                        process.Kill();
                        process.WaitForExit(1000);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Already exited.
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Already exiting.
                }
            }

            SetState(HostedAppState.Stopped);
        }

        public void Dispose()
        {
            Stop();
            lock (_gate) _disposed = true;
        }

        private void Launch(bool show)
        {
            lock (_gate)
            {
                if (_disposed || _stopping || _process != null) return;
                _retry?.Dispose();
                _retry = null;
            }

            // No hosted process is ours, so a held mutex means a standalone PadTide: never poll the controller twice.
            if (IsStandaloneRunning())
            {
                EnterRunningOutside();
                return;
            }

            if (!File.Exists(_exePath))
            {
                _log.Error("PadTide executable not found: " + _exePath);
                SetState(HostedAppState.Failed);
                return;
            }

            SetState(HostedAppState.Starting);
            try
            {
                using var current = Process.GetCurrentProcess();
                var arguments = "--hosted --parent " + current.Id + " --data \"" + _dataDirectory.TrimEnd('\\') + "\"";
                if (show)
                {
                    arguments += " --show";
                    AllowSetForegroundWindow(AllowAnyProcess);
                }
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo(_exePath, arguments)
                    {
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(_exePath),
                    },
                    EnableRaisingEvents = true,
                };
                process.Exited += OnExited;
                lock (_gate)
                {
                    if (_stopping || _disposed)
                    {
                        process.Dispose();
                        SetState(HostedAppState.Stopped);
                        return;
                    }
                    process.Start();
                    _process = process;
                }
                _log.Info("PadTide started (pid " + process.Id + ").");
                SetState(HostedAppState.Running);
            }
            catch (Exception error)
            {
                _log.Error("Could not start PadTide.", error);
                SetState(HostedAppState.Failed);
            }
        }

        private void OnExited(object? sender, EventArgs e)
        {
            var process = (Process)sender!;
            int code;
            try
            {
                code = process.ExitCode;
            }
            catch (InvalidOperationException)
            {
                code = -1;
            }
            process.Dispose();

            bool stopping;
            lock (_gate)
            {
                if (ReferenceEquals(_process, process)) _process = null;
                stopping = _stopping || _disposed;
            }
            if (stopping) return;

            if (code == AlreadyRunningExitCode)
            {
                EnterRunningOutside();
                return;
            }

            _log.Warn("PadTide exited unexpectedly (code " + code + ").");
            int recent;
            lock (_gate)
            {
                var now = DateTime.UtcNow;
                _crashes.Enqueue(now);
                while (_crashes.Count > 0 && now - _crashes.Peek() > CrashWindow) _crashes.Dequeue();
                recent = _crashes.Count;
            }

            if (recent >= MaxCrashesPerWindow)
            {
                _log.Error("PadTide keeps exiting; automatic restarts stopped.");
                SetState(HostedAppState.Failed);
                return;
            }

            SetState(HostedAppState.Starting);
            ScheduleLaunch(TimeSpan.FromSeconds(recent));
        }

        /// <summary>Checks only the mutex's existence until the standalone PadTide exits, then takes over.</summary>
        private void EnterRunningOutside()
        {
            SetState(HostedAppState.RunningOutside);
            lock (_gate)
            {
                if (_stopping || _disposed) return;
                _retry?.Dispose();
                _retry = new Timer(_ =>
                {
                    if (!IsStandaloneRunning()) Launch(show: false);
                }, null, OutsidePollInterval, OutsidePollInterval);
            }
        }

        private void ScheduleLaunch(TimeSpan delay)
        {
            lock (_gate)
            {
                if (_stopping || _disposed) return;
                _retry?.Dispose();
                _retry = new Timer(_ => Launch(show: false), null, delay, Timeout.InfiniteTimeSpan);
            }
        }

        private void SetState(HostedAppState state)
        {
            lock (_gate)
            {
                if (_state == state) return;
                _state = state;
            }
            try
            {
                StateChanged?.Invoke(state);
            }
            catch (Exception error)
            {
                _log.Error("State handler failed.", error);
            }
        }

        private static bool IsStandaloneRunning()
        {
            try
            {
                if (!Mutex.TryOpenExisting(MutexName, out var mutex)) return false;
                mutex.Dispose();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true; // Exists, just not openable with these rights.
            }
        }

        private static bool PostToApp(int message)
        {
            var window = FindWindow(null, HostWindowTitle);
            return window != IntPtr.Zero && PostMessage(window, message, IntPtr.Zero, IntPtr.Zero);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? className, string windowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindow(int processId);
    }
}
