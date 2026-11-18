using System.Globalization;

namespace Metaphor.Core.Field;

// Issues #177, #178 and #180: what the development-only field watch reads, as log fields. The offsets behind each field
// are our reading of the exe on one build (docs/raw-sources/field-position-2026-11-16.md,
// docs/raw-sources/field-actors-2026-11-16.md, docs/raw-sources/field-state-and-map-2026-11-16.md); what each value
// means is what the capture session checks. Numbers and class names only, no game text.

/// <summary>One read of the field's state (#177, #180): the field scene's class, the map id, the player's class,
/// position, facing and locks, and the camera's type, its two vectors and the heading between them. A null field was
/// unreadable or its owner absent.</summary>
public sealed record FieldState(
    string? Scene,
    int? MapId,
    string? PlayerClass,
    FieldVector? Position,
    double? Facing,
    uint? InputLocks,
    uint? ActionFlags,
    int? CameraType,
    FieldVector? CameraAt60,
    FieldVector? CameraAt70)
{
    /// <summary>The heading of the camera's <c>+0x70</c> minus its <c>+0x60</c>, as the game's
    /// <c>FldCameraGetRelativeToPlayerDir_Yaw</c> takes it; whether that points forward or back is unverified.</summary>
    public double? CameraHeading => CameraAt60 is { } a && CameraAt70 is { } b ? FieldGeometry.HeadingTo(a, b) : null;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"scene={Scene ?? "none"} map={Number(MapId)} player={PlayerClass ?? "none"} pos={(Position?.ToString() ?? "-")} facing={Degrees(Facing)} locks={Number(InputLocks)} action=0x{ActionFlags ?? 0:x} cam={Number(CameraType)} cam60={(CameraAt60?.ToString() ?? "-")} cam70={(CameraAt70?.ToString() ?? "-")} camHeading={Degrees(CameraHeading)}");

    private static string Number<T>(T? value) where T : struct, IFormattable =>
        value?.ToString(null, CultureInfo.InvariantCulture) ?? "-";

    private static string Degrees(double? value) =>
        value is { } v ? Math.Round(v).ToString(CultureInfo.InvariantCulture) : "-";
}

/// <summary>A unit read from one of the field unit manager's lists (#178): the list's index, the unit's id at
/// <c>+0x9F8</c>, its position at <c>+0x80</c>, and, for doors, the position of its model node.</summary>
public sealed record FieldUnit(int List, int Id, FieldVector Position, FieldVector? NodePosition = null);

/// <summary>The field unit manager's lists as one log line (#178): each non-empty list's count, its first unit's class,
/// and its nearest units to the player with distance and heading.</summary>
public static class FieldNearby
{
    /// <summary>How many of each list's nearest units a line names.</summary>
    public const int NearestPerList = 3;

    /// <summary>The 21 lists of <c>fldUnitManager</c>, <c>0x40</c> apart from <c>+0x08</c>, named after NM-20/Resonant's
    /// reconstruction (docs/raw-sources/field-actors-2026-11-16.md); the last four are unnamed there.</summary>
    public static IReadOnlyList<string> ListNames { get; } =
    [
        "enemy", "member", "npc", "gem", "treasure", "door", "object", "gather", "other", "ladder", "actionHit",
        "cameraBox", "envBox", "mobGenerator", "mobUnit", "scriptHit", "gayaHit", "list17", "list18", "list19", "vehicle",
    ];

    /// <summary>Where list <paramref name="index"/>'s <c>begin</c> pointer sits in the manager; <c>end</c> follows.</summary>
    public static int BeginOffset(int index) => 0x18 + (index * 0x40);

    /// <summary>The line for <paramref name="units"/> around <paramref name="player"/>, given each list's count and its
    /// first unit's class.</summary>
    public static string Describe(
        FieldVector player, IReadOnlyDictionary<int, (int Count, string? FirstClass)> lists, IEnumerable<FieldUnit> units)
    {
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(units);
        var byList = units.GroupBy(unit => unit.List).ToDictionary(group => group.Key, group => group.ToList());
        var parts = new List<string>();
        foreach (var (index, (count, firstClass)) in lists.Where(list => list.Value.Count > 0).OrderBy(list => list.Key))
        {
            var name = index >= 0 && index < ListNames.Count ? ListNames[index] : $"list{index}";
            var nearest = byList.TryGetValue(index, out var listed)
                ? listed.OrderBy(unit => FieldGeometry.GroundDistance(player, unit.Position)).Take(NearestPerList)
                    .Select(unit => Unit(player, unit))
                : [];
            parts.Add(string.Create(CultureInfo.InvariantCulture,
                $"{name}={count}({firstClass ?? "?"})[{string.Join(' ', nearest)}]"));
        }
        return parts.Count == 0 ? "none" : string.Join(' ', parts);
    }

    private static string Unit(FieldVector player, FieldUnit unit)
    {
        var text = string.Create(CultureInfo.InvariantCulture,
            $"{unit.Id}@{FieldGeometry.GroundDistance(player, unit.Position):0.0}/{Math.Round(FieldGeometry.HeadingTo(player, unit.Position))}");
        return unit.NodePosition is { } node
            ? text + string.Create(CultureInfo.InvariantCulture,
                $"/node{FieldGeometry.GroundDistance(player, node):0.0}/{Math.Round(FieldGeometry.HeadingTo(player, node))}")
            : text;
    }
}
