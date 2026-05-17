using System;
using System.Collections.Generic;
using System.IO;

namespace Natsurainko.FluentLauncher.Services;

/// <summary>
/// Resolves Xenon-Fluent's launcher root and locates plausible existing
/// Mindustry data directories on the local machine.
/// </summary>
internal static class MindustryDataLocator
{
    /// <summary>
    /// The launcher's own root directory: <c>%UserProfile%\Documents\Xenon-Fluent</c>.
    /// Each instance lives in <c>{LauncherRoot}\versions\{InstanceId}\</c>.
    /// </summary>
    /// <remarks>
    /// We deliberately avoid <c>%AppData%</c> / <c>%LocalAppData%</c> because under MSIX
    /// the OS file-system redirector silently rewrites those paths into the package's
    /// <c>LocalCache\Roaming</c> sandbox — paths the launcher writes there are invisible
    /// to out-of-sandbox processes such as <c>javaw.exe</c>. <c>Documents</c> (a.k.a.
    /// <c>SpecialFolder.MyDocuments</c>) is not virtualized and the same physical path
    /// is seen by both packaged and unpackaged code, so the Java VM can read instance
    /// jars the launcher wrote.
    /// </remarks>
    public static string LauncherRoot
    {
        get
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "Xenon-Fluent");
        }
    }

    /// <summary>
    /// Ensure the launcher root exists. Safe to call repeatedly.
    /// Returns the path so callers can chain.
    /// </summary>
    public static string EnsureLauncherRoot()
    {
        var root = LauncherRoot;
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "versions"));
        return root;
    }

    /// <summary>
    /// Scans well-known locations for an existing data directory the user might
    /// want to register. The launcher root is always returned first (created on
    /// the fly if missing). Then any *existing* legacy Mindustry game folder is
    /// returned for users who want to import their game data.
    /// </summary>
    public static IEnumerable<string> ScanCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Always offer Xenon-Fluent's own root, creating it if needed.
        var root = EnsureLauncherRoot();
        if (seen.Add(Path.GetFullPath(root)))
            yield return root;

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        // 2. Existing Mindustry game folders the user may want to register too
        //    (only returned if they actually exist on disk — they're informational
        //    candidates, not the launcher's storage root).
        foreach (var path in EnumerateLegacy(appData, localAppData, userProfile, documents, desktop))
        {
            if (string.IsNullOrEmpty(path))
                continue;

            string normalized;
            try { normalized = Path.GetFullPath(path); }
            catch { continue; }

            if (!seen.Add(normalized))
                continue;

            if (Directory.Exists(normalized))
                yield return normalized;
        }
    }

    private static IEnumerable<string> EnumerateLegacy(
        string appData,
        string localAppData,
        string userProfile,
        string documents,
        string desktop)
    {
        if (!string.IsNullOrEmpty(appData))
            yield return Path.Combine(appData, "Mindustry");

        if (!string.IsNullOrEmpty(localAppData))
            yield return Path.Combine(localAppData, "Mindustry");

        if (!string.IsNullOrEmpty(userProfile))
            yield return Path.Combine(userProfile, ".local", "share", "Mindustry");

        if (!string.IsNullOrEmpty(documents))
        {
            yield return Path.Combine(documents, "Mindustry");
            yield return Path.Combine(documents, "Mindustry-data");
            yield return Path.Combine(documents, "Mindustry-saves");
        }

        if (!string.IsNullOrEmpty(desktop))
        {
            yield return Path.Combine(desktop, "Mindustry-data");
            yield return Path.Combine(desktop, "Mindustry-saves");
        }
    }
}
