using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using PadTide.Core;

namespace PadTide.Shell
{
    /// <summary>Checks GitHub for a newer release and drives the download + silent install flow.</summary>
    internal sealed class Updater : IDisposable
    {
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        public Updater()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("PadTide-Updater");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        public async Task<UpdateOffer?> CheckAsync(string currentVersion)
        {
            using var response = await _http.GetAsync(UpdateManifest.LatestReleaseUrl, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return UpdateManifest.Evaluate(currentVersion, await response.Content.ReadAsStringAsync());
        }

        public async Task<string> DownloadAsync(UpdateOffer offer, IProgress<double> progress)
        {
            var directory = Path.Combine(Path.GetTempPath(), "PadTide", "updates");
            Directory.CreateDirectory(directory);
            // The installer of a previous update cannot delete itself; this is the next chance to do it.
            foreach (var leftover in Directory.GetFiles(directory, UpdateManifest.InstallerAssetPrefix + "*.exe")) TryDelete(leftover);
            var destination = Path.Combine(directory, UpdateManifest.InstallerAssetPrefix + offer.Version + "-" + Guid.NewGuid().ToString("N") + ".exe");
            try
            {
                using var response = await _http.GetAsync(offer.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength;
                using (var source = await response.Content.ReadAsStreamAsync())
                using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    long received = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await target.WriteAsync(buffer, 0, read);
                        received += read;
                        if (total > 0) progress.Report((double)received / total.Value);
                    }
                    if (received == 0 || (total.HasValue && received != total.Value)) throw new IOException("Update download ended early.");
                }
                if (offer.Sha256 != null && !string.Equals(HashFile(destination), offer.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("Update download failed its integrity check.");
                }
                return destination;
            }
            catch
            {
                TryDelete(destination);
                throw;
            }
        }

        // /update=1 is a custom Inno Setup parameter (see installer/PadTide.iss) that relaunches the app
        // after a silent install; [Run]'s skipifsilent flag would otherwise suppress that.
        public void StartSilentInstall(string installerPath)
        {
            var started = Process.Start(new ProcessStartInfo(installerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /update=1") { UseShellExecute = true });
            if (started == null) throw new IOException("The update installer did not start.");
        }

        public void Dispose() => _http.Dispose();

        private static string HashFile(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A leftover in the temp folder is harmless.
            }
            catch (UnauthorizedAccessException)
            {
                // A leftover in the temp folder is harmless.
            }
        }
    }
}
