using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Logging;

namespace Metaphor.Core.Navigation;

/// <summary>Field maps' walk graphs, read lazily from the game's archive (#179): the first <see cref="Get"/> for a map
/// starts reading its <c>.NAV</c> file on the thread pool and returns null; later calls return the graph once it has
/// loaded. Read-only: the archive is opened, read and closed for each file. The last <see cref="Capacity"/> maps used
/// are kept, and one that couldn't be read is remembered as missing, so a map is read at most once while kept.
/// Callers name the file's two numbers, from the map table (<see cref="NavMapFiles"/>, #741).
/// </summary>
public sealed class NavMeshSource
{
    /// <summary>Where the game keeps the files in its archive.</summary>
    public const string Folder = "COMMON/model/field/navi";

    private readonly Func<Stream> open;
    private readonly Action<string> log;
    private readonly Dictionary<string, Task<WalkGraph?>> loads = [];
    private readonly LinkedList<string> used = [];
    private readonly Lock gate = new();

    /// <summary>A source that opens the archive with <paramref name="open"/> and tells <paramref name="log"/> what came
    /// of each read: a line naming the file, never its folder on disk.</summary>
    public NavMeshSource(Func<Stream> open, Action<string> log, int capacity = 4)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(log);
        this.open = open;
        this.log = log;
        Capacity = Math.Max(1, capacity);
    }

    /// <summary>How many maps are kept.</summary>
    public int Capacity { get; }

    /// <summary>The archive path of map file <c>f_&lt;a&gt;_&lt;b&gt;.NAV</c>, as the game names them
    /// (<c>f_0000_014.NAV</c>), or null when <paramref name="a"/> isn't 0 to 9999 or <paramref name="b"/> 0 to 999.
    /// </summary>
    public static string? ArchivePath(int a, int b) => a is >= 0 and <= 9999 && b is >= 0 and <= 999
        ? string.Create(CultureInfo.InvariantCulture, $"{Folder}/f_{a:D4}_{b:D3}.NAV")
        : null;

    /// <summary>Map <c>f_&lt;a&gt;_&lt;b&gt;</c>'s graph if it has loaded; otherwise null, starting its read if none
    /// has started. Null too when the file is missing or unreadable. Never blocks on the archive.</summary>
    public WalkGraph? Get(int a, int b)
    {
        var load = Load(a, b);
        return load is { IsCompletedSuccessfully: true } ? load.Result : null;
    }

    /// <summary>The read of map <c>f_&lt;a&gt;_&lt;b&gt;</c>, started if it hasn't been, or null when the numbers are
    /// out of range. Its result is the graph, or null when the file couldn't be read.</summary>
    public Task<WalkGraph?>? Load(int a, int b)
    {
        if (ArchivePath(a, b) is not { } path)
        {
            return null;
        }
        lock (gate)
        {
            if (loads.TryGetValue(path, out var load))
            {
                used.Remove(path);
                used.AddFirst(path);
                return load;
            }
            load = Task.Run(() => Read(path));
            loads[path] = load;
            used.AddFirst(path);
            while (used.Count > Capacity)
            {
                loads.Remove(used.Last!.Value);
                used.RemoveLast();
            }
            return load;
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "A failed read leaves the map without a graph; it must not reach the game.")]
    private WalkGraph? Read(string path)
    {
        var name = path[(Folder.Length + 1)..];
        var started = StepTimer.Start();
        try
        {
            using var archive = open();
            var found = CpkArchive.Lookup(archive, path, NavMeshFile.MaxFileSize);
            if (found.Bytes is null || NavMeshFile.Parse(found.Bytes) is not { } file)
            {
                log($"navmesh: {name}: {Why(found.Status)}");
                return null;
            }
            log(string.Create(CultureInfo.InvariantCulture,
                $"navmesh: {name}: {file.Tiles.Count} tiles read, {file.FailedTiles} skipped, {StepTimer.Took(started)}"));
            return new WalkGraph(file);
        }
        catch (Exception e)
        {
            log($"navmesh: can't read {name}: {e.GetType().Name}");
            return null;
        }
    }

    /// <summary>Why a file looked up in the archive gave no data (#741): the log line's reason, so a session tells a
    /// wrong name from a file that is there but won't read. <see cref="CpkFileStatus.Read"/> means it was read and didn't
    /// parse.</summary>
    internal static string Why(CpkFileStatus status) => status switch
    {
        CpkFileStatus.NotFound => "not in the archive",
        CpkFileStatus.Packed => "packed in the archive",
        CpkFileStatus.TooBig => "over the size limit",
        CpkFileStatus.Unreadable => "archive unreadable",
        _ => "doesn't parse",
    };
}
