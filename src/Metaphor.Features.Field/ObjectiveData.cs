using System.Text.Json;
using System.Text.RegularExpressions;

namespace Metaphor.Features.Field;

/// <summary>Turns a field's 2D icon-point position into world <c>x, z</c> (#652). By default each map's minimap record
/// (<see cref="MiniMapFrameSource"/>, docs/findings/minimap-data-converts-map-points-to-world.md); a field in
/// <c>map-to-world.json</c> (<see cref="FittedMapToWorld"/>) overrides it (<see cref="OverriddenMapToWorld"/>).
/// </summary>
public interface IMapToWorld
{
    /// <summary>Whether the conversion is still being read; asking starts the read if it hasn't started.</summary>
    bool Loading { get; }

    /// <summary>World <c>x, z</c> for point <paramref name="x"/>, <paramref name="y"/> at <paramref name="spot"/>, or
    /// null when there is no conversion there.</summary>
    (float X, float Z)? ToWorld(MapSpot spot, int x, int y);

    /// <summary>Which conversion <paramref name="spot"/> gets, for the Debug log: numbers and words, no spaces.</summary>
    string Describe(MapSpot spot);
}

/// <summary>The hand-fitted fields of <paramref name="overrides"/> first, then <paramref name="frames"/> (#652).
/// </summary>
public sealed class OverriddenMapToWorld(FittedMapToWorld overrides, IMapToWorld frames) : IMapToWorld
{
    /// <inheritdoc/>
    public bool Loading => frames.Loading;

    /// <inheritdoc/>
    public (float X, float Z)? ToWorld(MapSpot spot, int x, int y) =>
        overrides.ToWorld(spot, x, y) ?? frames.ToWorld(spot, x, y);

    /// <inheritdoc/>
    public string Describe(MapSpot spot) => overrides.Has(spot.Field) ? overrides.Describe(spot) : frames.Describe(spot);
}

/// <summary>One field's fitted conversion (#652): world <c>x = OriginX + ScaleX × x</c> and
/// <c>z = OriginZ + ScaleZ × y</c>, no rotation. A scale may be negative for an axis that runs the other way.</summary>
public sealed record MapConversion(double OriginX, double OriginZ, double ScaleX, double ScaleZ);

/// <summary>The per-field conversions a contributor fitted from a marker walk, from
/// <c>data/objectives/map-to-world.json</c> (#652): an override for a field whose minimap record turns out wrong. Empty
/// by default.</summary>
public sealed class FittedMapToWorld(IReadOnlyDictionary<string, MapConversion> fields) : IMapToWorld
{
    /// <summary>No conversion for any field.</summary>
    public static FittedMapToWorld Empty { get; } = new(new Dictionary<string, MapConversion>());

    /// <summary>How many fields have one.</summary>
    public int Count => fields.Count;

    /// <inheritdoc/>
    public bool Loading => false;

    /// <summary>Whether field <paramref name="field"/> has one.</summary>
    public bool Has(string field) => fields.ContainsKey(field);

    /// <inheritdoc/>
    public (float X, float Z)? ToWorld(MapSpot spot, int x, int y) =>
        fields.TryGetValue(spot.Field, out var fit)
            ? ((float)(fit.OriginX + (fit.ScaleX * x)), (float)(fit.OriginZ + (fit.ScaleZ * y)))
            : null;

    /// <inheritdoc/>
    public string Describe(MapSpot spot) => Has(spot.Field) ? "override" : "none";
}

/// <summary>A hand-written waypoint for an objective with no marker on a field (#652, ADR 0016): our own data.</summary>
/// <param name="Field">The field, <c>f_0001_001</c>.</param>
/// <param name="Row">The objective table's row it guides to.</param>
/// <param name="X">World <c>x</c>, as the "where am I" log line gives the player's.</param>
/// <param name="Z">World <c>z</c>.</param>
public sealed record ObjectiveWaypoint(string Field, int Row, float X, float Z);

/// <summary>The objective guidance's own data (#652), both under <c>data/objectives/</c> in the mod folder and both
/// empty for now: <c>map-to-world.json</c>, fitted conversions that override the minimap records, and <c>waypoints.json</c>, the hand-written
/// waypoints. The format is in docs/contributors/how-to/fit-objective-markers.md. A file outside the schema is left out
/// and its problem logged, so the other still loads.</summary>
public sealed partial class ObjectiveData
{
    /// <summary>The conversions' file name.</summary>
    public const string ConversionsFile = "map-to-world.json";

    /// <summary>The waypoints' file name.</summary>
    public const string WaypointsFile = "waypoints.json";

    /// <summary>A file larger than this is left out, so a huge file can't stop the mod's start.</summary>
    public const long MaxFileBytes = 1024 * 1024;

    private ObjectiveData(FittedMapToWorld conversions, IReadOnlyList<ObjectiveWaypoint> waypoints)
    {
        Conversions = conversions;
        Waypoints = waypoints;
    }

    /// <summary>No conversions and no waypoints.</summary>
    public static ObjectiveData Empty { get; } = new(FittedMapToWorld.Empty, []);

    /// <summary>Each field's fitted conversion.</summary>
    public FittedMapToWorld Conversions { get; }

    /// <summary>The hand-written waypoints, in file order.</summary>
    public IReadOnlyList<ObjectiveWaypoint> Waypoints { get; }

    /// <summary>Both files in <paramref name="folder"/>; a missing one is empty, and one that can't be read or breaks the
    /// schema is empty with its problem handed to <paramref name="problem"/>.</summary>
    public static ObjectiveData Load(string folder, Action<string> problem)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(problem);
        var conversions = Read(Path.Combine(folder, ConversionsFile), problem, ParseConversions) ?? FittedMapToWorld.Empty;
        var waypoints = Read(Path.Combine(folder, WaypointsFile), problem, ParseWaypoints) ?? [];
        return new(conversions, waypoints);
    }

    /// <summary>The conversions in <paramref name="json"/>; throws a <see cref="FormatException"/> saying what breaks
    /// the schema.</summary>
    public static FittedMapToWorld ParseConversions(string json)
    {
        var fields = new Dictionary<string, MapConversion>(StringComparer.Ordinal);
        foreach (var row in Rows(json, ConversionsFile, "maps"))
        {
            Only(row, ConversionsFile, "field", "originX", "originZ", "scaleX", "scaleZ");
            var field = Field(row, ConversionsFile);
            if (!fields.TryAdd(field, new(
                Number(row, ConversionsFile, "originX", field), Number(row, ConversionsFile, "originZ", field),
                Scale(row, "scaleX", field),
                Scale(row, "scaleZ", field))))
            {
                throw Refused(ConversionsFile, $"{field} is there twice");
            }
        }
        return fields.Count == 0 ? FittedMapToWorld.Empty : new(fields);
    }

    /// <summary>The waypoints in <paramref name="json"/>; throws a <see cref="FormatException"/> saying what breaks the
    /// schema.</summary>
    public static IReadOnlyList<ObjectiveWaypoint> ParseWaypoints(string json)
    {
        var waypoints = new List<ObjectiveWaypoint>();
        foreach (var row in Rows(json, WaypointsFile, "waypoints"))
        {
            Only(row, WaypointsFile, "field", "row", "x", "z");
            var field = Field(row, WaypointsFile);
            if (!row.TryGetProperty("row", out var rowValue) || !rowValue.TryGetInt32(out var index) || index < 0)
            {
                throw Refused(WaypointsFile, $"a waypoint on {field} needs row, the objective's row as a whole number");
            }
            waypoints.Add(new(
                field, index, (float)Number(row, WaypointsFile, "x", field), (float)Number(row, WaypointsFile, "z", field)));
        }
        return waypoints;
    }

    private static T? Read<T>(string path, Action<string> problem, Func<string, T> parse)
        where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            if (new FileInfo(path).Length > MaxFileBytes)
            {
                problem($"{Path.GetFileName(path)}: over {MaxFileBytes} bytes, left out");
                return null;
            }
            return parse(File.ReadAllText(path));
        }
        catch (FormatException refused)
        {
            problem(refused.Message);
            return null;
        }
        catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
        {
            problem($"{Path.GetFileName(path)}: can't be read ({failed.GetType().Name})");
            return null;
        }
    }

    // The rows of the array named by key, each an object; the file is one object holding only that array.
    private static List<JsonElement> Rows(string json, string fileName, string key)
    {
        ArgumentNullException.ThrowIfNull(json);
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
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(key, out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                throw Refused(fileName, $"must hold one object with {key}, a list");
            }
            Only(root, fileName, key);
            var list = new List<JsonElement>();
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    throw Refused(fileName, $"each of {key} must be an object");
                }
                list.Add(row.Clone());
            }
            return list;
        }
    }

    private static string Field(JsonElement row, string fileName) =>
        row.TryGetProperty("field", out var value) && value.ValueKind == JsonValueKind.String &&
        value.GetString() is { } field && FieldPattern().IsMatch(field)
            ? field
            : throw Refused(fileName, "each row needs field, such as f_0001_001");

    private static double Number(JsonElement row, string fileName, string key, string field) =>
        row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number) && double.IsFinite(number)
            ? number
            : throw Refused(fileName, $"{field}: {key} must be a number");

    private static double Scale(JsonElement row, string key, string field) =>
        Number(row, ConversionsFile, key, field) is var scale && scale != 0
            ? scale
            : throw Refused(ConversionsFile, $"{field}: {key} must be a number other than 0");

    private static void Only(JsonElement element, string fileName, params string[] allowed)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw Refused(fileName, $"holds {property.Name}; only {string.Join(", ", allowed)} are allowed there");
            }
        }
    }

    private static FormatException Refused(string fileName, string why) => new($"{fileName}: {why}");

    [GeneratedRegex(@"^f_[0-9]{4}_[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldPattern();
}
