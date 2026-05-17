namespace Natsurainko.FluentLauncher.Services.Download;

/// <summary>
/// Mindustry rebrand: minimal install configuration for the new Mindustry-only
/// install pipeline. Replaces <c>InstanceInstallConfig</c> + loaders + Modrinth
/// for downloading and registering a single Mindustry client jar.
/// </summary>
public sealed class MindustryInstallConfig
{
    /// <summary>Folder name under <c>versions/</c>; also the FluentCore InstanceId.</summary>
    public required string InstanceId { get; init; }

    /// <summary>Direct URL to the Mindustry desktop .jar (a GitHub release asset).</summary>
    public required string DownloadUrl { get; init; }

    /// <summary>Display version (e.g. <c>v157.4</c>) shown in UI; usually the GitHub tag.</summary>
    public required string DisplayVersion { get; init; }

    /// <summary>Optional source label for UI (e.g. <c>"Mindustry"</c>, <c>"MindustryX"</c>). Not used in path logic.</summary>
    public string? Source { get; init; }
}
