using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Metaphor.Features.Field;

/// <summary>One names-table row (#687): the name a contributor wrote for a thing, and its kind.</summary>
/// <param name="Name">The spoken name.</param>
/// <param name="Kind">The kind it's listed as.</param>
public sealed record NamedThing(string Name, NearbyKind Kind);

/// <summary>One map's names-table file (#687).</summary>
/// <param name="Map">The map id.</param>
/// <param name="Objects">Its rows by object id.</param>
public sealed record NamesFile(int Map, IReadOnlyDictionary<string, NamedThing> Objects);

/// <summary>The names table (#687), per the maintainer's decision of 2026-11-22 on #687: one JSON file per map under
/// <c>data/names/</c>, named for the map id, whose <c>objects</c> give a contributor's name and kind for an object id
/// (<see cref="FieldUnit.Id"/>). Only names contributors write, never game text. The format is in
/// docs/contributors/how-to/name-a-nearby-thing.md. A thing with no row keeps its list's kind.</summary>
public sealed partial class NamesTable
{
    /// <summary>The longest name a row holds.</summary>
    public const int MaxName = 60;

    private static readonly Dictionary<string, NearbyKind> Kinds = new(StringComparer.Ordinal)
    {
        ["person"] = NearbyKind.Person,
        ["door"] = NearbyKind.Door,
        ["exit"] = NearbyKind.Exit,
        ["chest"] = NearbyKind.Chest,
        ["item"] = NearbyKind.Item,
        ["enemy"] = NearbyKind.Enemy,
        ["examine"] = NearbyKind.Examine,
    };

    private readonly Dictionary<(int Map, string Id), NamedThing> rows;

    private NamesTable(Dictionary<(int Map, string Id), NamedThing> rows) => this.rows = rows;

    /// <summary>A table with no rows.</summary>
    public static NamesTable Empty { get; } = new([]);

    /// <summary>A table of <paramref name="files"/>' rows; a later file's row for the same map and id wins.</summary>
    public static NamesTable From(IEnumerable<NamesFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var rows = new Dictionary<(int, string), NamedThing>();
        foreach (var file in files)
        {
            foreach (var (id, row) in file.Objects)
            {
                rows[(file.Map, id)] = row;
            }
        }
        return rows.Count == 0 ? Empty : new(rows);
    }

    /// <summary>A names file larger than this is skipped: a hand-written table for one map is a few kilobytes, and a
    /// huge file must not stop the mod's start (security review of #687).</summary>
    public const long MaxFileBytes = 1024 * 1024;

    /// <summary>Every <c>*.json</c> file in <paramref name="folder"/>, in name order. A file outside the schema is
    /// skipped and its problem handed to <paramref name="problem"/>; a missing folder is an empty table.</summary>
    public static NamesTable Load(string folder, Action<string> problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        if (!Directory.Exists(folder))
        {
            return Empty;
        }
        var files = new List<NamesFile>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                if (new FileInfo(path).Length > MaxFileBytes)
                {
                    problem($"{Path.GetFileName(path)}: over {MaxFileBytes} bytes, skipped");
                    continue;
                }
                files.Add(Parse(File.ReadAllText(path), Path.GetFileName(path)));
            }
            catch (FormatException refused)
            {
                problem(refused.Message);
            }
            catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
            {
                problem($"{Path.GetFileName(path)}: can't be read ({failed.GetType().Name})");
            }
        }
        return From(files);
    }

    /// <summary>The rows of one file's <paramref name="json"/>, named <paramref name="fileName"/>; throws a
    /// <see cref="FormatException"/> that starts with the file name and says what breaks the schema.</summary>
    public static NamesFile Parse(string json, string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw Refused(fileName, "isn't JSON");
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Refused(fileName, "must hold one object with map and objects");
            }
            Only(root, fileName, "the file", "map", "objects");
            if (!root.TryGetProperty("map", out var mapValue) || !mapValue.TryGetInt32(out var map) || map < 0)
            {
                throw Refused(fileName, "needs map, the map id as a whole number");
            }
            if (fileName != string.Create(CultureInfo.InvariantCulture, $"{map}.json"))
            {
                throw Refused(fileName, string.Create(CultureInfo.InvariantCulture, $"holds map {map}, so it must be named {map}.json"));
            }
            if (!root.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Object)
            {
                throw Refused(fileName, "needs objects, an object of rows by object id");
            }
            var rows = new Dictionary<string, NamedThing>(StringComparer.Ordinal);
            foreach (var row in objects.EnumerateObject())
            {
                if (!ObjectId().IsMatch(row.Name))
                {
                    throw Refused(fileName, $"object id {row.Name} isn't one the mod reads (npc-K-N, door-N or chest-N)");
                }
                if (rows.ContainsKey(row.Name))
                {
                    throw Refused(fileName, $"object id {row.Name} is there twice");
                }
                rows[row.Name] = Row(row.Value, fileName, row.Name);
            }
            return new NamesFile(map, rows);
        }
    }

    /// <summary><paramref name="view"/> with each unit that has a row on its map renamed and given the row's kind; the
    /// same view when nothing matches.</summary>
    public FieldView Apply(FieldView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (rows.Count == 0 || view.MapId is not { } map ||
            !view.Units.Any(unit => unit.Id is { } id && rows.ContainsKey((map, id))))
        {
            return view;
        }
        return view with
        {
            Units = [.. view.Units.Select(unit => unit.Id is { } id && rows.TryGetValue((map, id), out var row)
                ? unit with { Name = row.Name, Called = row.Kind }
                : unit)],
        };
    }

    private static NamedThing Row(JsonElement row, string fileName, string id)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            throw Refused(fileName, $"{id} must be an object with name and kind");
        }
        Only(row, fileName, id, "name", "kind");
        if (!row.TryGetProperty("name", out var nameValue) || nameValue.ValueKind != JsonValueKind.String ||
            nameValue.GetString() is not { } name || string.IsNullOrWhiteSpace(name) || name.Length > MaxName ||
            name != name.Trim() || name.Any(char.IsControl))
        {
            throw Refused(fileName, $"{id} needs a name of 1 to {MaxName} characters on one line, with no spaces around it");
        }
        if (!row.TryGetProperty("kind", out var kindValue) || kindValue.ValueKind != JsonValueKind.String ||
            !Kinds.TryGetValue(kindValue.GetString()!, out var kind))
        {
            throw Refused(fileName, $"{id} needs a kind, one of {string.Join(", ", Kinds.Keys)}");
        }
        return new NamedThing(name, kind);
    }

    private static void Only(JsonElement element, string fileName, string what, params string[] allowed)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw Refused(fileName, $"{what} holds {property.Name}; only {string.Join(" and ", allowed)} are allowed");
            }
        }
    }

    private static FormatException Refused(string fileName, string why) => new($"{fileName}: {why}");

    // The ids FieldMemory makes (src/Metaphor.Hooks/FieldReads.cs): a person's NpcID kind and the index that kind
    // reads, a door's or chest's layout index.
    [GeneratedRegex(@"^(npc-[0-9]{1,2}-[0-9]{1,6}|door-[0-9]{1,6}|chest-[0-9]{1,6})$", RegexOptions.CultureInvariant)]
    private static partial Regex ObjectId();
}
