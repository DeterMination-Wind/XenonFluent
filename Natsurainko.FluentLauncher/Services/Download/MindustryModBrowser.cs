using Natsurainko.FluentLauncher.Services.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Natsurainko.FluentLauncher.Services.Download;

/// <summary>
/// Result row returned from a GitHub topic search.
/// </summary>
public sealed record MindustryModRepo(
    string FullName,
    string? Description,
    int StargazersCount,
    DateTimeOffset UpdatedAt,
    string HtmlUrl,
    string Owner,
    string Name,
    string? Language)
{
    public string UpdatedAtDisplay => UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd");
}

/// <summary>
/// Lightweight client for the GitHub Search and Releases APIs, scoped to the
/// <c>mindustry-mod</c> topic. Does not authenticate; the public limit of
/// 60 requests/hour applies.
/// </summary>
public sealed class MindustryModBrowser
{
    private readonly HttpClient _httpClient;

    public MindustryModBrowser(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Parameterless ctor so the VM can fall back to its own HttpClient when DI is not wired.
    public MindustryModBrowser() : this(new HttpClient())
    {
    }

    public async Task<List<MindustryModRepo>> SearchAsync(
        string? query,
        string? sort,
        CancellationToken ct = default)
    {
        // Always anchor on the topic; the user-supplied free-text is appended.
        var q = "topic:mindustry-mod";
        if (!string.IsNullOrWhiteSpace(query))
        {
            q += "+" + Uri.EscapeDataString(query.Trim());
        }

        var sortParam = sort switch
        {
            "updated" => "updated",
            _ => "stars",
        };

        var url = $"https://api.github.com/search/repositories?q={q}&sort={sortParam}&per_page=30";

        using var req = BuildJsonRequest(HttpMethod.Get, url);
        using var res = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);

        if (res.StatusCode == HttpStatusCode.Forbidden ||
            res.StatusCode == (HttpStatusCode)429)
        {
            throw new HttpRequestException("GitHub API rate limit exceeded");
        }

        res.EnsureSuccessStatusCode();

        await using var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var doc = await JsonSerializer.DeserializeAsync<SearchResponse>(stream, JsonOpts, ct)
            .ConfigureAwait(false);

        var list = new List<MindustryModRepo>();
        if (doc?.Items is null) return list;

        foreach (var i in doc.Items)
        {
            list.Add(new MindustryModRepo(
                FullName: i.FullName ?? string.Empty,
                Description: i.Description,
                StargazersCount: i.StargazersCount,
                UpdatedAt: i.UpdatedAt,
                HtmlUrl: i.HtmlUrl ?? string.Empty,
                Owner: i.Owner?.Login ?? string.Empty,
                Name: i.Name ?? string.Empty,
                Language: i.Language));
        }

        return list;
    }

    /// <summary>
    /// Resolves the latest GitHub release for the repo, picks the first
    /// <c>.jar</c> asset, and downloads it (through the gh.tinylake.top mirror)
    /// into the supplied <paramref name="destinationFolder"/>, reporting
    /// progress + speed.
    /// </summary>
    /// <param name="repo">Repo to fetch the latest release of.</param>
    /// <param name="destinationFolder">Target mods folder. Caller decides; usually
    /// <c>{instanceWorkingDir}\.data\Mindustry\mods</c> for per-instance isolation.</param>
    public async Task<string> DownloadLatestReleaseAsync(
        MindustryModRepo repo,
        string destinationFolder,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(repo.Owner) || string.IsNullOrEmpty(repo.Name))
            throw new ArgumentException("Repo owner/name missing", nameof(repo));
        if (string.IsNullOrWhiteSpace(destinationFolder))
            throw new ArgumentException("Destination folder required", nameof(destinationFolder));

        var url = $"https://api.github.com/repos/{repo.Owner}/{repo.Name}/releases/latest";

        using var req = BuildJsonRequest(HttpMethod.Get, url);
        using var res = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);

        if (res.StatusCode == HttpStatusCode.Forbidden ||
            res.StatusCode == (HttpStatusCode)429)
        {
            throw new HttpRequestException("GitHub API rate limit exceeded");
        }

        res.EnsureSuccessStatusCode();

        await using var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var release = await JsonSerializer.DeserializeAsync<ReleaseResponse>(stream, JsonOpts, ct)
            .ConfigureAwait(false);

        var asset = release?.Assets?.FirstOrDefault(a =>
            a.Name?.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) == true);
        if (asset is null || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
            throw new InvalidOperationException("No .jar asset found in latest release");

        var safeName = SanitizeFileName(asset.Name!);
        var filePath = Path.Combine(destinationFolder, safeName);

        var mirroredUrl = GhMirror.Rewrite(asset.BrowserDownloadUrl!);
        await StreamingDownload.DownloadAsync(_httpClient, mirroredUrl, filePath, progress, ct)
            .ConfigureAwait(false);

        return filePath;
    }

    private static HttpRequestMessage BuildJsonRequest(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Accept.Clear();
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        // Some GitHub endpoints reject requests without a UA. Use a stable identifier.
        req.Headers.UserAgent.Clear();
        req.Headers.UserAgent.ParseAdd(UserAgent);
        return req;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private const string UserAgent = "Xenon-Fluent";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private sealed class SearchResponse
    {
        [JsonPropertyName("items")] public List<RepoItem>? Items { get; set; }
    }

    private sealed class RepoItem
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("full_name")] public string? FullName { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("stargazers_count")] public int StargazersCount { get; set; }
        [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("language")] public string? Language { get; set; }
        [JsonPropertyName("owner")] public OwnerInfo? Owner { get; set; }
    }

    private sealed class OwnerInfo
    {
        [JsonPropertyName("login")] public string? Login { get; set; }
    }

    private sealed class ReleaseResponse
    {
        [JsonPropertyName("assets")] public List<AssetInfo>? Assets { get; set; }
    }

    private sealed class AssetInfo
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    }
}
