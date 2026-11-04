using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

[assembly: System.Reflection.AssemblyVersion(ReleaseCheck.CurrentVersion)]
[assembly: System.Reflection.AssemblyFileVersion(ReleaseCheck.CurrentVersion)]

internal static class ReleaseCheck
{
    public const string CurrentVersion = "1.2.0";
    public const string ReleasePage = "https://github.com/IvanZakharov77/vision-break/releases/latest";
    private const string ReleaseApi = "https://api.github.com/repos/IvanZakharov77/vision-break/releases/latest";

    public static async Task<UpdateOffer> CheckAsync()
    {
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
            using (var client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromSeconds(6);
                client.MaxResponseContentBufferSize = 1024 * 1024;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("VisionBreak/" + CurrentVersion);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                string payload = await client.GetStringAsync(ReleaseApi).ConfigureAwait(false);
                return FindUpdate(new Version(CurrentVersion), payload);
            }
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public static Version FindNewerVersion(Version current, string payload)
    {
        return NewerVersion(current, Parse(payload));
    }

    public static UpdateOffer FindUpdate(Version current, string payload)
    {
        Release release = Parse(payload);
        Version version = NewerVersion(current, release);
        if (version == null || release.assets == null) return null;
        string name = "VisionBreak-Setup-" + version.ToString(3) + ".exe";
        string expectedUrl = "https://github.com/IvanZakharov77/vision-break/releases/download/v" +
            version.ToString(3) + "/" + name;
        Asset selected = null;
        foreach (Asset asset in release.assets)
        {
            if (asset == null || asset.name != name) continue;
            if (selected != null) return null;
            selected = asset;
        }
        if (selected == null || selected.state != "uploaded" || selected.size < 1 ||
            selected.size > 32 * 1024 * 1024 || selected.browser_download_url != expectedUrl ||
            selected.digest == null || !selected.digest.StartsWith("sha256:", StringComparison.Ordinal) ||
            !UpdateOffer.IsDigest(selected.digest.Substring(7))) return null;
        return new UpdateOffer(version.ToString(3), expectedUrl, selected.digest.Substring(7), selected.size);
    }

    private static Version NewerVersion(Version current, Release release)
    {
        Version latest;
        if (release != null && !release.draft && !release.prerelease &&
            release.tag_name != null && release.tag_name.StartsWith("v", StringComparison.Ordinal) &&
            Version.TryParse(release.tag_name.Substring(1), out latest) &&
            latest.Build >= 0 && latest.Revision < 0 &&
            release.tag_name == "v" + latest.ToString(3) && latest > current)
            return latest;
        return null;
    }

    private static Release Parse(string payload)
    {
        try { return new JavaScriptSerializer().Deserialize<Release>(payload); }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public sealed class Release
    {
        public string tag_name { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public Asset[] assets { get; set; }
    }

    public sealed class Asset
    {
        public string name { get; set; }
        public string state { get; set; }
        public string browser_download_url { get; set; }
        public string digest { get; set; }
        public long size { get; set; }
    }
}

internal sealed class UpdateOffer
{
    public readonly string Version;
    public readonly string Url;
    public readonly string Digest;
    public readonly long Size;

    public UpdateOffer(string version, string url, string digest, long size)
    {
        Version = version;
        Url = url;
        Digest = digest;
        Size = size;
    }

    public static bool IsDigest(string value)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(value, @"\A[0-9a-fA-F]{64}\z");
    }
}
