using System;

namespace Natsurainko.FluentLauncher.Services.Network;

/// <summary>
/// Rewrites GitHub asset / raw / blob URLs through the gh.tinylake.top mirror.
/// API metadata (api.github.com) is left untouched: most ghproxy-style mirrors
/// only proxy raw file downloads, not the JSON API.
/// </summary>
public static class GhMirror
{
    /// <summary>Mirror base, with trailing slash so concatenation is correct.</summary>
    public const string BaseUrl = "https://gh.tinylake.top/";

    /// <summary>
    /// If <paramref name="url"/> points at a GitHub binary host, prepend the
    /// mirror; otherwise return it unchanged. Idempotent.
    /// </summary>
    public static string Rewrite(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        if (url.StartsWith(BaseUrl, StringComparison.OrdinalIgnoreCase)) return url;

        // Binary download hosts — safe to mirror.
        if (url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://raw.githubusercontent.com/", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://objects.githubusercontent.com/", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://codeload.github.com/", StringComparison.OrdinalIgnoreCase))
        {
            return BaseUrl + url;
        }

        // api.github.com kept direct: most mirrors return a redirect/forbidden for /api/.
        return url;
    }
}
