using System;
using System.Runtime.InteropServices;
using PadTide.Native;

namespace PadTide.Shell
{
    /// <summary>The notification-area icon. Always present: the percentage while the controller is on, the brand mark while it is off.</summary>
    internal sealed class TrayIcon : IDisposable
    {
        public const int CallbackMessage = NativeMethods.WmApp + 1;
        private const uint IconId = 1;

        private readonly IntPtr _window;
        private IntPtr _icon;
        private string _tip = "";
        private bool _added;

        public TrayIcon(IntPtr window)
        {
            _window = window;
        }

        public void Show(IntPtr ownedIcon, string tip)
        {
            var previous = _icon;
            _icon = ownedIcon;
            _tip = tip;
            var data = CreateData();
            if (!_added)
            {
                _added = NativeMethods.ShellNotifyIcon(NativeMethods.NimAdd, ref data);
                if (_added)
                {
                    data.Version = NativeMethods.NotifyIconVersion4;
                    NativeMethods.ShellNotifyIcon(NativeMethods.NimSetVersion, ref data);
                }
            }
            else
            {
                NativeMethods.ShellNotifyIcon(NativeMethods.NimModify, ref data);
            }
            if (previous != IntPtr.Zero) NativeMethods.DestroyIcon(previous);
        }

        public void Hide()
        {
            if (_added)
            {
                var data = CreateData();
                NativeMethods.ShellNotifyIcon(NativeMethods.NimDelete, ref data);
                _added = false;
            }
            if (_icon != IntPtr.Zero) NativeMethods.DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }

        /// <summary>Explorer restarted, so the shell forgot the icon.</summary>
        public void Restore()
        {
            if (!_added || _icon == IntPtr.Zero) return;
            _added = false;
            var data = CreateData();
            _added = NativeMethods.ShellNotifyIcon(NativeMethods.NimAdd, ref data);
            if (_added)
            {
                data.Version = NativeMethods.NotifyIconVersion4;
                NativeMethods.ShellNotifyIcon(NativeMethods.NimSetVersion, ref data);
            }
        }

        public void Dispose() => Hide();

        private NativeMethods.NotifyIconData CreateData() => new NativeMethods.NotifyIconData
        {
            Size = Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            Window = _window,
            Id = IconId,
            // Version 4 hides the native tooltip without NIF_SHOWTIP; the tip remains for screen readers.
            Flags = NativeMethods.NifMessage | NativeMethods.NifIcon | NativeMethods.NifTip,
            CallbackMessage = CallbackMessage,
            Icon = _icon,
            Tip = _tip,
        };
    }
}
