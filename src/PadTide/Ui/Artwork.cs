using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PadTide.Core;
using PadTide.Native;

namespace PadTide.Ui
{
    /// <summary>Every drawn mark of the brand: the widget badge, the app icon and the tray icon.</summary>
    internal static class Artwork
    {
        // Controller drawing space: 100 wide, the body spans y 8..66.
        private const double ViewLeft = -2;
        private const double ViewTop = 4;
        private const double ViewWidth = 104;
        private const double ViewHeight = 66;
        private const double NumberCenterY = 28.5;
        private const double TideFloor = 66;
        private const double TideCeiling = 4;
        private const double TideSwell = 2.2;
        private const double BoltRounding = 1.2;

        public const double BadgeAspect = ViewHeight / ViewWidth;

        private static readonly Geometry Controller = Parse(
            "M 28,8 C 22,8 16,10 13,16 C 8,26 3,42 2,52 C 1,61 6,66 12,66 C 18,66 21,62 25,56 L 29,50 " +
            "C 31,47 34,46 37,46 L 63,46 C 66,46 69,47 71,50 L 75,56 C 79,62 82,66 88,66 C 94,66 99,61 98,52 " +
            "C 97,42 92,26 87,16 C 84,10 78,8 72,8 C 66,8 64,11 59,11 L 41,11 C 36,11 34,8 28,8 Z");
        private static readonly Geometry Bolt = Parse("M 53.5,16.2 L 41.5,31 L 49,31 L 46.5,40.8 L 58.5,26 L 51,26 Z");
        private static readonly Geometry Tide = Parse("M -2,37 C 14,31 34,31 50,37 S 84,43 102,37 L 102,70 L -2,70 Z");
        private static readonly Typeface Numeral = new Typeface(new FontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private static readonly Typeface NumeralCondensed = new Typeface(new FontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Condensed);

        /// <summary>The widget: the controller filled to the tide level, with the percentage in the middle, or a bolt while charging (null number).</summary>
        public static void DrawBadge(DrawingContext dc, double width, int? number, int? tide, double phase = 0, double windowOpacity = 1)
        {
            var scale = width / ViewWidth;
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.PushTransform(new TranslateTransform(-ViewLeft, -ViewTop));

            // Layered windows ignore clicks on pixels with zero alpha. Scaling by the window opacity keeps the
            // hollow middle clickable at every setting while staying invisible.
            var hitAlpha = (byte)Math.Min(255, Math.Ceiling(2 / Math.Max(0.01, windowOpacity)));
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(hitAlpha, 0, 0, 0)), null, Controller);

            var line = Brush(number is int percent && BatteryReport.IsLowLevel(percent) ? "Color.Signal" : "Color.Foam");
            var ink = Brush("Color.Foam");
            var halo = Brush("Color.Halo");
            var stroke = Math.Max(4.4, 1.7 / scale);
            var haloWidth = (width < 44 ? 1.1 : 1.8) / scale;

            dc.DrawGeometry(null, RoundPen(halo, stroke + haloWidth), Controller);
            var waterline = tide is int charge ? BadgeTide(charge, phase) : null;
            if (waterline != null) dc.DrawGeometry(line, null, new CombinedGeometry(GeometryCombineMode.Intersect, Controller, waterline));
            dc.DrawGeometry(null, RoundPen(line, stroke), Controller);

            if (number is int value) DrawAcrossWaterline(dc, BuildNumber(value, width >= 60), waterline, halo, haloWidth, ink, 0);
            else DrawAcrossWaterline(dc, Bolt, waterline, halo, haloWidth + BoltRounding, ink, BoltRounding);

            dc.Pop();
            dc.Pop();
        }

        /// <summary>The brand mark: the controller is a battery. D-pad and minus are its poles, the tide its charge.</summary>
        public static void DrawAppIcon(DrawingContext dc, double size)
        {
            var compact = size <= 24;
            var tile = new RectangleGeometry(new Rect(0, 0, size, size), size * 0.235, size * 0.235);
            dc.DrawGeometry(new LinearGradientBrush(Token("Color.TideHigh"), Token("Color.Abyss"), new Point(0.2, 0), new Point(0.8, 1)), null, tile);

            DrawBrandController(dc, size, size * (compact ? 0.92 : 0.8), compact);
        }

        /// <summary>The mark alone, for hosts that draw their own tile behind it (the STK catalog).</summary>
        private static void DrawModuleIcon(DrawingContext dc, double size) => DrawBrandController(dc, size, size, compact: true);

        private static void DrawBrandController(DrawingContext dc, double size, double width, bool compact)
        {
            var scale = width / 100;
            dc.PushTransform(new TranslateTransform((size - width) / 2, (size - 58 * scale) / 2 - 8 * scale));
            dc.PushTransform(new ScaleTransform(scale, scale));

            dc.PushClip(Controller);
            dc.DrawGeometry(Brush("Color.Foam"), null, Controller);
            dc.DrawGeometry(new LinearGradientBrush(Token("Color.CoralHigh"), Token("Color.Coral"), new Point(0.5, 0.35), new Point(0.5, 1)), null, Tide);
            dc.Pop();

            var pole = Brush("Color.Abyss");
            var arm = compact ? 12.0 : 10.5;
            var bar = compact ? 4.6 : 3.4;
            var y = compact ? 23.0 : 24.0;
            dc.DrawRoundedRectangle(pole, null, new Rect(29 - arm / 2, y - bar / 2, arm, bar), bar / 2, bar / 2);
            dc.DrawRoundedRectangle(pole, null, new Rect(29 - bar / 2, y - arm / 2, bar, arm), bar / 2, bar / 2);
            dc.DrawRoundedRectangle(pole, null, new Rect(71 - arm / 2, y - bar / 2, arm, bar), bar / 2, bar / 2);

            dc.Pop();
            dc.Pop();
        }

        /// <summary>Notification-area tile: the number over a tide at the real level, amber when low, a bolt while charging (null level).</summary>
        public static void DrawTrayIcon(DrawingContext dc, double size, int? level)
        {
            if (level == null)
            {
                dc.DrawRoundedRectangle(Brush("Color.TideHigh"), null, new Rect(0, 0, size, size), size * 0.22, size * 0.22);
                var boltScale = size * 0.78 / 24.6;
                dc.PushTransform(new TranslateTransform(size / 2, size / 2));
                dc.PushTransform(new ScaleTransform(boltScale, boltScale));
                dc.PushTransform(new TranslateTransform(-50, -28.5));
                var foam = Brush("Color.Foam");
                dc.DrawGeometry(foam, RoundPen(foam, 1.2), Bolt);
                dc.Pop();
                dc.Pop();
                dc.Pop();
                return;
            }

            var percent = level.Value;
            var low = BatteryReport.IsLowLevel(percent);
            var tile = new RectangleGeometry(new Rect(0, 0, size, size), size * 0.22, size * 0.22);
            dc.PushClip(tile);
            dc.DrawRectangle(Brush(low ? "Color.Signal" : "Color.Abyss"), null, new Rect(0, 0, size, size));
            if (!low)
            {
                var height = size * percent / 100.0;
                dc.DrawRectangle(Brush("Color.TideHigh"), null, new Rect(0, size - height, size, height));
            }
            dc.Pop();

            var text = percent.ToString(CultureInfo.InvariantCulture);
            var emSize = size * (text.Length > 2 ? 0.62 : 0.84);
            var digits = Layout(text, text.Length > 2 ? NumeralCondensed : Numeral, emSize, Brush(low ? "Color.Abyss" : "Color.Foam"));
            dc.DrawText(digits, new Point((size - digits.WidthIncludingTrailingWhitespace) / 2, (size - digits.Height) / 2));
        }

        public static DrawingImage AppIconImage()
        {
            var group = new DrawingGroup();
            using (var dc = group.Open()) DrawAppIcon(dc, 256);
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }

        public static BitmapSource Render(int pixelWidth, int pixelHeight, Action<DrawingContext> draw)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) draw(dc);
            var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>Builds an HICON the caller owns (TrayIcon destroys it).</summary>
        public static IntPtr CreateTrayIconHandle(int? level) => CreateIconHandle((dc, size) => DrawTrayIcon(dc, size, level));

        /// <summary>The icon shown while the controller is off: the brand mark alone.</summary>
        public static IntPtr CreateAppIconHandle() => CreateIconHandle(DrawAppIcon);

        private static IntPtr CreateIconHandle(Action<DrawingContext, double> draw)
        {
            var size = NativeMethods.GetSystemMetricsForDpi(NativeMethods.SmCxSmIcon, NativeMethods.GetDpiForSystem());
            var straight = new FormatConvertedBitmap(Render(size, size, dc => draw(dc, size)), PixelFormats.Bgra32, null, 0);
            var pixels = new byte[size * size * 4];
            straight.CopyPixels(pixels, size * 4, 0);

            var color = NativeMethods.CreateBitmap(size, size, 1, 32, pixels);
            var mask = NativeMethods.CreateBitmap(size, size, 1, 1, null);
            try
            {
                var info = new NativeMethods.IconInfo { IsIcon = true, Color = color, Mask = mask };
                var handle = NativeMethods.CreateIconIndirect(ref info);
                if (handle == IntPtr.Zero) throw new ExternalException("CreateIconIndirect failed", Marshal.GetLastWin32Error());
                return handle;
            }
            finally
            {
                NativeMethods.DeleteObject(color);
                NativeMethods.DeleteObject(mask);
            }
        }

        public static void WriteIconFile(string path)
        {
            var preview = new PngBitmapEncoder();
            preview.Frames.Add(BitmapFrame.Create(Render(256, 256, dc => DrawAppIcon(dc, 256))));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using (var previewFile = File.Create(Path.ChangeExtension(path, ".png"))) preview.Save(previewFile);

            var moduleIcon = new PngBitmapEncoder();
            moduleIcon.Frames.Add(BitmapFrame.Create(Render(256, 256, dc => DrawModuleIcon(dc, 256))));
            using (var moduleFile = File.Create(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "padtide-module.png"))) moduleIcon.Save(moduleFile);

            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
            var frames = new byte[sizes.Length][];
            for (var i = 0; i < sizes.Length; i++)
            {
                var size = sizes[i];
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(Render(size, size, dc => DrawAppIcon(dc, size))));
                using var stream = new MemoryStream();
                encoder.Save(stream);
                frames[i] = stream.ToArray();
            }

            using var writer = new BinaryWriter(File.Create(path));
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)sizes.Length);
            var offset = 6 + 16 * sizes.Length;
            for (var i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(frames[i].Length);
                writer.Write(offset);
                offset += frames[i].Length;
            }
            foreach (var frame in frames) writer.Write(frame);
        }

        /// <summary>A mark split at the waterline: foam with a halo above it, dark ink below it.</summary>
        private static void DrawAcrossWaterline(DrawingContext dc, Geometry mark, Geometry? waterline, Brush halo, double haloWidth, Brush ink, double rounding)
        {
            if (waterline != null) dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(ViewLeft, ViewTop, ViewWidth, ViewHeight)), waterline));
            dc.DrawGeometry(null, RoundPen(halo, haloWidth), mark);
            dc.DrawGeometry(ink, rounding > 0 ? RoundPen(ink, rounding) : null, mark);
            if (waterline == null) return;
            dc.Pop();

            var dark = Brush("Color.Abyss");
            dc.PushClip(waterline);
            dc.DrawGeometry(dark, rounding > 0 ? RoundPen(dark, rounding) : null, mark);
            dc.Pop();
        }

        /// <summary>The water below the battery level; 100% rises past the top of the controller.</summary>
        private static Geometry BadgeTide(int percent, double phase) =>
            Wave(ViewLeft, ViewLeft + ViewWidth, TideFloor - (TideFloor - TideCeiling) * percent / 100.0, TideSwell, phase, ViewTop + ViewHeight);

        /// <summary>A water body with one sine swell across its width; the phase makes it travel.</summary>
        public static Geometry Wave(double left, double right, double y, double swell, double phase, double bottom)
        {
            const int Segments = 24;
            var points = new Point[Segments + 2];
            for (var i = 0; i <= Segments; i++)
            {
                var t = i / (double)Segments;
                points[i] = new Point(left + (right - left) * t, y + swell * Math.Sin(2 * Math.PI * t + phase));
            }
            points[Segments + 1] = new Point(right, bottom);

            var wave = new StreamGeometry();
            using (var context = wave.Open())
            {
                context.BeginFigure(new Point(left, bottom), true, true);
                context.PolyLineTo(points, false, true);
            }
            wave.Freeze();
            return wave;
        }

        private static Geometry BuildNumber(int percent, bool withSign)
        {
            var text = percent.ToString(CultureInfo.InvariantCulture);
            var emSize = text.Length > 2 ? 23.0 : 27.0;
            var ink = Brushes.Black;
            var digits = Layout(text, Numeral, emSize, ink);
            var sign = withSign ? Layout("%", Numeral, emSize * 0.46, ink) : null;
            var gap = withSign ? 1.2 : 0;
            var total = digits.WidthIncludingTrailingWhitespace + gap + (sign?.WidthIncludingTrailingWhitespace ?? 0);
            var left = 50 - total / 2;

            var group = new GeometryGroup();
            var digitsTop = NumberCenterY - digits.Height / 2;
            group.Children.Add(digits.BuildGeometry(new Point(left, digitsTop)));
            if (sign != null)
            {
                var signTop = digitsTop + digits.Baseline - sign.Baseline;
                group.Children.Add(sign.BuildGeometry(new Point(left + digits.WidthIncludingTrailingWhitespace + gap, signTop)));
            }
            group.Freeze();
            return group;
        }

        private static FormattedText Layout(string text, Typeface typeface, double emSize, Brush brush) =>
            new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, emSize, brush, 1.0);

        private static Pen RoundPen(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            return pen;
        }

        private static Color Token(string key) => (Color)Application.Current.FindResource(key);

        private static Brush Brush(string colorKey)
        {
            var brush = new SolidColorBrush(Token(colorKey));
            brush.Freeze();
            return brush;
        }

        private static Geometry Parse(string data)
        {
            var geometry = Geometry.Parse(data);
            geometry.Freeze();
            return geometry;
        }
    }
}
