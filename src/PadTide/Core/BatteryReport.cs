namespace PadTide.Core
{
    /// <summary>Decodes the dongle's answer to the device-info request.</summary>
    public readonly struct BatteryReport
    {
        public const int LowThreshold = 15;
        public const int ReportLength = 65;

        /// <summary>On the cable the firmware reports no real level, so only the last wireless reading can place the tide.</summary>
        public static readonly BatteryReport WiredCharging = new BatteryReport(100, true, false);

        private const int CommandOffset = 1;
        private const int AddressOffset = 2;
        private const int AddressLength = 6;
        private const int BatteryOffset = 19;
        private const byte DeviceInfoCommand = 0x01;
        private const byte ChargingBit = 0x80;
        private const byte NoLevelMarker = 0xFF;

        public BatteryReport(int percent, bool charging, bool hasLevel = true)
        {
            Percent = percent;
            Charging = charging;
            HasLevel = hasLevel;
        }

        public int Percent { get; }
        public bool Charging { get; }

        /// <summary>False when the controller reported charging without telling how full it is.</summary>
        public bool HasLevel { get; }
        public bool IsLow => !Charging && IsLowLevel(Percent);

        public static bool IsLowLevel(int percent) => percent < LowThreshold;

        /// <summary>The only request this app ever sends: report 0, command 1, action 1.</summary>
        public static byte[] CreateRequest()
        {
            var request = new byte[ReportLength];
            request[CommandOffset] = DeviceInfoCommand;
            request[CommandOffset + 1] = 0x01;
            return request;
        }

        public static bool TryParse(byte[] buffer, int length, out BatteryReport report)
        {
            report = default;
            if (buffer == null || length <= BatteryOffset || buffer.Length < length) return false;
            if (buffer[0] != 0 || buffer[CommandOffset] != DeviceInfoCommand) return false;

            // An all-zero address means the dongle answered without a controller behind it.
            var hasAddress = false;
            for (var i = AddressOffset; i < AddressOffset + AddressLength; i++) hasAddress |= buffer[i] != 0;
            if (!hasAddress) return false;

            var raw = buffer[BatteryOffset];
            var percent = raw & ~ChargingBit;
            report = new BatteryReport(percent > 100 ? 100 : percent, (raw & ChargingBit) != 0, raw != NoLevelMarker);
            return true;
        }
    }
}
