using System;
using System.Collections.Generic;
using System.IO;

namespace PadTide.Core
{
    /// <summary>
    /// Running under the SwissTechKnife host (--hosted): the host owns the tray, updates and auto-start,
    /// data lives in the module's folder and the process exits with its parent.
    /// </summary>
    public sealed class HostedOptions
    {
        public const string HostedArgument = "--hosted";
        public const string ParentArgument = "--parent";
        public const string DataArgument = "--data";
        public const string ShowArgument = "--show";

        /// <summary>Exit code when another PadTide already owns the single-instance mutex.</summary>
        public const int AlreadyRunningExitCode = 3;

        private HostedOptions(int parentId, string dataDirectory, bool showSettings)
        {
            ParentId = parentId;
            DataDirectory = dataDirectory;
            ShowSettings = showSettings;
        }

        public int ParentId { get; }
        public string DataDirectory { get; }
        public bool ShowSettings { get; }

        /// <summary>Null unless the arguments carry --hosted and a rooted --data folder.</summary>
        public static HostedOptions? TryParse(IReadOnlyList<string> args)
        {
            var hosted = false;
            var show = false;
            var parent = 0;
            string? data = null;
            for (var i = 0; i < args.Count; i++)
            {
                var argument = args[i];
                if (string.Equals(argument, HostedArgument, StringComparison.OrdinalIgnoreCase)) hosted = true;
                else if (string.Equals(argument, ShowArgument, StringComparison.OrdinalIgnoreCase)) show = true;
                else if (string.Equals(argument, ParentArgument, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count) int.TryParse(args[++i], out parent);
                else if (string.Equals(argument, DataArgument, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count) data = args[++i];
            }
            if (!hosted || string.IsNullOrWhiteSpace(data) || !Path.IsPathRooted(data)) return null;
            return new HostedOptions(parent, data!, show);
        }

        /// <summary>First hosted run: copies the standalone preferences. The standalone file is never changed.</summary>
        public static bool MigrateSettings(string standaloneFile, string hostedFile)
        {
            try
            {
                if (File.Exists(hostedFile) || !File.Exists(standaloneFile)) return false;
                Directory.CreateDirectory(Path.GetDirectoryName(hostedFile)!);
                File.Copy(standaloneFile, hostedFile);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
