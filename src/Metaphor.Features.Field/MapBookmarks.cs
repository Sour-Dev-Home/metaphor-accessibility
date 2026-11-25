using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace Metaphor.Features.Field;

/// <summary>One bookmarked spot on a map (#877): its number on that map, from 1, and its world position, as
/// <see cref="FieldUnit"/>'s.</summary>
public sealed record MapBookmark(int Number, float X, float Y, float Z);

/// <summary>The player's map bookmarks (#877, the maintainer's decision of 2026-11-24): kept per install, per map id, in
/// <see cref="FileName"/> in the mod's config folder, positions as numbers only. Nothing is written to the game. The
/// file is <c>{"maps": {"&lt;map id&gt;": [{"n": 1, "x": 0, "y": 0, "z": 0}]}}</c>; a file of any other shape is left
/// unread and the store starts empty, saying why. Safe to call from any thread.</summary>
public sealed class MapBookmarks
{
    /// <summary>The file's name in the config folder.</summary>
    public const string FileName = "map-bookmarks.json";

    /// <summary>The most bookmarks one map keeps.</summary>
    public const int MaxPerMap = 99;

    /// <summary>The largest file read, in bytes.</summary>
    public const int MaxSize = 256 * 1024;

    private readonly Lock gate = new();
    private readonly Dictionary<int, List<MapBookmark>> maps = [];
    private readonly Action<string> save;

    /// <summary>A store holding what <paramref name="text"/> holds (none when null) that hands its whole file to
    /// <paramref name="save"/> after each change; <paramref name="problem"/> hears why a file was left unread.</summary>
    public MapBookmarks(string? text, Action<string> save, Action<string>? problem = null)
    {
        ArgumentNullException.ThrowIfNull(save);
        this.save = save;
        if (text is not null && !Read(text))
        {
            maps.Clear();
            problem?.Invoke("map bookmarks: the file isn't a bookmarks file, so it was left unread");
        }
    }

    /// <summary>The store in <paramref name="folder"/>'s <see cref="FileName"/>, read once now and written off the
    /// caller's thread after each change; with no folder, a store kept for this session only.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A file that can't be read or written must never stop the game.")]
    public static MapBookmarks Load(string? folder, Action<string> problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        if (folder is null)
        {
            return new MapBookmarks(null, _ => { });
        }
        var path = Path.Combine(folder, FileName);
        string? text = null;
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length <= MaxSize)
            {
                text = File.ReadAllText(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problem("map bookmarks: couldn't read the file: " + exception.GetType().Name);
        }
        var writing = new Lock();
        return new MapBookmarks(text, json => Task.Run(() =>
        {
            try
            {
                lock (writing)
                {
                    var temporary = path + ".tmp";
                    File.WriteAllText(temporary, json);
                    File.Move(temporary, path, overwrite: true);
                }
            }
            catch (Exception exception)
            {
                problem("map bookmarks: couldn't save the file: " + exception.GetType().Name);
            }
        }), problem);
    }

    /// <summary>Map <paramref name="map"/>'s bookmarks, by number.</summary>
    public IReadOnlyList<MapBookmark> On(int map)
    {
        lock (gate)
        {
            return maps.TryGetValue(map, out var marks) ? [.. marks] : [];
        }
    }

    /// <summary>Bookmarks (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) on map
    /// <paramref name="map"/> under its next number and saves the file; null when the map already holds
    /// <see cref="MaxPerMap"/>.</summary>
    public MapBookmark? Add(int map, float x, float y, float z)
    {
        MapBookmark mark;
        string json;
        lock (gate)
        {
            if (!maps.TryGetValue(map, out var marks))
            {
                marks = [];
                maps[map] = marks;
            }
            if (marks.Count >= MaxPerMap)
            {
                return null;
            }
            mark = new MapBookmark(marks.Count == 0 ? 1 : marks.Max(m => m.Number) + 1, x, y, z);
            marks.Add(mark);
            json = Json();
        }
        save(json);
        return mark;
    }

    private string Json()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("maps");
            foreach (var (map, marks) in maps.OrderBy(pair => pair.Key))
            {
                writer.WriteStartArray(map.ToString(CultureInfo.InvariantCulture));
                foreach (var mark in marks)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("n", mark.Number);
                    writer.WriteNumber("x", mark.X);
                    writer.WriteNumber("y", mark.Y);
                    writer.WriteNumber("z", mark.Z);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    // The file's bookmarks into maps: false when it isn't of the shape above.
    private bool Read(string text)
    {
        if (text.Length > MaxSize)
        {
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("maps", out var all) || all.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            foreach (var entry in all.EnumerateObject())
            {
                if (!int.TryParse(entry.Name, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var map) ||
                    entry.Value.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }
                var marks = new List<MapBookmark>();
                foreach (var item in entry.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object ||
                        !Number(item, "n", out var n) || !Number(item, "x", out var x) ||
                        !Number(item, "y", out var y) || !Number(item, "z", out var z) ||
                        n is < 1 or > MaxPerMap * 10 || marks.Count >= MaxPerMap)
                    {
                        return false;
                    }
                    marks.Add(new MapBookmark((int)n, (float)x, (float)y, (float)z));
                }
                maps[map] = marks;
            }
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool Number(JsonElement item, string name, out double value)
    {
        value = 0;
        return item.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Number &&
            field.TryGetDouble(out value) && double.IsFinite(value) && (name != "n" || value == Math.Floor(value));
    }
}
