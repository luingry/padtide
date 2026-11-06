using System;
using PadTide.Core;

namespace PadTide.Tests
{
    internal static class Program
    {
        private static int _failures;

        private static int Main()
        {
            RequestIsTheCapturedDeviceInfoPacket();
            ParsesTheCapturedAnswer();
            ReadsTheChargingBitAndClampsTheLevel();
            RejectsAnythingThatIsNotADeviceInfoAnswer();
            LowLevelMeansStrictlyBelowFifteen();
            SettingsClampAndRoundTrip();
            OffersOnlyANewerInstallerFromThisProject();
            ChecksForUpdatesAtMostOncePerDay();
            ChargingIsNeverLow();
            KeepsTheWidgetInsideTheFrame();

            Console.WriteLine(_failures == 0 ? "OK" : _failures + " falha(s)");
            return _failures == 0 ? 0 : 1;
        }

        private static byte[] Answer(byte battery)
        {
            var buffer = new byte[BatteryReport.ReportLength];
            buffer[1] = 0x01;
            buffer[2] = 0xAA;
            buffer[19] = battery;
            return buffer;
        }

        private static void RequestIsTheCapturedDeviceInfoPacket()
        {
            var request = BatteryReport.CreateRequest();
            Check(request.Length == 65, "o pedido tem 65 bytes");
            Check(request[0] == 0 && request[1] == 1 && request[2] == 1, "cabecalho do pedido");
            for (var i = 3; i < request.Length; i++) Check(request[i] == 0, "resto do pedido zerado");
        }

        private static void ParsesTheCapturedAnswer()
        {
            Check(BatteryReport.TryParse(Answer(0x41), 65, out var report), "aceita resposta valida");
            Check(report.Percent == 65 && !report.Charging, "le 65%");
        }

        private static void ReadsTheChargingBitAndClampsTheLevel()
        {
            BatteryReport.TryParse(Answer(0x80 | 42), 65, out var charging);
            Check(charging.Charging && charging.Percent == 42, "bit 7 indica carga");
            BatteryReport.TryParse(Answer(0xFF), 65, out var overflow);
            Check(overflow.Percent == 100, "valor acima de 100 vira 100");
            Check(!overflow.HasLevel && overflow.Charging, "0xFF indica carga sem nivel conhecido");
            Check(charging.HasLevel, "carga com nivel informado mantem o nivel");
            Check(!BatteryReport.WiredCharging.HasLevel, "no cabo o nivel nao e conhecido");
        }

        private static void RejectsAnythingThatIsNotADeviceInfoAnswer()
        {
            var stream = Answer(0x41);
            stream[1] = 0xF4;
            Check(!BatteryReport.TryParse(stream, 65, out _), "rejeita outro comando");

            var reportId = Answer(0x41);
            reportId[0] = 7;
            Check(!BatteryReport.TryParse(reportId, 65, out _), "rejeita outro report");

            var noController = Answer(0x41);
            noController[2] = 0;
            Check(!BatteryReport.TryParse(noController, 65, out _), "rejeita resposta sem controle");

            Check(!BatteryReport.TryParse(Answer(0x41), 19, out _), "rejeita resposta curta");
            Check(!BatteryReport.TryParse(null!, 65, out _), "rejeita buffer nulo");
        }

        private static void LowLevelMeansStrictlyBelowFifteen()
        {
            Check(BatteryReport.IsLowLevel(14), "14% e baixo");
            Check(!BatteryReport.IsLowLevel(15), "15% nao e baixo");
        }

        private static void SettingsClampAndRoundTrip()
        {
            var defaults = new AppSettings();
            Check(defaults.OpacityPercent == 50, "opacidade padrao 50%");

            var clamped = AppSettings.Parse(new[] { "size=500", "opacity=1", "lixo", "left=abc" });
            Check(clamped.Size == 100 && clamped.OpacityPercent == 10 && clamped.Left == null, "limita valores invalidos");
            Check(AppSettings.Parse(new[] { "size=2" }).Size == 30, "tamanho minimo 30");

            var saved = new AppSettings { Size = 72, OpacityPercent = 35, Left = 1234.5, Top = -20, LastPercent = 61 };
            var loaded = AppSettings.Parse(saved.Serialize().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            Check(loaded.Size == 72 && loaded.OpacityPercent == 35 && loaded.Left == 1234.5 && loaded.Top == -20 && loaded.LastPercent == 61, "preserva as preferencias");
            Check(AppSettings.Parse(new[] { "lastPercent=140" }).LastPercent == null, "ignora percentual invalido");
        }

        private static string Release(string tag, string assetName, string url, bool prerelease = false, string digest = "null") =>
            "{\"tag_name\":\"" + tag + "\",\"draft\":false,\"prerelease\":" + (prerelease ? "true" : "false") +
            ",\"unknown\":{\"a\":1},\"assets\":[{\"name\":\"" + assetName + "\",\"browser_download_url\":\"" + url + "\",\"digest\":" + digest + "}]}";

        private static void OffersOnlyANewerInstallerFromThisProject()
        {
            const string root = "https://github.com/luingry/padtide/releases/download/";
            var hash = new string('a', 64);

            var offer = UpdateManifest.Evaluate("0.1.0", Release("v0.2.0", "PadTide-Setup-0.2.0.exe", root + "v0.2.0/PadTide-Setup-0.2.0.exe", digest: "\"sha256:" + hash + "\""));
            Check(offer != null && offer.Version == "0.2.0" && offer.Sha256 == hash, "oferece versao nova com digest");

            Check(UpdateManifest.Evaluate("0.2.0", Release("v0.2.0", "PadTide-Setup-0.2.0.exe", root + "x.exe")) == null, "versao igual nao e oferecida");
            Check(UpdateManifest.Evaluate("0.10.0", Release("v0.9.0", "PadTide-Setup-0.9.0.exe", root + "x.exe")) == null, "compara por numero, nao por texto");
            Check(UpdateManifest.Evaluate("0.1.0", Release("v0.2.0", "PadTide-Setup-0.2.0.exe", root + "x.exe", prerelease: true)) == null, "ignora pre-release");
            Check(UpdateManifest.Evaluate("0.1.0", Release("v0.2.0", "Outro.exe", root + "x.exe")) == null, "exige o instalador esperado");
            Check(UpdateManifest.Evaluate("0.1.0", Release("v0.2.0", "PadTide-Setup-0.2.0.exe", "https://example.com/PadTide-Setup-0.2.0.exe")) == null, "rejeita download de outro endereco");
            Check(UpdateManifest.Evaluate("0.1.0", Release("v0.2.0-beta", "PadTide-Setup-0.2.0.exe", root + "x.exe")) == null, "rejeita tag fora do padrao");
        }

        private static void ChecksForUpdatesAtMostOncePerDay()
        {
            var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
            Check(UpdatePolicy.IsDue(null, now), "primeira verificacao");
            Check(!UpdatePolicy.IsDue(now.AddHours(-23), now), "nao repete em menos de 24 h");
            Check(UpdatePolicy.IsDue(now.AddHours(-24), now), "repete apos 24 h");

            var saved = new AppSettings { LastUpdateCheckUtc = now };
            var loaded = AppSettings.Parse(saved.Serialize().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            Check(loaded.LastUpdateCheckUtc == now, "preserva a data da verificacao");
        }

        private static void ChargingIsNeverLow()
        {
            Check(BatteryReport.WiredCharging.Charging, "cabo significa carregando");
            Check(!new BatteryReport(5, true).IsLow, "carregando nao fica com contorno amarelo");
            Check(new BatteryReport(5, false).IsLow, "5% sem carga e baixo");
        }

        private static void KeepsTheWidgetInsideTheFrame()
        {
            Check(WidgetPlacement.Clamp(100, 64, 0, 2560) == 100, "posicao valida nao muda");
            Check(WidgetPlacement.Clamp(-30, 64, 0, 2560) == 0, "limita na borda esquerda");
            Check(WidgetPlacement.Clamp(2550, 64, 0, 2560) == 2496, "limita na borda direita");
            Check(WidgetPlacement.Clamp(40, 41, 66, 1146) == 66, "respeita o monitor deslocado");
            Check(WidgetPlacement.Clamp(5000, 64, 2560, 4480) == 4416, "limita no segundo monitor");
        }

        private static void Check(bool condition, string name)
        {
            if (condition) return;
            _failures++;
            Console.WriteLine("FALHOU: " + name);
        }
    }
}
