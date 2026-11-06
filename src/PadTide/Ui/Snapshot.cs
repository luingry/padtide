using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PadTide.Core;

namespace PadTide.Ui
{
    /// <summary>--snapshot: renders every visual state to PNG, without touching the controller.</summary>
    internal static class Snapshot
    {
        public static void Write(string directory)
        {
            Directory.CreateDirectory(directory);

            Save(directory, "badges-dark.png", Badges(Color.FromRgb(0x20, 0x24, 0x2A)));
            Save(directory, "badges-light.png", Badges(Color.FromRgb(0xE8, 0xEA, 0xEC)));
            Save(directory, "icon-sizes.png", Icons());
            Save(directory, "widget-states.png", WidgetStates());
            Save(directory, "tray-sizes.png", Trays());

            var app = (App)Application.Current;
            app.Settings.LastPercent = 42;
            foreach (var (name, report) in new (string, BatteryReport?)[]
            {
                ("settings-65.png", new BatteryReport(65, false)),
                ("settings-9.png", new BatteryReport(9, false)),
                ("settings-charging.png", BatteryReport.WiredCharging),
                ("settings-off.png", null),
            })
            {
                Save(directory, name, SettingsFrame(app, report));
            }

            app.PreviewHostedLook(true);
            Save(directory, "settings-hosted.png", SettingsFrame(app, new BatteryReport(65, false)));
            app.PreviewHostedLook(false);

            var menu = new TrayMenuWindow(app) { ShowActivated = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
            menu.Show();
            menu.UpdateLayout();
            var menuBitmap = new RenderTargetBitmap((int)menu.ActualWidth, (int)menu.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            menuBitmap.Render((Visual)menu.Content);
            Save(directory, "tray-menu.png", OnBackdrop(menuBitmap));
            menu.Close();

            using var updater = new Shell.Updater();
            var notice = new UpdateNoticeWindow(new UpdateOffer("0.2.0", "", null), updater, () => { });
            notice.Show();
            notice.UpdateLayout();
            var noticeBitmap = new RenderTargetBitmap((int)notice.ActualWidth, (int)notice.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            noticeBitmap.Render((Visual)notice.Content);
            Save(directory, "update-notice.png", OnBackdrop(noticeBitmap));
            notice.Close();
        }

        private static BitmapSource Badges(Color background) => Artwork.Render(1060, 300, dc =>
        {
            dc.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, 1060, 300));
            var x = 16.0;
            const int ChargingTide = 42;
            foreach (var (size, percent) in new (int, int?)[] { (30, 65), (30, 100), (30, 9), (30, null), (43, 65), (43, 30), (43, null), (64, 65), (64, 100), (64, 14), (64, null), (100, 65), (100, null), (100, 9) })
            {
                dc.PushTransform(new TranslateTransform(x, 40));
                Artwork.DrawBadge(dc, size, percent, percent ?? ChargingTide);
                dc.Pop();
                dc.PushTransform(new TranslateTransform(x, 170));
                dc.PushOpacity(0.5);
                Artwork.DrawBadge(dc, size, percent, percent ?? ChargingTide);
                dc.Pop();
                dc.Pop();
                x += size + 14;
            }
        });

        private static BitmapSource WidgetStates() => Artwork.Render(520, 130, dc =>
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x17, 0x1D, 0x24)), null, new Rect(0, 0, 520, 130), 18, 18);
            var x = 40.0;
            foreach (var percent in new int?[] { 65, null, 9 })
            {
                dc.PushTransform(new TranslateTransform(x, 32));
                Artwork.DrawBadge(dc, 100, percent, percent ?? 42);
                dc.Pop();
                x += 160;
            }
        });

        private static BitmapSource Icons() => Artwork.Render(620, 300, dc =>
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2C, 0x30, 0x36)), null, new Rect(0, 0, 620, 300));
            var x = 12.0;
            foreach (var size in new[] { 16, 20, 24, 32, 40, 48, 64, 256 })
            {
                dc.PushTransform(new TranslateTransform(x, 22));
                Artwork.DrawAppIcon(dc, size);
                dc.Pop();
                x += size + 12;
            }
        });

        private static BitmapSource Trays() => Artwork.Render(420, 90, dc =>
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1C, 0x1F, 0x24)), null, new Rect(0, 0, 420, 90));
            var x = 10.0;
            foreach (var (size, percent) in new (int, int?)[] { (16, 65), (16, 100), (16, 9), (16, null), (20, 65), (20, null), (24, 65), (24, null), (32, 65), (32, 9), (32, null) })
            {
                dc.PushTransform(new TranslateTransform(x, 20));
                Artwork.DrawTrayIcon(dc, size, percent);
                dc.Pop();
                x += size + 12;
            }
        });

        private static BitmapSource SettingsFrame(App app, BatteryReport? report)
        {
            var window = new SettingsWindow(app) { ShowActivated = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show();
            window.ShowBatteryForSnapshot(report);
            window.UpdateLayout();
            LetAnimationsFinish();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render((Visual)window.Content);
            window.Close();
            return OnBackdrop(bitmap);
        }

        private static void LetAnimationsFinish()
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        private static BitmapSource OnBackdrop(BitmapSource content) => Artwork.Render(content.PixelWidth, content.PixelHeight, dc =>
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x5B, 0x63, 0x6B)), null, new Rect(0, 0, content.PixelWidth, content.PixelHeight));
            dc.DrawImage(content, new Rect(0, 0, content.PixelWidth, content.PixelHeight));
        });

        private static void Save(string directory, string name, BitmapSource bitmap)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(directory, name));
            encoder.Save(stream);
        }
    }
}
