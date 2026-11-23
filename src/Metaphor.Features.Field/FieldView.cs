namespace Metaphor.Features.Field;

/// <summary>What a nearby thing is (#181): the game's unit list it came from, which is its class
/// (docs/raw-sources/field-actors-2026-11-16.md).</summary>
public enum FieldKind
{
    Person,
    Door,
    Chest,
    Enemy,
}

/// <summary>One unit of the field, copied out of the game as plain values on the press that asked for it: no game
/// pointer outlives the read.</summary>
/// <param name="Kind">Its list.</param>
/// <param name="X">Its position, in the game's units: <c>X</c> and <c>Z</c> across the ground, <c>Y</c> up.</param>
/// <param name="Y">Its height.</param>
/// <param name="Z">Its other ground axis.</param>
/// <param name="Name">The name the game gives it, or null when none was read.</param>
/// <param name="Raw">The ids read for it, for the development log only (numbers, no game text).</param>
/// <param name="Id">Its object id on the map for the names table (#687), such as <c>npc-2-17</c>, or null when it has
/// none the mod reads.</param>
/// <param name="Called">The kind a names-table row gives it, or null to go by its list.</param>
/// <param name="Stunned">For an enemy, whether it is stunned (#772): its break byte, which the game's
/// <c>FldEnemyIsBreak</c> returns (docs/findings/enemy-stun-break-and-battle-condition-static.md); false when unread.
/// </param>
public sealed record FieldUnit(
    FieldKind Kind, float X, float Y, float Z, string? Name = null, string Raw = "", string? Id = null,
    NearbyKind? Called = null, bool Stunned = false)
{
    /// <summary>The kind it is spoken and sorted as: the names table's, or its list's.</summary>
    public NearbyKind Says => Called ?? Nearby.KindOf(Kind);
}

/// <summary>What the nearby list calls a thing (#687): its list's kind, or a finer one a names-table row gives.</summary>
public enum NearbyKind
{
    Person,
    Door,
    Exit,
    Chest,
    Item,
    Enemy,
    Examine,
}

/// <summary>The field around the player on one press (#181), as plain values.</summary>
/// <param name="X">The player's position, as <see cref="FieldUnit"/>'s.</param>
/// <param name="Y">The player's height.</param>
/// <param name="Z">The player's other ground axis.</param>
/// <param name="Heading">The way the player faces, in radians counter-clockwise from above, so left is +π/2, measured
/// as <c>atan2(x, z)</c> of a direction (docs/findings/field-position-facing-map-static.md).</param>
/// <param name="Units">The units of the lists read.</param>
/// <param name="DoorReachValue">The field database's value that picks the doors' reach (1 or 2: 150, else 300), or null
/// when it wasn't read.</param>
/// <param name="MapId">The map's id, or null when it wasn't read.</param>
/// <param name="CameraHeading">As <see cref="FieldPose.CameraHeading"/>; the nearby list's directions are from it
/// (#686).</param>
public sealed record FieldView(
    float X, float Y, float Z, double Heading, IReadOnlyList<FieldUnit> Units, int? DoorReachValue = null,
    int? MapId = null, double? CameraHeading = null);

/// <summary>Where the player stands and faces, as <see cref="FieldView"/>'s: a value, so the beacon (#183) reads it on
/// every poll without allocating.</summary>
/// <param name="X">The player's position.</param>
/// <param name="Y">The player's height.</param>
/// <param name="Z">The player's other ground axis.</param>
/// <param name="Heading">The way the player faces, as <see cref="FieldView.Heading"/>.</param>
/// <param name="CameraHeading">The way the field camera looks across the ground (from its eye to its look-at point),
/// measured as <paramref name="Heading"/>, or null when it can't be read
/// (docs/raw-sources/field-position-camera-session6-2026-11-17.md).</param>
public readonly record struct FieldPose(float X, float Y, float Z, double Heading, double? CameraHeading = null);

/// <summary>What the field controls read from the game, on the thread that called them.</summary>
public interface IFieldReads
{
    /// <summary>Whether the player roams the field freely: no menu, no dialogue, the field's idle scene.</summary>
    bool InFreeRoam();

    /// <summary>The field around the player, or null when it can't be read.</summary>
    FieldView? Read();

    /// <summary>The map's id, or null when it can't be read.</summary>
    int? MapId();

    /// <summary>The player's position and facing alone, or null when they can't be read; allocates nothing.</summary>
    FieldPose? ReadPose();

    /// <summary>The player's input-lock count and action flags as text for the Debug log (#702's probe), or null when
    /// they can't be read or in a Release build.</summary>
    string? PlayerLocks() => null;
}
