using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PadTide.Native
{
    internal static class NativeMethods
    {
        public const uint GenericRead = 0x80000000;
        public const uint GenericWrite = 0x40000000;
        public const uint FileShareReadWrite = 3;
        public const uint OpenExisting = 3;
        public const uint FileFlagOverlapped = 0x40000000;
        public const int ErrorIoPending = 997;
        public const uint WaitObject0 = 0;

        public const uint DigcfPresent = 0x02;
        public const uint DigcfDeviceInterface = 0x10;

        public const int WmClose = 0x0010;
        public const int WmDeviceChange = 0x0219;
        public const int DbtDeviceArrival = 0x8000;
        public const int DbtDeviceRemoveComplete = 0x8004;
        public const int WmContextMenu = 0x007B;
        public const int WmApp = 0x8000;
        public const int NinSelect = 0x0400;
        public const int NinKeySelect = 0x0401;

        public const int GwlExStyle = -20;
        public const int WsExToolWindow = 0x00000080;
        public const int WsExNoActivate = 0x08000000;
        public const int WsPopup = unchecked((int)0x80000000);

        public static readonly IntPtr HwndTopmost = new IntPtr(-1);
        public const uint SwpNoSize = 0x0001;
        public const uint SwpNoMove = 0x0002;
        public const uint SwpNoActivate = 0x0010;

        public const uint NimAdd = 0;
        public const uint NimModify = 1;
        public const uint NimDelete = 2;
        public const uint NimSetVersion = 4;
        public const uint NifMessage = 0x01;
        public const uint NifIcon = 0x02;
        public const uint NifTip = 0x04;
        public const uint NotifyIconVersion4 = 4;

        public const int SmCxSmIcon = 49;
        public const uint MonitorDefaultToNearest = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct SpDeviceInterfaceData
        {
            public int Size;
            public Guid ClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HidCaps
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DevBroadcastDeviceInterface
        {
            public int Size;
            public int DeviceType;
            public int Reserved;
            public Guid ClassGuid;
            public short Name;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NotifyIconData
        {
            public int Size;
            public IntPtr Window;
            public uint Id;
            public uint Flags;
            public int CallbackMessage;
            public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
            public uint State;
            public uint StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
            public uint InfoFlags;
            public Guid Item;
            public IntPtr BalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IconInfo
        {
            [MarshalAs(UnmanagedType.Bool)] public bool IsIcon;
            public int HotspotX;
            public int HotspotY;
            public IntPtr Mask;
            public IntPtr Color;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MonitorInfo
        {
            public int Size;
            public Rect Frame;
            public Rect Work;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(SafeFileHandle file, IntPtr buffer, uint count, IntPtr written, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(SafeFileHandle file, IntPtr buffer, uint count, IntPtr read, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetOverlappedResult(SafeFileHandle file, IntPtr overlapped, out uint transferred, bool wait);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        public static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimum, IntPtr maximum);

        [DllImport("kernel32.dll")]
        public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr parent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid classGuid, uint index, ref SpDeviceInterfaceData data);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SpDeviceInterfaceData data, IntPtr detail, int size, out int required, IntPtr info);

        [DllImport("setupapi.dll")]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("hid.dll")]
        public static extern void HidD_GetHidGuid(out Guid guid);

        [DllImport("hid.dll")]
        public static extern bool HidD_GetPreparsedData(SafeFileHandle file, out IntPtr data);

        [DllImport("hid.dll")]
        public static extern bool HidD_FreePreparsedData(IntPtr data);

        [DllImport("hid.dll")]
        public static extern int HidP_GetCaps(IntPtr data, out HidCaps caps);

        [DllImport("hid.dll")]
        public static extern bool HidD_FlushQueue(SafeFileHandle file);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr RegisterDeviceNotification(IntPtr recipient, ref DevBroadcastDeviceInterface filter, int flags);

        [DllImport("user32.dll")]
        public static extern bool UnregisterDeviceNotification(IntPtr handle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string name);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string? className, string windowName);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        public static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        public static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(Point point, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetricsForDpi(int index, uint dpi);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForSystem();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CreateIconIndirect(ref IconInfo info);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr icon);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitsPerPixel, byte[]? bits);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr handle);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
        public static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

        public static void MakeNonActivatingToolWindow(IntPtr window)
        {
            var style = GetWindowLongPtr(window, GwlExStyle).ToInt64();
            SetWindowLongPtr(window, GwlExStyle, new IntPtr(style | WsExToolWindow | WsExNoActivate));
        }
    }
}
