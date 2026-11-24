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
    /// drive or stream-looking parts. Returns it normalised, or throws <see cref="RefusedPathException"/>.</summary>
    public static string ArchivePath(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        if (path.Length == 0 || path.Contains('\0') || path.Contains(':') ||
            parts.Any(p => p.Length == 0 || p is "." or ".." || p.TrimEnd('.', ' ').Length != p.Length))
        {
            throw new RefusedPathException("an archive path must be relative, like DIR/NAME, with no '..', ':' or empty parts");
        }
        return string.Join('/', parts);
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
