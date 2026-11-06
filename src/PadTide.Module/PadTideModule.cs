using System.IO;
using System.Windows;
using SwissTechKnife.Sdk;

namespace PadTide.Module
{
    /// <summary>
    /// SwissTechKnife entry point. PadTide keeps its own process and floating widget, so polling the controller
    /// never touches the host's UI thread. The module starts it with the host, restarts it after a crash, stops
    /// it when disabled and opens its settings.
    /// </summary>
    public sealed class PadTideModule : IBackgroundModule, IModuleWindow
    {
        /// <summary>Folder and file inside the package; see scripts/pack-module.ps1.</summary>
        internal const string AppFolder = "app";
        internal const string AppExecutable = "PadTide.Hosted.exe";

        private IModuleContext? _context;
        private HostedAppSupervisor? _supervisor;

        public void Initialize(IModuleContext context)
        {
            _context = context;
            _supervisor = new HostedAppSupervisor(Path.Combine(context.InstallDirectory, AppFolder, AppExecutable), context.DataDirectory, context.Log);
        }

        public void Start() => _supervisor?.Start();

        public void Stop() => _supervisor?.Stop();

        public FrameworkElement CreateView() => new ModulePage(_supervisor!, _context!);

        /// <summary>Double click on the module's notification-area icon.</summary>
        public void ShowWindow() => _supervisor?.OpenSettings();

        public void Dispose()
        {
            _supervisor?.Dispose();
            _supervisor = null;
        }
    }
}
