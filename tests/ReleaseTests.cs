using System;

internal static class ReleaseTests
{
    public static void Run()
    {
        var current = new Version(1, 0, 0);
        Check(current, "{\"tag_name\":\"v1.10.0\"}", new Version(1, 10, 0));
        Check(current, "{\"tag_name\":\"v1.0.0\"}", null);
        Check(current, "{\"tag_name\":\"v0.9.9\"}", null);
        Check(current, "{\"tag_name\":\"v2.0.0-beta.1\"}", null);
        Check(current, "{\"tag_name\":\"v2.0.0\",\"prerelease\":true}", null);
        Check(current, "{\"tag_name\":\"v2.0.0\",\"draft\":true}", null);
        Check(current, "{\"tag_name\":\"v2.0.0.1\"}", null);
        Check(current, "{\"message\":\"Not Found\"}", null);
        Check(current, "not JSON", null);
        Console.WriteLine("9 release checks passed.");
    }

    private static void Check(Version current, string payload, Version expected)
    {
        Version actual = ReleaseCheck.FindNewerVersion(current, payload);
        if (!object.Equals(actual, expected)) throw new Exception("Unexpected version for " + payload);
    }
}
