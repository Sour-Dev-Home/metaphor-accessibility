namespace Metaphor.CpkExtract;

/// <summary>Why the tool refused a path. The message names the rule, never a local path, so it is safe to paste into
/// an issue.</summary>
public sealed class RefusedPathException(string message) : Exception(message);

/// <summary>Keeps everything the tool writes inside the repository's git-ignored <c>extracted/</c> folder (ground
/// rule 4: game content is never committed). Real paths are compared, so <c>..</c>, a drive or UNC path elsewhere, and a
/// symlink or junction that leads out are all refused.</summary>
public static class OutputGuard
{
    /// <summary>The folder name, at the repository root, that holds extracted game content.</summary>
    public const string ExtractedFolder = "extracted";

    /// <summary>The repository root above <paramref name="start"/>: the nearest folder holding both <c>.git</c> (a
    /// folder, or a file in a worktree) and <c>Metaphor.sln</c>.</summary>
    public static string FindRepositoryRoot(string start)
    {
        for (var dir = new DirectoryInfo(Path.GetFullPath(start)); dir is not null; dir = dir.Parent)
        {
            if ((Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git"))) &&
                File.Exists(Path.Combine(dir.FullName, "Metaphor.sln")))
            {
                return dir.FullName;
            }
        }
        throw new RefusedPathException("run this from inside the repository: no Metaphor.sln beside a .git was found above the current folder");
    }

    /// <summary>The full path of <paramref name="requested"/> when it lies inside <c>extracted/</c> under
    /// <paramref name="repositoryRoot"/> (<c>extracted/</c> itself counts); otherwise throws
    /// <see cref="RefusedPathException"/>. A relative path is read from <paramref name="currentDirectory"/>.</summary>
    public static string ResolveOutput(string repositoryRoot, string currentDirectory, string requested)
    {
        if (string.IsNullOrWhiteSpace(requested) || requested.Contains('\0'))
        {
            throw new RefusedPathException("--out needs a folder inside extracted/");
        }
        if (requested.Split('/', '\\').Contains(".."))
        {
            throw new RefusedPathException("--out must not contain '..'");
        }
        var root = Path.GetFullPath(Path.Combine(repositoryRoot, ExtractedFolder));
        var full = Path.GetFullPath(requested, currentDirectory);
        if (!IsInside(root, full))
        {
            throw new RefusedPathException($"--out must be inside the repository's {ExtractedFolder}/ folder");
        }
        RefuseLinks(root, full);
        return full;
    }

    /// <summary>Throws unless <paramref name="target"/> (a file or folder) is inside <paramref name="folder"/> and no
    /// existing part of the way there is a link.</summary>
    public static void EnsureInside(string folder, string target)
    {
        if (!IsInside(folder, target))
        {
            throw new RefusedPathException("a path in the archive would land outside the output folder");
        }
        RefuseLinks(folder, target);
    }

    /// <summary>The path inside an archive, as <c>DIR/NAME</c> with forward slashes: relative, no <c>..</c>, no empty,
    /// drive or stream-looking parts, no Windows device name (<c>NUL</c>, <c>CON</c>, <c>COM1</c>, …, with or without an
    /// extension) and none of the characters Windows can't put in a name (<c>&lt;&gt;"|?*</c>, control characters).
    /// Returns it normalised, or throws <see cref="RefusedPathException"/>.</summary>
    public static string ArchivePath(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        if (path.Length == 0 || path.Contains(':') ||
            parts.Any(p => p.Length == 0 || p is "." or ".." || p.TrimEnd('.', ' ').Length != p.Length))
        {
            throw new RefusedPathException("an archive path must be relative, like DIR/NAME, with no '..', ':' or empty parts");
        }
        if (path.Any(c => char.IsControl(c) || "<>\"|?*".Contains(c)))
        {
            throw new RefusedPathException("an archive path must not contain <, >, \", |, ?, * or a control character");
        }
        if (parts.Any(IsDeviceName))
        {
            throw new RefusedPathException("an archive path must not name a Windows device, such as NUL, CON or COM1");
        }
        return string.Join('/', parts);
    }

    // A Windows device name: the part before its first dot, trailing spaces dropped, is CON, PRN, AUX, NUL, COM0-9,
    // LPT0-9, or COM or LPT with a superscript digit (Windows 11 reserves those too).
    private static bool IsDeviceName(string part)
    {
        var stem = part.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                (char.IsAsciiDigit(stem[3]) || stem[3] is '¹' or '²' or '³'));
    }

    /// <summary>Writes <paramref name="bytes"/> to <paramref name="file"/> inside <paramref name="folder"/>, opening it
    /// only as a new file, so nothing that appeared after the checks (a file, or a link to one elsewhere) is written
    /// through. With <paramref name="force"/>, a plain file already there is deleted first; a link there is refused,
    /// never followed. The way there is checked again once the file is open, before any byte is written.</summary>
    public static void WriteNew(string folder, string file, byte[] bytes, bool force)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        EnsureInside(folder, file);
        if (force && File.Exists(file))
        {
            File.Delete(file);
        }
        using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        EnsureInside(folder, file);
        stream.Write(bytes);
    }

    private static bool IsInside(string folder, string path)
    {
        var relative = Path.GetRelativePath(folder, path);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !Path.IsPathRooted(relative);
    }

    // The folder and every existing step from it down to path must be a real folder or file, not a symlink or junction.
    private static void RefuseLinks(string folder, string path)
    {
        var steps = new List<string> { folder };
        var current = folder;
        foreach (var part in Path.GetRelativePath(folder, path).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
            {
                continue;
            }
            current = Path.Combine(current, part);
            steps.Add(current);
        }
        foreach (var step in steps)
        {
            FileSystemInfo info = Directory.Exists(step) ? new DirectoryInfo(step) : new FileInfo(step);
            if (info.Exists && (info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.LinkTarget is not null))
            {
                throw new RefusedPathException($"{ExtractedFolder}/ holds a link (symlink or junction) on the way to the output; remove it");
            }
        }
    }
}
