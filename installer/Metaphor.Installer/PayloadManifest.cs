using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Metaphor.Installer;

/// <summary>The exe's payload doesn't match the list of files it carries: a damaged download (#608).</summary>
internal sealed class PayloadDamagedException(string message) : Exception(message);

/// <summary>A link turned up in the folder the payload unpacks into, which only another program can have put there.</summary>
internal sealed class PayloadLinkException(string message) : Exception(message);

/// <summary>
/// The list of the payload's files and their SHA-256 values, built into the exe at release time
/// (scripts/release/installer-exe.ps1), in <c>sha256sum</c>'s format: per line, 64 lower-case hex digits, two spaces and
/// the file's path in the release zip, with forward slashes. Every path is a plain relative one (<see cref="IsSafePath"/>),
/// and exactly one names the installer, <c>MODFOLDER/install.ps1</c>. Anything else is <see cref="PayloadDamagedException"/>.
/// </summary>
internal sealed class PayloadManifest
{
    private static readonly Regex Line = new("^([0-9a-f]{64})  (.+)$", RegexOptions.CultureInvariant);

    // Letters, digits and a few marks, starting with a letter or digit and ending in neither a dot nor a space, which
    // Windows drops; nothing Windows reads specially.
    private static readonly Regex Segment = new("^[A-Za-z0-9](?:[A-Za-z0-9 ._()+-]*[A-Za-z0-9_()+-])?\\z", RegexOptions.CultureInvariant);

    // Windows' device names, which mean a device in any folder and with any extension.
    private static readonly Regex DeviceName = new("^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\\.|$)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, string> hashes;

    private PayloadManifest(Dictionary<string, string> hashes, string modFolder)
    {
        this.hashes = hashes;
        ModFolder = modFolder;
    }

    /// <summary>Every path, in ordinal order.</summary>
    public IReadOnlyList<string> Paths => [.. hashes.Keys.OrderBy(path => path, StringComparer.Ordinal)];

    /// <summary>The top folder that holds <c>install.ps1</c>: the mod's folder in the release zip.</summary>
    public string ModFolder { get; }

    public int Count => hashes.Count;

    /// <summary>The SHA-256 of PATH, matched exactly, or null when the list doesn't name it.</summary>
    public string? HashOf(string path) => hashes.TryGetValue(path, out var hash) ? hash : null;

    public static PayloadManifest Parse(string text)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        // Windows' file names ignore case, so two paths differing only in case would be one file.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }
            var match = Line.Match(line);
            if (!match.Success)
            {
                throw new PayloadDamagedException("a line of the file list isn't a SHA-256 and a path");
            }
            var path = match.Groups[2].Value;
            if (!IsSafePath(path))
            {
                throw new PayloadDamagedException($"the file list names an unsafe path: {path}");
            }
            if (!seen.Add(path))
            {
                throw new PayloadDamagedException($"the file list names {path} twice");
            }
            hashes.Add(path, match.Groups[1].Value);
        }
        var installers = hashes.Keys.Where(IsInstaller).ToList();
        if (installers.Count != 1)
        {
            throw new PayloadDamagedException("the file list doesn't name one installer in a mod folder");
        }
        return new PayloadManifest(hashes, installers[0].Substring(0, installers[0].IndexOf('/')));
    }

    private static bool IsInstaller(string path)
    {
        var segments = path.Split('/');
        return segments.Length == 2 && segments[1] == "install.ps1";
    }

    /// <summary>True for a relative path of plain names joined by forward slashes: no root, drive, stream, device name,
    /// <c>.</c> or <c>..</c>, empty part or trailing dot or space, so it can only name a file inside the folder.</summary>
    public static bool IsSafePath(string path) =>
        path.Split('/').All(segment => Segment.IsMatch(segment) && !DeviceName.IsMatch(segment));
}
