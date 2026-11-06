using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using PadTide.Core;
using PadTide.Native;
using PadTide.Shell;

namespace PadTide.Ui
{
    /// <summary>Asks before updating, then shows the download. Appears in a corner without taking focus.</summary>
    public partial class UpdateNoticeWindow : Window
    {
        private const double EdgeMargin = 8;
        private readonly UpdateOffer _offer;
        private readonly Updater _updater;
        private readonly Action _quit;

        internal UpdateNoticeWindow(UpdateOffer offer, Updater updater, Action quit)
        {
            _offer = offer;
            _updater = updater;
            _quit = quit;
            InitializeComponent();
            BrandIcon.Source = Artwork.AppIconImage();
            DetailText.Text = "Versão " + offer.Version;
            SourceInitialized += (s, e) => NativeMethods.MakeNonActivatingToolWindow(new WindowInteropHelper(this).Handle);
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -10000;
            Top = -10000;
            Loaded += (s, e) =>
            {
                Left = SystemParameters.WorkArea.Right - ActualWidth - EdgeMargin;
                Top = SystemParameters.WorkArea.Bottom - ActualHeight - EdgeMargin;
            };
        }

        private void OnLaterClicked(object sender, RoutedEventArgs e) => Close();

        private async void OnUpdateClicked(object sender, RoutedEventArgs e)
        {
            Actions.Visibility = Visibility.Collapsed;
            ProgressRail.Visibility = Visibility.Visible;
            DetailText.Text = "Baixando a versão " + _offer.Version;
            try
            {
                var path = await _updater.DownloadAsync(_offer, new Progress<double>(ShowProgress));
                DetailText.Text = "Instalando. O PadTide volta em instantes";
                ProgressFill.Width = ProgressRail.ActualWidth;
                _updater.StartSilentInstall(path);
                await Task.Delay(300);
                _quit();
            }
            catch (Exception exception) when (exception is IOException || exception is HttpRequestException || exception is TaskCanceledException || exception is Win32Exception)
            {
                ProgressRail.Visibility = Visibility.Collapsed;
                Actions.Visibility = Visibility.Visible;
                DetailText.Text = "O download falhou. Verifique a conexão e tente de novo";
            }
        }

        private void ShowProgress(double fraction) => ProgressFill.Width = ProgressRail.ActualWidth * Math.Max(0, Math.Min(1, fraction));
    }
}
