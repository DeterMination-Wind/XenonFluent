using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Natsurainko.FluentLauncher.Services.Network;

/// <summary>
/// Snapshot of a streaming download's progress, including instantaneous speed.
/// </summary>
public readonly record struct DownloadProgressInfo(long BytesReceived, long? TotalBytes, double BytesPerSecond)
{
    public double Percent => TotalBytes is > 0 ? BytesReceived / (double)TotalBytes.Value : 0;

    /// <summary>Human-friendly speed string ("1.23 MB/s").</summary>
    public string FormatSpeed()
    {
        var bps = BytesPerSecond;
        if (bps < 1024) return $"{bps:F0} B/s";
        if (bps < 1024 * 1024) return $"{bps / 1024:F1} KB/s";
        return $"{bps / 1024 / 1024:F2} MB/s";
    }

    public string FormatProgress()
    {
        if (TotalBytes is > 0)
            return $"{FormatBytes(BytesReceived)} / {FormatBytes(TotalBytes.Value)}";
        return FormatBytes(BytesReceived);
    }

    private static string FormatBytes(long b)
    {
        if (b < 1024) return $"{b} B";
        if (b < 1024 * 1024) return $"{b / 1024.0:F1} KB";
        if (b < 1024L * 1024 * 1024) return $"{b / 1024.0 / 1024:F2} MB";
        return $"{b / 1024.0 / 1024 / 1024:F2} GB";
    }
}

/// <summary>
/// HTTP streaming download with throttled progress + speed reporting.
/// Writes to a <c>.partial</c> sibling and atomically renames on success.
/// </summary>
public static class StreamingDownload
{
    public static async Task DownloadAsync(
        HttpClient http,
        string url,
        string destPath,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        var partial = destPath + ".partial";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("Xenon-Fluent");

        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();

        long? total = res.Content.Headers.ContentLength;

        await using (var src = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var dst = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long received = 0;
            var sw = Stopwatch.StartNew();
            long windowStartMs = 0;
            long windowStartBytes = 0;

            while (true)
            {
                var n = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                if (n == 0) break;

                await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                received += n;

                var nowMs = sw.ElapsedMilliseconds;
                if (nowMs - windowStartMs >= 200)
                {
                    var deltaMs = nowMs - windowStartMs;
                    var deltaBytes = received - windowStartBytes;
                    var bps = deltaMs > 0 ? deltaBytes * 1000.0 / deltaMs : 0;
                    progress?.Report(new DownloadProgressInfo(received, total, bps));
                    windowStartMs = nowMs;
                    windowStartBytes = received;
                }
            }

            // Final report
            progress?.Report(new DownloadProgressInfo(received, total, 0));
        }

        if (File.Exists(destPath)) File.Delete(destPath);
        File.Move(partial, destPath);
    }
}
