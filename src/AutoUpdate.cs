using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

internal static class AutoUpdate
{
    public static readonly string Profile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VisionBreak");

    private static string InstalledExe { get { return Path.Combine(Profile, "VisionBreak.exe"); } }
    private static string RunningExe { get { return Assembly.GetExecutingAssembly().Location; } }
    private static string Folder(string version) { return Path.Combine(Profile, "updates", version); }
    private static string Package(string version) { return Path.Combine(Folder(version), "setup.exe"); }
    private static string Attempt(string version) { return Path.Combine(Folder(version), "attempted"); }
    private static string Helper(string version) { return Path.Combine(Folder(version), "previous.exe"); }

    public static bool IsInstalled
    {
        get
        {
            return string.Equals(RunningExe, InstalledExe, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(Profile, "unins000.exe"));
        }
    }

    public static async Task<UpdateOffer> StageAsync()
    {
        try
        {
            UpdateOffer offer = await ReleaseCheck.CheckAsync().ConfigureAwait(false);
            if (offer == null || File.Exists(Attempt(offer.Version))) return null;
            Directory.CreateDirectory(Folder(offer.Version));
            if (Matches(Package(offer.Version), offer.Digest)) return offer;
            string partial = Package(offer.Version) + ".part";
            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
                using (var client = new HttpClient(handler))
                {
                    if (!await Download(client, new Uri(offer.Url), partial, offer.Size, timeout.Token).ConfigureAwait(false))
                        return null;
                }
                if (!Matches(partial, offer.Digest)) return null;
                if (File.Exists(Package(offer.Version)))
                    File.Replace(partial, Package(offer.Version), null);
                else
                    File.Move(partial, Package(offer.Version));
                return offer;
            }
            finally { File.Delete(partial); }
        }
        catch (Exception e) { if (!Handled(e)) throw; Debug.WriteLine(e); }
        return null;
    }

    private static async Task<bool> Download(HttpClient client, Uri uri, string destination, long size, CancellationToken token)
    {
        for (int redirect = 0; redirect < 4; redirect++)
        {
            if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                (uri.Host != "github.com" && uri.Host != "release-assets.githubusercontent.com" &&
                 uri.Host != "objects.githubusercontent.com")) return false;

            using (var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
            {
                int status = (int)response.StatusCode;
                if (status >= 300 && status < 400 && response.Headers.Location != null)
                {
                    uri = new Uri(uri, response.Headers.Location);
                    continue;
                }
                if (!response.IsSuccessStatusCode) return false;
                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    byte[] buffer = new byte[81920];
                    long total = 0;
                    int read;
                    // Disposing the response stream also interrupts a stalled read on .NET Framework.
                    using (token.Register(input.Dispose))
                    {
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                        {
                            total += read;
                            if (total > size) return false;
                            await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                        }
                    }
                    output.Flush(true);
                    return total == size;
                }
            }
        }
        return false;
    }

    public static void Launch(UpdateOffer offer)
    {
        try
        {
            File.Copy(RunningExe, Helper(offer.Version), true);
            using (File.Open(Attempt(offer.Version), FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            Start(Helper(offer.Version), "--apply-update " + offer.Version + " " + offer.Digest).Dispose();
        }
        catch (Exception e) { if (!Handled(e)) throw; Debug.WriteLine(e); }
    }

    public static void Apply(string version, string digest)
    {
        Version candidate;
        if (!Version.TryParse(version, out candidate) || candidate.Build < 0 || candidate.Revision >= 0 ||
            version != candidate.ToString(3) || candidate <= new Version(ReleaseCheck.CurrentVersion) ||
            !UpdateOffer.IsDigest(digest) ||
            !string.Equals(RunningExe, Helper(version), StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Attempt(version))) return;

        bool attempted = false;
        bool success = false;
        try
        {
            using (var package = File.Open(Package(version), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (!Matches(package, digest)) return;
                using (var installer = Start(Package(version), "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /VBUPDATE"))
                {
                    attempted = true;
                    installer.WaitForExit();
                    success = installer.ExitCode == 0 &&
                        AssemblyName.GetAssemblyName(InstalledExe).Version.ToString(3) == version;
                }
            }
        }
        catch (Exception e) { if (!Handled(e)) throw; Debug.WriteLine(e); }
        finally
        {
            if (attempted)
            {
                try
                {
                    if (!success && (!File.Exists(InstalledExe) ||
                        AssemblyName.GetAssemblyName(InstalledExe).Version.ToString(3) != ReleaseCheck.CurrentVersion))
                        File.Copy(RunningExe, InstalledExe, true);
                    Start(InstalledExe, "").Dispose();
                }
                catch (Exception e) { if (!Handled(e)) throw; Debug.WriteLine(e); }
            }
        }
    }

    private static bool Handled(Exception e)
    {
        return e is IOException || e is UnauthorizedAccessException || e is HttpRequestException ||
            e is OperationCanceledException || e is InvalidOperationException ||
            e is System.ComponentModel.Win32Exception || e is BadImageFormatException;
    }

    private static Process Start(string file, string arguments)
    {
        return Process.Start(new ProcessStartInfo(file, arguments) {
            WorkingDirectory = Profile, UseShellExecute = false, CreateNoWindow = true
        });
    }

    private static bool Matches(string path, string digest)
    {
        if (!File.Exists(path)) return false;
        using (var file = File.OpenRead(path)) return Matches(file, digest);
    }

    private static bool Matches(Stream file, string digest)
    {
        using (var sha = SHA256.Create())
            return string.Equals(BitConverter.ToString(sha.ComputeHash(file)).Replace("-", ""),
                digest, StringComparison.OrdinalIgnoreCase);
    }
}
