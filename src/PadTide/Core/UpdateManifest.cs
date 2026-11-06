using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PadTide.Core
{
    public enum UpdateCheckResult
    {
        UpToDate,
        Available,
        Failed,
    }

    public sealed class UpdateOffer
    {
        public UpdateOffer(string version, string downloadUrl, string? sha256)
        {
            Version = version;
            DownloadUrl = downloadUrl;
            Sha256 = sha256;
        }

        public string Version { get; }
        public string DownloadUrl { get; }
        public string? Sha256 { get; }
    }

    public static class UpdatePolicy
    {
        public static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromHours(24);

        public static bool IsDue(DateTime? lastCheckUtc, DateTime nowUtc) =>
            lastCheckUtc == null || nowUtc - lastCheckUtc.Value >= MinimumCheckInterval;
    }

    /// <summary>Decides whether a GitHub release is an installable update over the running version.</summary>
    public static class UpdateManifest
    {
        public const string LatestReleaseUrl = "https://api.github.com/repos/luingry/padtide/releases/latest";
        public const string InstallerAssetPrefix = "PadTide-Setup-";

        private const string DownloadRoot = "https://github.com/luingry/padtide/releases/download/";
        private const string DigestPrefix = "sha256:";

        public static UpdateOffer? Evaluate(string currentVersion, string releaseJson)
        {
            ReleaseDto? release;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(releaseJson)))
            {
                release = new DataContractJsonSerializer(typeof(ReleaseDto)).ReadObject(stream) as ReleaseDto;
            }
            if (release == null || release.Draft || release.Prerelease || release.Assets == null) return null;
            if (!TryParseVersion(currentVersion, out var current) || !TryParseVersion(release.Tag, out var latest) || latest <= current) return null;

            var version = latest.ToString(3);
            var expectedName = InstallerAssetPrefix + version + ".exe";
            foreach (var asset in release.Assets)
            {
                if (!string.Equals(asset.Name, expectedName, StringComparison.OrdinalIgnoreCase)) continue;
                // An installer is only ever taken from this project's own release downloads.
                if (asset.Url == null || !asset.Url.StartsWith(DownloadRoot, StringComparison.Ordinal)) return null;
                return new UpdateOffer(version, asset.Url, ParseDigest(asset.Digest));
            }
            return null;
        }

        public static bool TryParseVersion(string? text, out Version version)
        {
            version = new Version(0, 0, 0);
            if (string.IsNullOrWhiteSpace(text)) return false;
            var trimmed = text!.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed.Substring(1);
            var parts = trimmed.Split('.');
            if (parts.Length != 3) return false;
            var numbers = new int[3];
            for (var i = 0; i < 3; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return false;
            }
            version = new Version(numbers[0], numbers[1], numbers[2]);
            return true;
        }

        private static string? ParseDigest(string? digest)
        {
            if (digest == null || !digest.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase)) return null;
            var hash = digest.Substring(DigestPrefix.Length);
            return hash.Length == 64 ? hash : null;
        }

        [DataContract]
        private sealed class ReleaseDto
        {
            [DataMember(Name = "tag_name")] public string? Tag { get; set; }
            [DataMember(Name = "draft")] public bool Draft { get; set; }
            [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
            [DataMember(Name = "assets")] public AssetDto[]? Assets { get; set; }
        }

        [DataContract]
        private sealed class AssetDto
        {
            [DataMember(Name = "name")] public string? Name { get; set; }
            [DataMember(Name = "browser_download_url")] public string? Url { get; set; }
            [DataMember(Name = "digest")] public string? Digest { get; set; }
        }
    }
}
