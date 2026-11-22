using System.Globalization;

namespace Metaphor.Features.Field;

/// <summary>One nearby thing as the list says it (#181).</summary>
/// <param name="Unit">The unit.</param>
/// <param name="Distance">Its distance across the ground, in the game's units.</param>
/// <param name="Bearing">Its direction from the camera's heading, or the player's facing when the camera isn't read, in
/// radians, left positive, from −π to π.</param>
/// <param name="InReach">Whether the game's own reach test for its kind passes, from the player's facing.</param>
public sealed record NearbyThing(FieldUnit Unit, double Distance, double Bearing, bool InReach)
{
    /// <summary>"Chest, 4 steps ahead-left, in reach": its name or kind, distance and direction.</summary>
    public string Text()
    {
        var steps = Nearby.Steps(Distance);
        var text = string.Create(CultureInfo.InvariantCulture,
            $"{Unit.Name ?? Nearby.KindWord(Unit.Says)}, {steps} {(steps == 1 ? "step" : "steps")} {Nearby.Direction(Bearing)}");
        return InReach ? text + ", in reach" : text;
    }
}

/// <summary>Which things the nearby list holds (#697): everything, or one category, per the maintainer's decision of
/// 2026-11-22 on #697. The category design comes from Arcana, the Persona 3 Reload accessibility mod
/// (<c>ChipsAhoiMcCoy/arcana</c>, Zlib, <c>main</c> at <c>ed3b005f86b1cde27e30ab299ae91af3d974c988</c>,
/// <c>CHANGELOG.md</c> "0.5.0 / Field" and <c>src/nav.cpp</c>; docs/raw-sources/arcana-combat-navigation-2026-11-22.md).
/// No code is copied.</summary>
public enum NearbyCategory
{
    Everything,
    People,
    DoorsAndExits,
    ChestsAndItems,
    Enemies,
    Examine,
}

/// <summary>The nearby list (#181), per the maintainer's decisions of 2026-11-21 on #181: things within about 30 metres,
/// nearest first, at most 8, each with its kind or name, distance and direction from the camera's heading, which #181's
/// spec calls camera-relative (the player's facing when the camera isn't read; #686).</summary>
public static class Nearby
{
    /// <summary>The most things one list holds.</summary>
    public const int MaxThings = 8;

    /// <summary>Game units in one spoken step. [NEEDS VERIFICATION]: the game's units per metre aren't measured yet, so
    /// the list speaks rough steps, taken as 100 units each, until a session measures them with the dev log.</summary>
    public const double UnitsPerStep = 100;

    /// <summary>The list's radius: the maintainer's "about 30 m", as 30 steps until a metre is measured.</summary>
    public const double Radius = 30 * UnitsPerStep;

    // The reach tests of fldGmcDoor::vf44 (0x6992a0) and fldGimmick::vf44 (0x697d00), which chests keep, on game build
    // 548ddc95… (Steam build 18330018): ground distance below the limit, height difference below 300, and within 90°
    // of facing (docs/findings/field-interaction-prompt-static.md).
    private const double NearDoorReach = 150;
    private const double FarReach = 300;
    private const double HeightReach = 300;
    private const double FacingReach = Math.PI / 2;

    /// <summary>The things of <paramref name="view"/> within <see cref="Radius"/>, nearest first, at most
    /// <see cref="MaxThings"/>.</summary>
    public static IReadOnlyList<NearbyThing> List(FieldView view, NearbyCategory category = NearbyCategory.Everything)
    {
        ArgumentNullException.ThrowIfNull(view);
        return [.. view.Units
            .Where(unit => category == NearbyCategory.Everything || CategoryOf(unit.Says) == category)
            .Select(unit => Place(view, unit))
            .Where(thing => thing.Distance <= Radius)
            .OrderBy(thing => thing.Distance)
            .Take(MaxThings)];
    }

    /// <summary>Spoken steps for a distance, at least one.</summary>
    public static int Steps(double distance) => Math.Max(1, (int)Math.Round(distance / UnitsPerStep));

    /// <summary>What a nameless thing is called. Doors are "Door", never "Exit", until a session proves which leave the
    /// map (maintainer, 2026-11-21).</summary>
    public static string KindWord(FieldKind kind) => KindWord(KindOf(kind));

    /// <summary>What a nameless thing of <paramref name="kind"/> is called. "Exit", "Item" and "Something to examine"
    /// come only from a names-table row (#687).</summary>
    public static string KindWord(NearbyKind kind) => kind switch
    {
        NearbyKind.Person => "Person",
        NearbyKind.Door => "Door",
        NearbyKind.Exit => "Exit",
        NearbyKind.Chest => "Chest",
        NearbyKind.Item => "Item",
        NearbyKind.Enemy => "Enemy",
        NearbyKind.Examine => "Something to examine",
        _ => "Thing",
    };

    /// <summary>The kind a unit of <paramref name="kind"/>'s list is spoken as before any names-table row.</summary>
    public static NearbyKind KindOf(FieldKind kind) => kind switch
    {
        FieldKind.Door => NearbyKind.Door,
        FieldKind.Chest => NearbyKind.Chest,
        FieldKind.Enemy => NearbyKind.Enemy,
        _ => NearbyKind.Person,
    };

    /// <summary>The category a thing of <paramref name="kind"/> is listed under.</summary>
    public static NearbyCategory CategoryOf(NearbyKind kind) => kind switch
    {
        NearbyKind.Person => NearbyCategory.People,
        NearbyKind.Door or NearbyKind.Exit => NearbyCategory.DoorsAndExits,
        NearbyKind.Chest or NearbyKind.Item => NearbyCategory.ChestsAndItems,
        NearbyKind.Enemy => NearbyCategory.Enemies,
        _ => NearbyCategory.Examine,
    };

    /// <summary>A category's spoken name.</summary>
    public static string CategoryWord(NearbyCategory category) => category switch
    {
        NearbyCategory.People => "People",
        NearbyCategory.DoorsAndExits => "Doors and exits",
        NearbyCategory.ChestsAndItems => "Chests and items",
        NearbyCategory.Enemies => "Enemies",
        NearbyCategory.Examine => "Examine",
        _ => "Everything",
    };

    /// <summary>One of eight directions for a bearing, left positive: ahead, ahead-left, left, behind-left, behind,
    /// behind-right, right, ahead-right.</summary>
    public static string Direction(double bearing)
    {
        var sector = (int)Math.Round(Normalised(bearing) / (Math.PI / 4));
        return sector switch
        {
            0 => "ahead",
            1 => "ahead-left",
            2 => "left",
            3 => "behind-left",
            -1 => "ahead-right",
            -2 => "right",
            -3 => "behind-right",
            _ => "behind",
        };
    }

    /// <summary>The ground reach of <paramref name="kind"/> by the game's test, or null when the game has none for it
    /// that the mod can read (people's isn't found, enemies aren't used). A door's is 150 when the field database's value
    /// is 1 or 2 and 300 otherwise; unknown is taken as 150, so "in reach" is never said too early.</summary>
    public static double? ReachLimit(FieldKind kind, int? doorReachValue) => kind switch
    {
        FieldKind.Door => doorReachValue is null or 1 or 2 ? NearDoorReach : FarReach,
        FieldKind.Chest => FarReach,
        _ => null,
    };

    /// <summary>Where <paramref name="unit"/> lies from <paramref name="pose"/>: its ground distance, its bearing from
    /// the facing (left positive, −π to π), and whether the game's reach test for its kind passes.</summary>
    public static (double Distance, double Bearing, bool InReach) Measure(
        FieldPose pose, FieldUnit unit, int? doorReachValue)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var dx = unit.X - pose.X;
        var dz = unit.Z - pose.Z;
        var distance = Math.Sqrt(dx * dx + dz * dz);
        var bearing = Normalised(Math.Atan2(dx, dz) - pose.Heading);
        var inReach = ReachLimit(unit.Kind, doorReachValue) is { } limit && distance < limit &&
            Math.Abs(unit.Y - pose.Y) < HeightReach && Math.Abs(bearing) < FacingReach;
        return (distance, bearing, inReach);
    }

    // The reach test keeps the character's facing; the spoken direction is from the camera's when it's read, as the
    // beacon's beep (#686, maintainer by ear 2026-11-22 on PR #667).
    private static NearbyThing Place(FieldView view, FieldUnit unit)
    {
        var (distance, bearing, inReach) =
            Measure(new FieldPose(view.X, view.Y, view.Z, view.Heading), unit, view.DoorReachValue);
        var spoken = view.CameraHeading is { } camera ? Normalised(bearing + view.Heading - camera) : bearing;
        return new(unit, distance, spoken, inReach);
    }

    /// <summary><paramref name="angle"/> wrapped into (−π, π].</summary>
    internal static double Normalised(double angle)
    {
        var turned = Math.IEEERemainder(angle, 2 * Math.PI);
        return turned <= -Math.PI ? turned + 2 * Math.PI : turned;
    }
}
