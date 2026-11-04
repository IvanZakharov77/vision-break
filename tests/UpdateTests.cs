using System;

internal static class UpdateTests
{
    public static void Run()
    {
        const string url = "https://github.com/IvanZakharov77/vision-break/releases/download/v1.3.0/VisionBreak-Setup-1.3.0.exe";
        string hash = new string('a', 64);
        string asset = "{\"name\":\"VisionBreak-Setup-1.3.0.exe\",\"state\":\"uploaded\",\"size\":123,\"digest\":\"sha256:" + hash +
            "\",\"browser_download_url\":\"" + url + "\"}";
        Check(asset, true);
        Check(asset.Replace("sha256:", "sha1:"), false);
        Check(asset.Replace("github.com", "example.com"), false);
        Check(asset.Replace("uploaded", "new"), false);
        Check(asset.Replace("1.3.0.exe", "1.4.0.exe"), false);
        Check(asset + "," + asset, false);
        Console.WriteLine("Trusted update metadata checks passed.");
    }

    private static void Check(string assets, bool expected)
    {
        var release = ReleaseCheck.FindUpdate(new Version(1, 2, 0), "{\"tag_name\":\"v1.3.0\",\"assets\":[" + assets + "]}");
        if ((release != null) != expected) throw new Exception("Unexpected asset validation");
    }
}
