using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Natsurainko.FluentLauncher.Services.Launch;

/// <summary>
/// Downloads an Eclipse Temurin JDK build directly from the Adoptium API
/// and extracts it into <c>%LocalAppData%\Xenon-Fluent\Runtimes\temurin-{major}\</c>.
///
/// Adoptium API (latest GA binary):
///   https://api.adoptium.net/v3/binary/latest/{major}/ga/windows/{arch}/jdk/hotspot/normal/eclipse
/// </summary>
public sealed class TemurinDownloader
{
    private const string AdoptiumLatestBinaryTemplate =
        "https://api.adoptium.net/v3/binary/latest/{0}/ga/windows/{1}/jdk/hotspot/normal/eclipse";

    /// <summary>
    /// Recommended Temurin major version. JDK 17 covers the current Mindustry builds.
    /// </summary>
    public static int GetRecommendedMajor() => 17;

    /// <summary>
    /// Download and extract Temurin for the requested major version.
    /// </summary>
    /// <returns>Absolute path to <c>javaw.exe</c> inside the extracted runtime.</returns>
    public async Task<string> DownloadAsync(int majorVersion, IProgress<double>? progress, CancellationToken ct)
    {
        var arch = GetArch();
        var url = string.Format(AdoptiumLatestBinaryTemplate, majorVersion, arch);

        var runtimesRoot = GetRuntimesRoot();
        Directory.CreateDirectory(runtimesRoot);

        var zipPath = Path.Combine(runtimesRoot, $"temurin-{majorVersion}.zip");
        var extractRoot = Path.Combine(runtimesRoot, $"temurin-{majorVersion}");

        // Clean any previous attempt so a partial state never lingers.
        TryDelete(zipPath);
        TryDeleteDir(extractRoot);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Xenon-Fluent");

        // Download
        using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;

            using var srcStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var dstStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920]; // 80 KB
            long readTotal = 0;
            int read;
            var lastReport = DateTime.UtcNow;

            while ((read = await srcStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                await dstStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                readTotal += read;

                var now = DateTime.UtcNow;
                if (progress is not null && (now - lastReport).TotalMilliseconds >= 100)
                {
                    if (total.HasValue && total.Value > 0)
                    {
                        // Reserve the last 10% for extraction.
                        var p = (double)readTotal / total.Value * 0.9;
                        if (p < 0) p = 0;
                        if (p > 0.9) p = 0.9;
                        progress.Report(p);
                    }
                    lastReport = now;
                }
            }

            progress?.Report(0.9);
        }

        // Extract
        Directory.CreateDirectory(extractRoot);
        await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractRoot, overwriteFiles: true), ct).ConfigureAwait(false);

        // Cleanup zip cache
        TryDelete(zipPath);

        progress?.Report(1.0);

        var javaw = FindJavaw(extractRoot)
            ?? throw new InvalidOperationException("javaw.exe not found in extracted Temurin archive.");
        return javaw;
    }

    private static string GetArch() =>
        RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "aarch64" : "x64";

    private static string GetRuntimesRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Xenon-Fluent", "Runtimes");
    }

    private static string? FindJavaw(string extractRoot)
    {
        if (!Directory.Exists(extractRoot)) return null;

        // Adoptium archives unpack into a top-level dir like `jdk-17.0.10+7\`.
        foreach (var dir in Directory.EnumerateDirectories(extractRoot))
        {
            var candidate = Path.Combine(dir, "bin", "javaw.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        // Fallback: any javaw.exe under the extract root.
        return Directory.EnumerateFiles(extractRoot, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDir(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }
}
