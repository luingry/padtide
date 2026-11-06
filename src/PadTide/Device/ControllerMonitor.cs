using System;
using System.Threading;
using PadTide.Core;

namespace PadTide.Device
{
    /// <summary>
    /// Polls only while the controller's interface exists. With the controller off nothing ticks: the next
    /// scan comes from a Windows device notification (see <see cref="DeviceChanged"/>).
    /// </summary>
    internal sealed class ControllerMonitor : IDisposable
    {
        private const int PollMilliseconds = 5000;
        private const int WakeRetryMilliseconds = 1000;
        private const int DeviceSettleMilliseconds = 400;
        private const int WakeRetries = 4;
        private const int MissesBeforeOffline = 2;

        private readonly Timer _timer;
        private readonly object _gate = new object();
        private string? _path;
        private bool _needsScan = true;
        private bool _published;
        private BatteryReport? _current;
        private int _misses;
        private int _wakeRetries;
        private bool _disposed;

        public ControllerMonitor()
        {
            _timer = new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>Raised on a worker thread when the reading changes; null means the controller is off.</summary>
        public event Action<BatteryReport?>? Changed;

        public void Start() => Schedule(0);

        public void DeviceChanged()
        {
            lock (_gate)
            {
                _needsScan = true;
                _wakeRetries = WakeRetries;
            }
            Schedule(DeviceSettleMilliseconds);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _timer.Dispose();
            }
        }

        private void Schedule(int delay)
        {
            lock (_gate)
            {
                if (!_disposed) _timer.Change(delay, Timeout.Infinite);
            }
        }

        private void Tick()
        {
            string? path;
            lock (_gate)
            {
                if (_needsScan) _path = HidChannel.FindDevicePath();
                _needsScan = false;
                path = _path;
            }

            if (path == null)
            {
                _misses = 0;
                Publish(null);
                return;
            }

            if (HidChannel.TryRead(path, out var report))
            {
                _misses = 0;
                _wakeRetries = 0;
                Publish(report);
                Schedule(PollMilliseconds);
                return;
            }

            lock (_gate) _needsScan = true;
            if (++_misses >= MissesBeforeOffline || _current == null) Publish(null);
            Schedule(_wakeRetries-- > 0 ? WakeRetryMilliseconds : PollMilliseconds);
        }

        private void Publish(BatteryReport? report)
        {
            var same = _published && Nullable.Equals(_current, report);
            _published = true;
            _current = report;
            if (!same) Changed?.Invoke(report);
        }
    }
}
