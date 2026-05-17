using Natsurainko.FluentLauncher.Services.Network;
using Nrk.FluentCore.GameManagement.Installer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Natsurainko.FluentLauncher.Services.Download;

/// <summary>
/// Mindustry distribution source.
/// </summary>
public enum MindustrySource
{
    /// <summary>Official upstream: Anuken/Mindustry.</summary>
    Mindustry = 0,
    /// <summary>MindustryX fork.</summary>
    MindustryX = 1,
    /// <summary>CN-ARC-Mindustry community fork.</summary>
    CnArc = 2,
    /// <summary>Foo's client.</summary>
    Foo = 3,
}

/// <summary>
/// Pulls real Mindustry releases from one of several GitHub repos and adapts
/// them to FluentCore's <see cref="VersionManifestItem"/> shape so the existing
/// download UI can stay mostly intact while sourcing Mindustry data instead of
/// Mojang's version_manifest_v2.json.
/// </summary>
public sealed class MindustryReleaseService(HttpClient httpClient)
{
    private const string UserAgent = "Xenon-Fluent";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Resolve a <see cref="MindustrySource"/> to its GitHub <c>owner/repo</c> slug.</summary>
    public static string GetRepo(MindustrySource source) => source switch
    {
        MindustrySource.Mindustry  => "Anuken/Mindustry",
        MindustrySource.MindustryX => "TinyLake/MindustryX",
        MindustrySource.CnArc      => "BlackDeluxeCat/CN-ARC",
        MindustrySource.Foo        => "mindustry-antigrief/mindustry-foo-client",
        _ => "Anuken/Mindustry"
    };

    /// <summary>Display label for the source (matches the i18n dropdown order).</summary>
    public static string GetDisplayName(MindustrySource source) => source switch
    {
        MindustrySource.Mindustry  => "Mindustry",
        MindustrySource.MindustryX => "MindustryX",
        MindustrySource.CnArc      => "CN-ARC-Mindustry",
        MindustrySource.Foo        => "Foo",
        _ => "Mindustry"
    };

    /// <summary>Map a 0-based UI dropdown index to a <see cref="MindustrySource"/>.</summary>
    public static MindustrySource SourceFromIndex(int index) => index switch
    {
        0 => MindustrySource.Mindustry,
        1 => MindustrySource.MindustryX,
        2 => MindustrySource.CnArc,
        3 => MindustrySource.Foo,
        _ => MindustrySource.Mindustry
    };

    public Task<List<VersionManifestItem>> GetReleasesAsync(CancellationToken ct = default)
        => GetReleasesAsync(MindustrySource.Mindustry, ct);

    public async Task<List<VersionManifestItem>> GetReleasesAsync(MindustrySource source, CancellationToken ct = default)
    {
        var repo = GetRepo(source);
        var url = $"https://api.github.com/repos/{repo}/releases?per_page=50";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd(UserAgent);
        req.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var res = await httpClient.SendAsync(req, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();

        await using var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, JsonOpts, ct)
            .ConfigureAwait(false);

        var list = new List<VersionManifestItem>();
        if (releases is null) return list;

        foreach (var r in releases)
        {
            var asset = PickClientJar(source, r.Assets);

            if (asset is null || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
                continue;

            var time = r.PublishedAt?.ToString("o") ?? string.Empty;

            list.Add(new VersionManifestItem
            {
                Id = !string.IsNullOrEmpty(r.TagName) ? r.TagName : (r.Name ?? string.Empty),
                Type = r.Prerelease ? "snapshot" : "release",
                // Route the asset through the gh.tinylake.top mirror; api.github.com
                // (the metadata above) stays direct because most mirrors don't proxy /api/.
                Url = GhMirror.Rewrite(asset.BrowserDownloadUrl!),
                Time = time,
                ReleaseTime = time,
            });
        }

        return list;
    }

    private static Asset? PickClientJar(MindustrySource source, List<Asset>? assets)
    {
        if (assets is null) return null;

        static bool IsExcluded(string name) =>
            name.Contains("server", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("dependencies", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("sources", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("javadoc", StringComparison.OrdinalIgnoreCase);

        // Helper: any .jar that isn't a server/dependencies/sources/javadoc artifact.
        Asset? AnyClientJar() => assets.FirstOrDefault(a =>
            !string.IsNullOrEmpty(a.Name) &&
            a.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) &&
            !IsExcluded(a.Name));

        return source switch
        {
            // Anuken/Mindustry: prefer Mindustry.jar; rejects dependencies.jar (~11MB,
            // no Main-Class) and server-release.jar (~17MB, headless).
            MindustrySource.Mindustry => assets.FirstOrDefault(a =>
                a.Name?.Equals("Mindustry.jar", StringComparison.OrdinalIgnoreCase) == true)
                ?? AnyClientJar(),

            // TinyLake/MindustryX: MindustryX-desktop.jar / desktop.jar preferred
            MindustrySource.MindustryX => assets.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.Name) &&
                a.Name.Contains("desktop", StringComparison.OrdinalIgnoreCase) &&
                a.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) &&
                !IsExcluded(a.Name))
                ?? AnyClientJar(),

            // CN-ARC-Mindustry: ships its own variant; fall back to any client jar
            MindustrySource.CnArc => AnyClientJar(),

            // Foo's client: typically client.jar
            MindustrySource.Foo => assets.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.Name) &&
                a.Name.Equals("client.jar", StringComparison.OrdinalIgnoreCase))
                ?? AnyClientJar(),

            _ => AnyClientJar()
        };
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }
        [JsonPropertyName("assets")] public List<Asset>? Assets { get; set; }
    }

    private sealed class Asset
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    }
}
