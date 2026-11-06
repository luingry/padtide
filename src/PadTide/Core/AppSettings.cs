using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PadTide.Core
{
    /// <summary>User preferences, stored as a small key=value file.</summary>
    public sealed class AppSettings
    {
        public const int MinSize = 30;
        public const int MaxSize = 100;
        public const int MinOpacity = 10;
        public const int MaxOpacity = 100;

        private int _size = 64;
        private int _opacityPercent = 50;

        public int Size
        {
            get => _size;
            set => _size = Clamp(value, MinSize, MaxSize);
        }

        public int OpacityPercent
        {
            get => _opacityPercent;
            set => _opacityPercent = Clamp(value, MinOpacity, MaxOpacity);
        }

        public double? Left { get; set; }
        public double? Top { get; set; }
        public DateTime? LastUpdateCheckUtc { get; set; }

        public static AppSettings Parse(IEnumerable<string> lines)
        {
            var settings = new AppSettings();
            foreach (var line in lines)
            {
                var separator = line.IndexOf('=');
                if (separator <= 0) continue;
                var key = line.Substring(0, separator).Trim();
                var value = line.Substring(separator + 1).Trim();
                switch (key)
                {
                    case "size" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size):
                        settings.Size = size;
                        break;
                    case "opacity" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var opacity):
                        settings.OpacityPercent = opacity;
                        break;
                    case "left" when TryParseCoordinate(value, out var left):
                        settings.Left = left;
                        break;
                    case "top" when TryParseCoordinate(value, out var top):
                        settings.Top = top;
                        break;
                    case "lastUpdateCheck" when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var checkedAt):
                        settings.LastUpdateCheckUtc = checkedAt;
                        break;
                }
            }
            return settings;
        }

        public string Serialize()
        {
            var text = new StringBuilder();
            text.AppendLine("size=" + Size.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("opacity=" + OpacityPercent.ToString(CultureInfo.InvariantCulture));
            if (Left.HasValue) text.AppendLine("left=" + Left.Value.ToString("0.##", CultureInfo.InvariantCulture));
            if (Top.HasValue) text.AppendLine("top=" + Top.Value.ToString("0.##", CultureInfo.InvariantCulture));
            if (LastUpdateCheckUtc.HasValue) text.AppendLine("lastUpdateCheck=" + LastUpdateCheckUtc.Value.ToString("o", CultureInfo.InvariantCulture));
            return text.ToString();
        }

        public static AppSettings Load(string path)
        {
            try
            {
                return File.Exists(path) ? Parse(File.ReadAllLines(path)) : new AppSettings();
            }
            catch (IOException)
            {
                return new AppSettings();
            }
            catch (UnauthorizedAccessException)
            {
                return new AppSettings();
            }
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, Serialize(), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        private static bool TryParseCoordinate(string value, out double coordinate) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate) &&
            !double.IsNaN(coordinate) && !double.IsInfinity(coordinate);

        private static int Clamp(int value, int minimum, int maximum) => Math.Max(minimum, Math.Min(maximum, value));
    }
}
