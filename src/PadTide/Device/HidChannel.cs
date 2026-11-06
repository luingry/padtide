using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using PadTide.Core;
using PadTide.Native;

namespace PadTide.Device
{
    /// <summary>
    /// Talks to the Nova 2 Lite dongle over its vendor HID interface. The dongle reports PID 1098 only while
    /// the controller is on; alone it reports PID 2106, so finding this path already means "controller on".
    /// On the cable the controller itself appears as PID 100F. That mode is recognized by presence alone: no
    /// request is ever sent to it.
    /// </summary>
    internal readonly struct ControllerLink
    {
        public ControllerLink(string? dongleInterface, bool isWired)
        {
            DongleInterface = dongleInterface;
            IsWired = isWired;
        }

        public string? DongleInterface { get; }
        public bool IsWired { get; }
    }

    internal static class HidChannel
    {
        private const string InterfaceMarker = "vid_3537&pid_1098&mi_02";
        private const string WiredMarker = "vid_3537&pid_100f";
        private const ushort VendorUsagePage = 0xFF7A;
        private const int ReadBudgetMilliseconds = 800;
        private const int WriteBudgetMilliseconds = 300;

        public static ControllerLink FindController()
        {
            NativeMethods.HidD_GetHidGuid(out var hidGuid);
            var set = NativeMethods.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, NativeMethods.DigcfPresent | NativeMethods.DigcfDeviceInterface);
            if (set == new IntPtr(-1)) return default;
            try
            {
                string? vendorPath = null;
                var data = new NativeMethods.SpDeviceInterfaceData { Size = Marshal.SizeOf<NativeMethods.SpDeviceInterfaceData>() };
                for (uint index = 0; NativeMethods.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, index, ref data); index++)
                {
                    var path = ReadInterfacePath(set, ref data);
                    if (path == null) continue;
                    if (path.IndexOf(WiredMarker, StringComparison.OrdinalIgnoreCase) >= 0) return new ControllerLink(null, true);
                    if (vendorPath == null && path.IndexOf(InterfaceMarker, StringComparison.OrdinalIgnoreCase) >= 0 && IsVendorInterface(path)) vendorPath = path;
                }
                return new ControllerLink(vendorPath, false);
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(set);
            }
        }

        public static bool TryRead(string path, out BatteryReport report)
        {
            report = default;
            using var file = NativeMethods.CreateFile(path, NativeMethods.GenericRead | NativeMethods.GenericWrite, NativeMethods.FileShareReadWrite,
                IntPtr.Zero, NativeMethods.OpenExisting, NativeMethods.FileFlagOverlapped, IntPtr.Zero);
            if (file.IsInvalid) return false;

            // Other apps may have turned on the state stream; stale reports must not be mistaken for the answer.
            NativeMethods.HidD_FlushQueue(file);
            var request = BatteryReport.CreateRequest();
            if (!Transfer(file, request, write: true, WriteBudgetMilliseconds, out var written) || written != request.Length) return false;

            var clock = Stopwatch.StartNew();
            var buffer = new byte[BatteryReport.ReportLength];
            while (clock.ElapsedMilliseconds < ReadBudgetMilliseconds)
            {
                var remaining = (int)(ReadBudgetMilliseconds - clock.ElapsedMilliseconds);
                if (!Transfer(file, buffer, write: false, remaining, out var read)) return false;
                if (BatteryReport.TryParse(buffer, read, out report)) return true;
            }
            return false;
        }

        private static string? ReadInterfacePath(IntPtr set, ref NativeMethods.SpDeviceInterfaceData data)
        {
            NativeMethods.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out var required, IntPtr.Zero);
            if (required <= 0) return null;
            var detail = Marshal.AllocHGlobal(required);
            try
            {
                // cbSize is the fixed part of SP_DEVICE_INTERFACE_DETAIL_DATA_W, which differs by process bitness.
                Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out _, IntPtr.Zero)) return null;
                return Marshal.PtrToStringUni(detail + 4);
            }
            finally
            {
                Marshal.FreeHGlobal(detail);
            }
        }

        private static bool IsVendorInterface(string path)
        {
            using var file = NativeMethods.CreateFile(path, 0, NativeMethods.FileShareReadWrite, IntPtr.Zero, NativeMethods.OpenExisting, 0, IntPtr.Zero);
            if (file.IsInvalid || !NativeMethods.HidD_GetPreparsedData(file, out var data)) return false;
            try
            {
                NativeMethods.HidP_GetCaps(data, out var caps);
                return caps.UsagePage == VendorUsagePage && caps.OutputReportByteLength == BatteryReport.ReportLength;
            }
            finally
            {
                NativeMethods.HidD_FreePreparsedData(data);
            }
        }

        private static bool Transfer(SafeFileHandle file, byte[] buffer, bool write, int timeoutMilliseconds, out int transferred)
        {
            transferred = 0;
            using var completed = new ManualResetEvent(false);
            var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var overlapped = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlapped>());
            try
            {
                Marshal.StructureToPtr(new NativeOverlapped { EventHandle = completed.SafeWaitHandle.DangerousGetHandle() }, overlapped, false);
                var started = write
                    ? NativeMethods.WriteFile(file, pinned.AddrOfPinnedObject(), (uint)buffer.Length, IntPtr.Zero, overlapped)
                    : NativeMethods.ReadFile(file, pinned.AddrOfPinnedObject(), (uint)buffer.Length, IntPtr.Zero, overlapped);
                if (!started && Marshal.GetLastWin32Error() != NativeMethods.ErrorIoPending) return false;

                if (NativeMethods.WaitForSingleObject(completed.SafeWaitHandle.DangerousGetHandle(), (uint)Math.Max(1, timeoutMilliseconds)) != NativeMethods.WaitObject0)
                {
                    NativeMethods.CancelIoEx(file, overlapped);
                    NativeMethods.GetOverlappedResult(file, overlapped, out _, true);
                    return false;
                }
                if (!NativeMethods.GetOverlappedResult(file, overlapped, out var count, false)) return false;
                transferred = (int)count;
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(overlapped);
                pinned.Free();
            }
        }
    }
}
