using System.Reflection;
using Microsoft.Win32;

namespace PadTide.Shell
{
    /// <summary>Owns one value in the shared per-user Run key; never touches its other entries.</summary>
    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "PadTide";
        public const string StartupArgument = "--startup";

        public static bool IsEnabled
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) != null;
            }
        }

        public static void Set(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (enabled) key.SetValue(ValueName, "\"" + Assembly.GetExecutingAssembly().Location + "\" " + StartupArgument, RegistryValueKind.String);
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
