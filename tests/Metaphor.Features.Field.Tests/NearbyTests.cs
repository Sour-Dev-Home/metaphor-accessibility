namespace Metaphor.Features.Field.Tests;

/// <summary>The nearby list (#181): within the radius, nearest first, at most eight, each with its name or kind, rough
/// steps and direction from the player's facing, and "in reach" by the game's thresholds. Made-up positions and names.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NearbyTests
{
    private const double Step = Nearby.UnitsPerStep;

    // Facing +Z: heading 0. Left is +X, as heading counts counter-clockwise with atan2(x, z).
    private static FieldView Facing(double heading, params FieldUnit[] units) => new(0, 0, 0, heading, units);

    private static FieldUnit At(FieldKind kind, double x, double z, double y = 0, string? name = null) =>
        new(kind, (float)x, (float)y, (float)z, name);

    [Fact]
    public void ThingsAreNearestFirstAndOnlyWithinTheRadius()
    {
        var view = Facing(0,
            At(FieldKind.Door, 0, 10 * Step),
            At(FieldKind.Chest, 0, 4 * Step),
            At(FieldKind.Enemy, 0, Nearby.Radius + 1));

        var texts = Nearby.List(view).Select(thing => thing.Text());

        Assert.Equal(["Chest, 4 steps ahead", "Door, 10 steps ahead"], texts);
    }

    // #650: "above" or "below" from the game's reach height, 300 units, and only when asked for.
    [Theory]
    [InlineData(300, "Door, 5 steps ahead, above")]
    [InlineData(-300, "Door, 5 steps ahead, below")]
    [InlineData(299, "Door, 5 steps ahead")]
    [InlineData(-299, "Door, 5 steps ahead")]
    public void HeightIsSaidPastTheReachHeight(double y, string said)
    {
        var view = Facing(0, At(FieldKind.Door, 0, 5 * Step, y));

        Assert.Equal(said, Nearby.List(view).Single().Text(sayHeight: true));
    }

    [Fact]
    public void HeightIsFromThePlayersHeight()
    {
        var view = new FieldView(0, 1000, 0, 0, [At(FieldKind.Door, 0, 5 * Step, 1000 - Nearby.HeightThreshold)]);

        Assert.Equal("Door, 5 steps ahead, below", Nearby.List(view).Single().Text(sayHeight: true));
    }

    [Fact]
    public void HeightIsLeftOutUnlessAskedFor()
    {
        var view = Facing(0, At(FieldKind.Door, 0, 5 * Step, 1000));

        Assert.Equal("Door, 5 steps ahead", Nearby.List(view).Single().Text());
    }

    [Fact]
    public void InReachKeepsAChestBehindEightNearerPeople()
    {
        var units = Enumerable.Range(1, 8).Select(i => At(FieldKind.Person, 0, i * 10))
            .Append(At(FieldKind.Chest, 0, 250)).ToArray();

        var inReach = Nearby.InReach(Facing(0, units));

        Assert.Equal(FieldKind.Chest, Assert.Single(inReach).Unit.Kind);
    }

    [Fact]
    public void AtMostEightThingsAreListed()
    {
        var units = Enumerable.Range(1, 12).Select(i => At(FieldKind.Person, 0, i * Step)).ToArray();

        var list = Nearby.List(Facing(0, units));

        Assert.Equal(Nearby.MaxThings, list.Count);
        Assert.Equal(Step, list[0].Distance, 3);
    }

    [Theory]
    [InlineData(0, 1, "ahead")]
    [InlineData(1, 1, "ahead-left")]
    [InlineData(1, 0, "left")]
    [InlineData(1, -1, "behind-left")]
    [InlineData(0, -1, "behind")]
    [InlineData(-1, -1, "behind-right")]
    [InlineData(-1, 0, "right")]
    [InlineData(-1, 1, "ahead-right")]
    public void DirectionsAreFromThePlayersFacing(double x, double z, string direction)
    {
        // Facing +X (left of +Z by 90°): turn every point the same way, so the answer stays relative to facing.
        var view = Facing(Math.PI / 2, At(FieldKind.Person, z * 5 * Step, -x * 5 * Step));

        Assert.EndsWith(direction, Nearby.List(view).Single().Text(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, 0, "right")]
    [InlineData(1, 0, "left")]
    [InlineData(0, 1, "ahead")]
    [InlineData(0, -1, "behind")]
    public void DirectionsAreFromTheCameraWhenItIsRead(double x, double z, string direction)
    {
        // #686, by ear 2026-11-22: the character faces the camera (heading π, camera 0, looking +Z), so the screen's
        // right is -X; a door there was said as "left".
        var view = new FieldView(0, 0, 0, Math.PI, [At(FieldKind.Door, x * 5 * Step, z * 5 * Step)], CameraHeading: 0);

        Assert.Equal($"Door, 5 steps {direction}", Nearby.List(view).Single().Text());
    }

    [Fact]
    public void DirectionsFallBackToTheFacingWhenTheCameraIsUnread()
    {
        var view = new FieldView(0, 0, 0, Math.PI, [At(FieldKind.Door, -5 * Step, 0)]);

        Assert.Equal("Door, 5 steps left", Nearby.List(view).Single().Text());
    }

    [Fact]
    public void TheReachTestKeepsTheCharactersFacing()
    {
        // Ahead on screen but behind the character, who faces the camera: not in reach.
        var behind = new FieldView(0, 0, 0, Math.PI, [At(FieldKind.Chest, 0, 2 * Step)], CameraHeading: 0);
        // Behind on screen but ahead of the character: in reach.
        var ahead = new FieldView(0, 0, 0, Math.PI, [At(FieldKind.Chest, 0, -2 * Step)], CameraHeading: 0);

        Assert.Equal("Chest, 2 steps ahead", Nearby.List(behind).Single().Text());
        Assert.Equal("Chest, 2 steps behind, in reach", Nearby.List(ahead).Single().Text());
    }

    [Fact]
    public void ANamedThingSaysItsNameAndOneStepIsSingular()
    {
        var view = Facing(0, At(FieldKind.Person, 0, 0.6 * Step, name: "Testa"));

        Assert.Equal("Testa, 1 step ahead", Nearby.List(view).Single().Text());
    }

    [Fact]
    public void AChestAheadWithinItsReachIsInReach()
    {
        var view = Facing(0, At(FieldKind.Chest, 0, 260), At(FieldKind.Chest, 0, -270));

        var list = Nearby.List(view);

        Assert.Equal(["Chest, 3 steps ahead, in reach", "Chest, 3 steps behind"], list.Select(thing => thing.Text()));
    }

    [Fact]
    public void ReachFailsTooFarTooHighOrForAPerson()
    {
        var view = Facing(0,
            At(FieldKind.Chest, 0, 310),
            At(FieldKind.Chest, 10, 100, y: 320),
            At(FieldKind.Person, 0, 50),
            At(FieldKind.Enemy, 0, 60));

        Assert.All(Nearby.List(view), thing => Assert.False(thing.InReach));
    }

    [Theory]
    [InlineData(null, 150)]
    [InlineData(1, 150)]
    [InlineData(2, 150)]
    [InlineData(0, 300)]
    [InlineData(5, 300)]
    public void ADoorsReachFollowsTheDatabaseValue(int? value, double limit)
    {
        Assert.Equal(limit, Nearby.ReachLimit(FieldKind.Door, value));
        var view = new FieldView(0, 0, 0, 0, [At(FieldKind.Door, 0, limit - 1), At(FieldKind.Door, 0, -(limit + 1))], value);

        Assert.Equal([true, false], Nearby.List(view).Select(thing => thing.InReach));
    }

    [Fact]
    public void NamelessThingsSayTheirKindAndDoorsAreNeverExits()
    {
        Assert.Equal(["Person", "Door", "Chest", "Enemy"], Enum.GetValues<FieldKind>().Select(Nearby.KindWord));
    }
}
