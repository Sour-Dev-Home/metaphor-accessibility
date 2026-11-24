using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Metaphor.Core;
using Metaphor.Core.Logging;

namespace Metaphor.Features.Field;

/// <summary>One point of a field map's icon-point file (#652): its 2D position on the map, its kind and its index.
/// </summary>
/// <param name="X">Its 2D position across, in the file's own units (not world units).</param>
/// <param name="Y">Its 2D position down.</param>
/// <param name="Kind">Its kind; <see cref="MapIconPoints.MissionKind"/> for a field objective's marker.</param>
/// <param name="Index">For a marker, the row of <c>Mission_Field_Data.bin</c> it belongs to.</param>
public readonly record struct MapIconPoint(int X, int Y, int Kind, int Index);

/// <summary>A field map's icon-point file (#652), read offline from the player's own <c>base.cpk</c> on game build
/// 548ddc95… (Steam build 18330018) (docs/findings/objective-map-icon-points-static.md): a <c>0x14</c>-byte header
/// whose <c>u32</c> at <c>+0xc</c> is the entry count and at <c>+0x10</c> the first entry's offset, then
/// <c>0x34</c>-byte entries, each with a <c>u16</c> 2D position at <c>+0x4</c> and <c>+0x6</c>, its kind at
/// <c>+0xc</c> and its index at <c>+0x10</c>. A kind-4 point is a field objective's marker, and its index is the
/// objective table's row, the row the mission line already finds; that the game picks the marker so is
/// [NEEDS VERIFICATION]. The name at <c>+0x14</c> isn't read: only numbers are kept.</summary>
public static class MapIconPoints
{
    /// <summary>The header's size.</summary>
    public const int HeaderSize = 0x14;

    /// <summary>One entry's size.</summary>
    public const int EntrySize = 0x34;

    /// <summary>A field objective's marker.</summary>
    public const int MissionKind = 4;

    /// <summary>The largest file read: the game's biggest is a few kilobytes.</summary>
    public const int MaxFileSize = 256 * 1024;

    private const int CountAt = 0xc;
    private const int FirstAt = 0x10;
    private const int XAt = 0x4;
    private const int YAt = 0x6;
    private const int KindAt = 0xc;
    private const int IndexAt = 0x10;

    /// <summary>Where the file of field <c>f_&lt;a&gt;_&lt;b&gt;</c>'s variant <paramref name="variant"/> is in the
    /// archive, as the exe's <c>mip_%04d_%03d_%03d.dat</c> string names it.</summary>
    public static string ArchivePath(int a, int b, int variant) => string.Create(CultureInfo.InvariantCulture,
        $"COMMON/field/source/layout/map_icon_point/mip_{a:0000}_{b:000}_{variant:000}.dat");

    /// <summary>The field's name, <c>f_0001_001</c>, as <c>MapData.bin</c>'s file numbers give it.</summary>
    public static string FieldName(int a, int b) =>
        string.Create(CultureInfo.InvariantCulture, $"f_{a:0000}_{b:000}");

    /// <summary>The points in <paramref name="bytes"/>, in file order, or null when they aren't an icon-point file: too
    /// short for the header, a first entry inside it, or fewer bytes than the count needs.</summary>
    public static IReadOnlyList<MapIconPoint>? Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSize)
        {
            return null;
        }
        var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[CountAt..]);
        var first = BinaryPrimitives.ReadUInt32LittleEndian(bytes[FirstAt..]);
        if (first < HeaderSize || first > (uint)bytes.Length || count > (uint)(bytes.Length - (int)first) / EntrySize)
        {
            return null;
        }
        var points = new MapIconPoint[count];
        for (var i = 0; i < points.Length; i++)
        {
            var entry = bytes.Slice((int)first + (i * EntrySize), EntrySize);
            points[i] = new(
                BinaryPrimitives.ReadUInt16LittleEndian(entry[XAt..]), BinaryPrimitives.ReadUInt16LittleEndian(entry[YAt..]),
                BinaryPrimitives.ReadInt32LittleEndian(entry[KindAt..]), BinaryPrimitives.ReadInt32LittleEndian(entry[IndexAt..]));
        }
        return points;
    }

    /// <summary>The points as the Debug log writes them: <c>(x,y k=4 i=9)</c>, space-separated, or <c>none</c>.</summary>
    public static string Describe(IEnumerable<MapIconPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var text = new StringBuilder();
        foreach (var point in points)
        {
            text.Append(text.Length == 0 ? "" : " ").Append(CultureInfo.InvariantCulture,
                $"({point.X},{point.Y} k={point.Kind} i={point.Index})");
        }
        return text.Length == 0 ? "none" : text.ToString();
    }
}

/// <summary>One field's icon points as loaded (#652).</summary>
/// <param name="Points">The variant-0 file's points, or empty when the field has none.</param>
/// <param name="HasVariant">Whether the field also has a variant-1 file. What picks it is [NEEDS VERIFICATION], so only
/// variant 0 is used, and the log says when the other exists.</param>
public sealed record FieldIconPoints(IReadOnlyList<MapIconPoint> Points, bool HasVariant);

/// <summary>Each field's icon points, read from the archive on the thread pool the first time a field is asked for
/// (#652), and kept: no press waits on the archive. Thread-safe.</summary>
public sealed class MapIconPointSource
{
    private readonly Func<string, byte[]?> read;
    private readonly Action<string> log;
    private readonly Func<Func<FieldIconPoints?>, Task<FieldIconPoints?>> run;
    private readonly ConcurrentDictionary<(int A, int B), Task<FieldIconPoints?>> fields = new();

    /// <summary>A source that reads each file through <paramref name="read"/> (the archive path to its bytes, or null
    /// when it has none), writes a line per field read to <paramref name="log"/>, and runs each read with
    /// <paramref name="run"/> (on the thread pool unless given).</summary>
    public MapIconPointSource(
        Func<string, byte[]?> read, Action<string> log, Func<Func<FieldIconPoints?>, Task<FieldIconPoints?>>? run = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(log);
        this.read = read;
        this.log = log;
        this.run = run ?? Task.Run;
    }

    /// <summary>A source over the archive <paramref name="open"/> opens read-only, once per file.</summary>
    public static MapIconPointSource FromArchive(Func<Stream> open, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(open);
        return new(path =>
        {
            using var archive = open();
            return CpkArchive.ReadFile(archive, path, MapIconPoints.MaxFileSize);
        }, log);
    }

    /// <summary>Field <c>f_&lt;a&gt;_&lt;b&gt;</c>'s points once read, starting the read on the first ask; null while it
    /// runs, and when it failed (said once in the log).</summary>
    public FieldIconPoints? For(int a, int b)
    {
        var task = fields.GetOrAdd((a, b), field => run(() => Load(field.A, field.B)));
        return task.IsCompletedSuccessfully ? task.Result : null;
    }

    /// <summary>Whether field <c>f_&lt;a&gt;_&lt;b&gt;</c>'s read has been asked for and is still running.</summary>
    public bool Loading(int a, int b) => fields.TryGetValue((a, b), out var task) && !task.IsCompleted;

    [SuppressMessage("Design", "CA1031", Justification = "A failed read leaves the field without points; it must not reach the game.")]
    private FieldIconPoints? Load(int a, int b)
    {
        var name = MapIconPoints.FieldName(a, b);
        var started = StepTimer.Start();
        try
        {
            var bytes = read(MapIconPoints.ArchivePath(a, b, 0));
            var points = bytes is null ? [] : MapIconPoints.Parse(bytes);
            if (points is null)
            {
                log($"objective: {name}: icon points unreadable");
                return null;
            }
            var variant = read(MapIconPoints.ArchivePath(a, b, 1)) is not null;
            log(string.Create(CultureInfo.InvariantCulture,
                $"objective: {name}: {points.Count} icon points{(bytes is null ? " (no file)" : "")}{(variant ? ", variant 1 exists, not used" : "")} {StepTimer.Took(started)}"));
            return new(points, variant);
        }
        catch (Exception e)
        {
            log($"objective: {name}: can't read the archive: {e.GetType().Name}");
            return null;
        }
    }
}
