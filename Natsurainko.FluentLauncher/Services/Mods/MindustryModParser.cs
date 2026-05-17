using Nrk.FluentCore.GameManagement.Mods;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Natsurainko.FluentLauncher.Services.Mods;

/// <summary>
/// Reads Mindustry mod metadata out of a packaged mod (<c>.jar</c> / <c>.zip</c>
/// archive). Mindustry mods declare themselves at the archive root via either
/// <c>mod.json</c> (strict JSON) or <c>mod.hjson</c> (Hjson — JSON with comments,
/// unquoted keys, optional commas, and triple-quoted multiline strings).
///
/// Rather than pulling in a full Hjson dependency, we use tolerant regex
/// extraction for the small fixed set of fields the launcher cares about
/// (name / displayName / description / version / author[s]). That handles
/// every well-formed Mindustry mod we've seen in the wild plus the loose
/// cases (line/block comments, unquoted scalars, triple-quoted descriptions).
/// </summary>
public static class MindustryModParser
{
    public static bool TryParse(string archivePath, out MinecraftMod? mod)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);

            ZipArchiveEntry? entry = null;
            foreach (var e in archive.Entries)
            {
                // Mindustry only looks for these at the archive root.
                var n = e.FullName;
                if (n.Equals("mod.json", StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("mod.hjson", StringComparison.OrdinalIgnoreCase))
                {
                    entry = e;
                    break;
                }
            }

            if (entry is null)
            {
                mod = null;
                return false;
            }

            string raw;
            using (var stream = entry.Open())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                raw = reader.ReadToEnd();

            var cleaned = StripComments(raw);

            var ext = Path.GetExtension(archivePath);
            mod = new MinecraftMod
            {
                AbsolutePath = archivePath,
                IsEnabled = ext.Equals(".jar", StringComparison.OrdinalIgnoreCase)
                         || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase),
                DisplayName = Extract(cleaned, "displayName")
                              ?? Extract(cleaned, "name")
                              ?? Path.GetFileNameWithoutExtension(archivePath),
                Description = Extract(cleaned, "description"),
                Version = Extract(cleaned, "version"),
                Authors = ExtractAuthors(cleaned),
            };
            return true;
        }
        catch
        {
            mod = null;
            return false;
        }
    }

    /// <summary>
    /// Strip <c>//</c> line and <c>/* */</c> block comments. Doesn't try to be
    /// string-aware — Mindustry mods don't put <c>//</c> inside string literals
    /// in practice, and the worst case is a missed extraction → fallback to
    /// file name, never a crash.
    /// </summary>
    private static string StripComments(string s)
    {
        s = Regex.Replace(s, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        s = Regex.Replace(s, @"(?<!:)//[^\r\n]*", string.Empty);
        return s;
    }

    private static string? Extract(string text, string key)
    {
        var keyPattern = $@"(?:""{Regex.Escape(key)}""|\b{Regex.Escape(key)}\b)\s*:\s*";

        // Hjson triple-quoted multiline string.
        var triple = Regex.Match(text, keyPattern + @"'''(.*?)'''", RegexOptions.Singleline);
        if (triple.Success) return triple.Groups[1].Value.Trim();

        // Standard JSON double-quoted string.
        var quoted = Regex.Match(text, keyPattern + @"""((?:[^""\\]|\\.)*)""");
        if (quoted.Success) return JsonUnescape(quoted.Groups[1].Value);

        // Unquoted scalar (Hjson) — read up to comma / brace / newline.
        var unquoted = Regex.Match(text, keyPattern + @"([^\s,\}\r\n]+)");
        if (unquoted.Success) return unquoted.Groups[1].Value.Trim();

        return null;
    }

    private static string[]? ExtractAuthors(string text)
    {
        // Singular: author: "..."
        var single = Extract(text, "author");
        if (!string.IsNullOrWhiteSpace(single))
            return new[] { single };

        // Plural: authors: [ "a", "b" ]
        var arrayMatch = Regex.Match(text, @"(?:""authors""|\bauthors\b)\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
        if (!arrayMatch.Success) return null;

        var list = new List<string>();
        foreach (Match m in Regex.Matches(arrayMatch.Groups[1].Value, @"""((?:[^""\\]|\\.)*)"""))
            list.Add(JsonUnescape(m.Groups[1].Value));

        return list.Count == 0 ? null : list.ToArray();
    }

    private static string JsonUnescape(string s) => s
        .Replace(@"\""", "\"")
        .Replace(@"\\", "\\")
        .Replace(@"\n", "\n")
        .Replace(@"\r", "\r")
        .Replace(@"\t", "\t");
}
