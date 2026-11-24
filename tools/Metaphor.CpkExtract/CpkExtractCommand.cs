using System.Globalization;
using Metaphor.Core;

namespace Metaphor.CpkExtract;

/// <summary>The <c>list</c> and <c>extract</c> commands (#895). The archive is named by file name only, such as
/// <c>base.cpk</c>, and found in the game's folder, the one holding <c>METAPHOR_GAME_EXE</c>, so no absolute path is
/// typed or committed. The archive is opened read-only; nothing is written outside <c>extracted/</c>.</summary>
public static class CpkExtractCommand
{
    private const string Usage =
        "usage: cpk-extract list <cpk> [filter]\n" +
        "       cpk-extract extract <cpk> <path-in-archive>... --out extracted/<dir>\n" +
        "  <cpk> is a file name such as base.cpk, found beside the game exe named by METAPHOR_GAME_EXE.";

    /// <summary>Runs the command; returns the exit code (0 done, 1 a file couldn't be read, 2 usage or a refused
    /// path).</summary>
    public static int Run(string[] args, Func<string, string?> environment, string currentDirectory, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Length >= 2 && args[0] == "list" && args.Length <= 3)
            {
                return List(OpenArchive(args[1], environment), args.Length == 3 ? args[2] : null, output, error);
            }
            if (args.Length >= 3 && args[0] == "extract")
            {
                return Extract(args, environment, currentDirectory, output, error);
            }
            error.WriteLine(Usage);
            return 2;
        }
        catch (RefusedPathException refused)
        {
            error.WriteLine("refused: " + refused.Message);
            return 2;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Only the exception's kind: its message can hold a local path, and this output goes into issues.
            error.WriteLine($"could not read or write a file ({e.GetType().Name})");
            return 1;
        }
    }

    private static int List(FileStream archive, string? filter, TextWriter output, TextWriter error)
    {
        using (archive)
        {
            if (CpkArchive.List(archive) is not { } entries)
            {
                error.WriteLine("not a CPK archive this reads (the table of contents is missing, damaged or encrypted)");
                return 1;
            }
            foreach (var entry in entries.Where(e => filter is null || e.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                var packed = entry.ExtractSize != entry.Size
                    ? string.Create(CultureInfo.InvariantCulture, $"\t(compressed; {entry.ExtractSize} bytes unpacked, not extractable)")
                    : "";
                output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{entry.Size}\t{entry.Path}{packed}"));
            }
            return 0;
        }
    }

    private static int Extract(string[] args, Func<string, string?> environment, string currentDirectory, TextWriter output, TextWriter error)
    {
        string? outFolder = null;
        var paths = new List<string>();
        for (var i = 2; i < args.Length; i++)
        {
            if (args[i] == "--out")
            {
                if (i + 1 >= args.Length || outFolder is not null)
                {
                    error.WriteLine(Usage);
                    return 2;
                }
                outFolder = args[++i];
            }
            else
            {
                paths.Add(OutputGuard.ArchivePath(args[i]));
            }
        }
        if (outFolder is null || paths.Count == 0)
        {
            error.WriteLine(Usage);
            return 2;
        }
        // Every refusal happens before the archive is opened or anything is written.
        var root = OutputGuard.FindRepositoryRoot(currentDirectory);
        var folder = OutputGuard.ResolveOutput(root, currentDirectory, outFolder);
        var targets = paths.Select(p => (Archive: p, File: Path.GetFullPath(Path.Combine(folder, p)))).ToList();
        foreach (var target in targets)
        {
            OutputGuard.EnsureInside(folder, target.File);
        }
        using var archive = OpenArchive(args[1], environment);
        var failed = 0;
        foreach (var (path, file) in targets)
        {
            var found = CpkArchive.Lookup(archive, path, int.MaxValue);
            if (found.Bytes is not { } bytes)
            {
                error.WriteLine($"{path}: {Describe(found.Status)}");
                failed++;
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            OutputGuard.EnsureInside(folder, file);
            File.WriteAllBytes(file, bytes);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{bytes.Length}\t{path}"));
        }
        return failed == 0 ? 0 : 1;
    }

    private static string Describe(CpkFileStatus status) => status switch
    {
        CpkFileStatus.NotFound => "not in the archive",
        CpkFileStatus.Packed => "compressed, which this doesn't read",
        CpkFileStatus.TooBig => "too big",
        _ => "damaged, or an archive this doesn't read",
    };

    // The archive by file name in the game's folder, opened for reading only.
    private static FileStream OpenArchive(string name, Func<string, string?> environment)
    {
        if (name.Length == 0 || name != Path.GetFileName(name) || !name.EndsWith(".cpk", StringComparison.OrdinalIgnoreCase))
        {
            throw new RefusedPathException("name the archive by file name only, such as base.cpk; it is found beside the game exe");
        }
        if (environment("METAPHOR_GAME_EXE") is not { Length: > 0 } exe)
        {
            throw new RefusedPathException("set METAPHOR_GAME_EXE to the game exe, as the other dev scripts do; the archive is found beside it");
        }
        var folder = Path.GetDirectoryName(Path.GetFullPath(exe));
        var file = folder is null ? null : Path.Combine(folder, name);
        if (file is null || !File.Exists(file))
        {
            throw new RefusedPathException($"{name} isn't beside the game exe named by METAPHOR_GAME_EXE");
        }
        return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }
}
