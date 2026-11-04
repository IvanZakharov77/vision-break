using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

[assembly: System.Reflection.AssemblyVersion(ReleaseCheck.CurrentVersion)]
[assembly: System.Reflection.AssemblyFileVersion(ReleaseCheck.CurrentVersion)]

internal static class ReleaseCheck
{
    public const string CurrentVersion = "1.0.0";
    public const string ReleasePage = "https://github.com/IvanZakharov77/vision-break/releases/latest";
    private const string ReleaseApi = "https://api.github.com/repos/IvanZakharov77/vision-break/releases/latest";

    public static async Task<Version> CheckAsync()
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
                return FindNewerVersion(new Version(CurrentVersion), payload);
            }
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public static Version FindNewerVersion(Version current, string payload)
    {
        try
        {
            var release = new JavaScriptSerializer().Deserialize<Release>(payload);
            Version latest;
            if (release != null && !release.draft && !release.prerelease &&
                release.tag_name != null && release.tag_name.StartsWith("v", StringComparison.Ordinal) &&
                Version.TryParse(release.tag_name.Substring(1), out latest) &&
                latest.Build >= 0 && latest.Revision < 0 &&
                release.tag_name == "v" + latest.ToString(3) && latest > current)
                return latest;
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        return null;
    }

    public sealed class Release
    {
        public string tag_name { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
    }
}
