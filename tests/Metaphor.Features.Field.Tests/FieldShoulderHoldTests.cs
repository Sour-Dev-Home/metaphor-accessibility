using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field.Tests;

/// <summary>LB and RB in the field (#811, the maintainer's design of 2026-11-23): the game opens the popularity ranking
/// on LB and the map on RB as each goes down, so in free roam both are held back until let go. LB with RB toggles the
/// reader (#779) and opens nothing; a lone tap of either still opens its screen, on release.</summary>
[Trait("Category", "Unit")]
public sealed class FieldShoulderHoldTests
{
    private const int LB = 0x0400;
    private const int RB = 0x0800;
    private const int Down = 0x0040;

    private readonly RoamField field = new();
    private readonly FieldShoulderHold hold;
    private readonly ShoulderTaps taps;

    public FieldShoulderHoldTests()
    {
        hold = new FieldShoulderHold(() => FieldBindings.Default, () => ReaderBinding.Default.Buttons, new FreeRoamGate(field));
        taps = new ShoulderTaps([hold.Held], () => 0);
    }

    // The field's chords hide only their D-pad button or, for the reader, nothing; the taps keep back the shoulders.
    private int Read(int buttons, int handed) => taps.Pad(0, buttons, handed, made: true);

    private int Read(int buttons) => Read(buttons, buttons);

    [Fact]
    public void InFreeRoamTheReaderChordLbWithRbOpensNothing()
    {
        Assert.Equal([0, 0, 0, 0, 0, 0], [Read(LB), Read(LB | RB), Read(RB), Read(0), Read(0), Read(0)]);
    }

    [Fact]
    public void InFreeRoamALoneLbTapOpensTheRankingOnRelease()
    {
        Assert.Equal([0, 0, LB], [Read(LB), Read(LB), Read(0)]);
    }

    [Fact]
    public void InFreeRoamALoneRbTapOpensTheMapOnRelease()
    {
        Assert.Equal([0, RB], [Read(RB), Read(0)]);
    }

    // #688: LB with the D-pad steps the nearby list; the ranking no longer opens with it.
    [Fact]
    public void InFreeRoamTheListChordLbWithTheDpadOpensNothing()
    {
        Assert.Equal([0, 0, 0, 0], [Read(LB), Read(LB | Down, LB), Read(LB), Read(0)]);
    }

    // #835: RB held alone past the hold delay reaches the game as a hold.
    [Fact]
    public void InFreeRoamRbHeldPastTheDelayReachesTheGameAsAHold()
    {
        long now = 0;
        var timed = new ShoulderTaps([hold.Held], () => now, () => new ShoulderTapLimits(200, 50));

        Assert.Equal(0, timed.Pad(0, RB, RB, made: true));
        now = 200;
        Assert.Equal(RB, timed.Pad(0, RB, RB, made: true));
        now = 216;
        Assert.Equal(0, timed.Pad(0, 0, 0, made: true));
    }

    [Fact]
    public void OutsideFreeRoamTheShouldersReachTheGameAtOnce()
    {
        field.Roaming = false;

        Assert.Equal(0, hold.Held());
        Assert.Equal([LB, LB | RB, 0], [Read(LB), Read(LB | RB), Read(0)]);
    }

    [Fact]
    public void FreeRoamHoldsTheListsAndTheReadersShouldersOnly()
    {
        Assert.Equal(LB | RB, hold.Held());

        var rebound = new FieldShoulderHold(
            () => FieldBindings.Default with { HoldButtons = 0x0100 }, () => 0x4000 | LB, new FreeRoamGate(field));
        Assert.Equal(LB, rebound.Held());
    }

    // Not heard by ear yet, so it ships off.
    [Fact]
    public void TheFieldShoulderHoldIsStillTesting()
    {
        Assert.True(SettingKey.Of(FieldFeature.ShoulderHold).Testing);
    }

    private sealed class RoamField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public bool InFreeRoam() => Roaming;

        public FieldView? Read() => null;

        public int? MapId() => null;

        public FieldPose? ReadPose() => null;
    }
}
