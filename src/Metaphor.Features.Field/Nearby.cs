using System.Globalization;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>One nearby thing as the list says it (#181).</summary>
/// <param name="Unit">The unit.</param>
/// <param name="Distance">Its distance across the ground, in the game's units.</param>
/// <param name="Bearing">Its direction from the camera's heading, or the player's facing when the camera isn't read, in
/// radians, left positive, from −π to π.</param>
/// <param name="InReach">Whether the game's own reach test for its kind passes, from the player's facing.</param>
/// <param name="Height">Its height above the player, in the game's units, up positive (#650).</param>
/// <param name="Ways">How many listed exits it stands for, the nearest of them (#956); 1 for anything else.</param>
/// <param name="Ahead">The ground heading <paramref name="Bearing"/> is taken from (#700), so
/// <c>Bearing + Ahead</c> is its heading on the map's own axes.</param>
public sealed record NearbyThing(
    FieldUnit Unit, double Distance, double Bearing, bool InReach, double Height = 0, int Ways = 1, double Ahead = 0)
{
    /// <summary>"Chest, 4 steps ahead-left, in reach": its name or kind, distance and direction, and with
    /// <paramref name="sayHeight"/> "above" or "below" past <see cref="Nearby.HeightThreshold"/> (#650). Exits merged
    /// into one row say how many ways there are: "Dorm, 2 ways, nearest 3 steps ahead" (#956). With
    /// <paramref name="compass"/> the direction is a compass point instead (#700, <see cref="Nearby.Compass"/>).</summary>
    public string Text(bool sayHeight = false, bool compass = false)
    {
        var steps = Nearby.Steps(Distance);
        var ways = Ways > 1 ? string.Create(CultureInfo.InvariantCulture, $" {Ways} ways, nearest") : "";
        var direction = compass ? Nearby.Compass(Bearing + Ahead) : Nearby.Direction(Bearing);
        var text = string.Create(CultureInfo.InvariantCulture,
            $"{Unit.Name ?? Nearby.KindWord(Unit.Says)},{ways} {steps} {(steps == 1 ? "step" : "steps")} {direction}");
        if (sayHeight && Nearby.HeightWord(Height) is { } height)
        {
            text += ", " + height;
        }
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

    /// <summary>The longest radius the setting allows, in game units: what the hook layer reads units out to (#931).
    /// </summary>
    public const double MaxRadius = 60 * UnitsPerStep;

    /// <summary>The list radius's slider (#931), in steps: 5 to 60, in steps of 5, and 30 by default, as
    /// <see cref="Radius"/>.</summary>
    public static SliderLimit RadiusSteps { get; } = new("field", "nearbyRadius", 30, 5, 60, 5)
    {
        Spoken = "Nearby list range",
        Description = "How far the nearby list looks (`nearby`), in steps.",
        Order = 140,
    };

    /// <summary>The list radius <paramref name="settings"/> give, in game units.</summary>
    public static double RadiusFrom(ModSettings settings) => RadiusSteps.From(settings) * UnitsPerStep;

    // The reach tests of fldGmcDoor::vf44 (0x6992a0) and fldGimmick::vf44 (0x697d00), which chests keep, on game build
    // 548ddc95… (Steam build 18330018): ground distance below the limit, height difference below 300, and within 90°
    // of facing (docs/findings/field-interaction-prompt-static.md).
    private const double NearDoorReach = 150;
    private const double FarReach = 300;
    private const double HeightReach = 300;
    private const double FacingReach = Math.PI / 2;

    /// <summary>How far above or below the player a thing is said as "above" or "below" (#650): the game's own reach
    /// height above, past which it never offers a door or chest, so it reads as another level. The mod's choice of
    /// threshold; [NEEDS VERIFICATION] by ear on stairs and slopes.</summary>
    public const double HeightThreshold = HeightReach;

    /// <summary>"above" or "below" for a thing <paramref name="height"/> units above the player, or null within
    /// <see cref="HeightThreshold"/> (#650).</summary>
    public static string? HeightWord(double height) =>
        height >= HeightThreshold ? "above" : height <= -HeightThreshold ? "below" : null;

    /// <summary>The things of <paramref name="view"/> within <paramref name="radius"/> (<see cref="Radius"/> when null),
    /// in game units, nearest first, at most <see cref="MaxThings"/>. Exits a names-table row gives the same name lead
    /// to the same place, so they are one row at the nearest (#956); nameless doors and people never merge.</summary>
    public static IReadOnlyList<NearbyThing> List(
        FieldView view, NearbyCategory category = NearbyCategory.Everything, double? radius = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var within = radius ?? Radius;
        var near = view.Units
            .Where(unit => category == NearbyCategory.Everything || CategoryOf(unit.Says) == category)
            .Select(unit => Place(view, unit))
            .Where(thing => thing.Distance <= within)
            .OrderBy(thing => thing.Distance);
        return [.. MergeExits(near).Take(MaxThings)];
    }

    // Nearest first in, nearest first out: each named exit's first row stands for the later ones with its name. The
    // idea comes from Arcana's players (ChipsAhoiMcCoy/arcana, Zlib; audiogames.net, 2026-11), no code seen (CREDITS.md).
    private static List<NearbyThing> MergeExits(IEnumerable<NearbyThing> nearestFirst)
    {
        var rows = new List<NearbyThing>();
        var byName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var thing in nearestFirst)
        {
            if (thing.Unit is not { Name: { } name, Says: NearbyKind.Exit })
            {
                rows.Add(thing);
                continue;
            }
            if (byName.TryGetValue(name, out var at))
            {
                rows[at] = rows[at] with { Ways = rows[at].Ways + 1 };
                continue;
            }
            byName[name] = rows.Count;
            rows.Add(thing);
        }
        return rows;
    }

    /// <summary>The things of <paramref name="view"/> the game's reach test passes for, nearest first, however many and
    /// whatever else is nearer (#644).</summary>
    public static IReadOnlyList<NearbyThing> InReach(FieldView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return [.. view.Units
            .Select(unit => Place(view, unit))
            .Where(thing => thing.InReach)
            .OrderBy(thing => thing.Distance)];
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

    /// <summary>One of eight compass points for a heading on the map's own axes (#700): north is +Z and west +X, the
    /// minimap's up and left (a minimap point's world <c>x</c> falls as it moves right and its <c>z</c> as it moves down,
    /// docs/findings/minimap-data-converts-map-points-to-world.md), which fits headings growing counter-clockwise seen
    /// from above (<see cref="CameraFrame"/>). The idea of a fixed compass frame comes from Arcana, the Persona 3 Reload
    /// accessibility mod (<c>ChipsAhoiMcCoy/arcana</c>, Zlib, <c>main</c> at
    /// <c>ed3b005f86b1cde27e30ab299ae91af3d974c988</c>, README "Top-down exploration"); no code is copied, and the
    /// camera is never changed.</summary>
    public static string Compass(double heading)
    {
        var sector = (int)Math.Round(Normalised(heading) / (Math.PI / 4));
        return sector switch
        {
            0 => "north",
            1 => "north-west",
            2 => "west",
            3 => "south-west",
            -1 => "north-east",
            -2 => "east",
            -3 => "south-east",
            _ => "south",
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

    /// <summary>Ground distance the reach callout's hold adds to the reach and height limits, half a step, so standing
    /// at the edge doesn't flicker in and out (#644). The mod's own choice, not the game's.</summary>
    public const double HoldDistanceSlack = 50;

    /// <summary>Angle the reach callout's hold adds to the facing limit, 20°, for the same reason (#644).</summary>
    public const double HoldFacingSlack = Math.PI / 9;

    /// <summary>Whether <paramref name="unit"/> passes the game's reach test widened by <see cref="HoldDistanceSlack"/>
    /// and <see cref="HoldFacingSlack"/>: what keeps a thing already in reach held there (#644).</summary>
    public static bool NearReach(FieldView view, FieldUnit unit)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(unit);
        return Place(view, unit, HoldDistanceSlack, HoldFacingSlack, CalloutRangeScale).InReach;
    }

    /// <summary>How much further than the game's reach distance the reach callout is said, 20%, so it comes a little
    /// earlier (#644). The mod's own choice; the game's test, the nearby list and the beacon keep the game's reach.</summary>
    public const double CalloutRangeScale = 1.2;

    /// <summary>The things of <paramref name="view"/> within the callout's range (the game's reach distance times
    /// <see cref="CalloutRangeScale"/>, same height and facing), nearest first (#644).</summary>
    public static IReadOnlyList<NearbyThing> CalloutReach(FieldView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return [.. view.Units
            .Select(unit => Place(view, unit, 0, 0, CalloutRangeScale))
            .Where(thing => thing.InReach)
            .OrderBy(thing => thing.Distance)];
    }

    /// <summary>Where <paramref name="unit"/> lies from <paramref name="pose"/>: its ground distance, its bearing from
    /// the facing (left positive, −π to π), and whether the game's reach test for its kind passes, widened by the
    /// slack and range scale the reach callout uses (#644).</summary>
    public static (double Distance, double Bearing, bool InReach) Measure(
        FieldPose pose, FieldUnit unit, int? doorReachValue, double slack = 0, double facingSlack = 0,
        double rangeScale = 1)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var dx = unit.X - pose.X;
        var dz = unit.Z - pose.Z;
        var distance = Math.Sqrt(dx * dx + dz * dz);
        var bearing = Normalised(Math.Atan2(dx, dz) - pose.Heading);
        var inReach = ReachLimit(unit.Kind, doorReachValue) is { } limit && distance < limit * rangeScale + slack &&
            Math.Abs(unit.Y - pose.Y) < HeightReach + slack && Math.Abs(bearing) < FacingReach + facingSlack;
        return (distance, bearing, inReach);
    }

    // The reach test keeps the character's facing; the spoken direction is from the camera-relative frame, as the
    // beacon's beep (#686, maintainer by ear 2026-11-22 on PR #667).
    private static NearbyThing Place(FieldView view, FieldUnit unit, double slack = 0, double facingSlack = 0,
        double rangeScale = 1)
    {
        var pose = view.Pose;
        var (distance, _, inReach) = Measure(pose, unit, view.DoorReachValue, slack, facingSlack, rangeScale);
        return new(unit, distance, CameraFrame.Bearing(pose, unit.X, unit.Z), inReach, unit.Y - pose.Y,
            Ahead: CameraFrame.Ahead(pose));
    }

    /// <summary><paramref name="angle"/> wrapped into (−π, π], as <see cref="CameraFrame.Normalised"/>.</summary>
    internal static double Normalised(double angle) => CameraFrame.Normalised(angle);
}
