using Metaphor.Core.Cues;

namespace Metaphor.Features.Field.Tests;

/// <summary>Where the kit's positional cues sound from (#903, #650): muffled and a little quieter past the behind angle
/// while the behind-muffle is on, the pan narrowed by height while the height pan is on, and the plain frame otherwise.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CuePlacementTests
{
    private static readonly HeldTone Tone = new(659, 0, 80, Broadband: true);

    private bool muffle = true;
    private int degrees = 100;
    private bool height;

    private CuePlacement Placement => new(() => muffle, () => degrees, () => height);

    [Theory]
    [InlineData(99, false)]
    [InlineData(101, true)]
    [InlineData(-101, true)]
    [InlineData(180, true)]
    [InlineData(-99, false)]
    [InlineData(0, false)]
    public void ASourcePastTheAngleIsMuffledAndALittleQuieter(double bearingDegrees, bool behind)
    {
        var placed = Placement.Place(Tone, bearingDegrees * Math.PI / 180);

        Assert.Equal(behind, placed.Muffled);
        Assert.Equal(behind ? 80 * CuePlacement.MuffledShare / 100 : 80, placed.Volume);
        Assert.Equal(CameraFrame.Pan(bearingDegrees * Math.PI / 180), placed.Pan, 12);
        Assert.True(placed.Broadband);
    }

    [Fact]
    public void TheAngleComesFromItsLimit()
    {
        degrees = 150;

        Assert.False(Placement.Place(Tone, 140 * Math.PI / 180).Muffled);
        Assert.True(Placement.Place(Tone, 160 * Math.PI / 180).Muffled);
        Assert.Equal(100, CuePlacement.MuffleDegrees.Default);
        Assert.Equal("field.behindMuffleDegrees", CuePlacement.MuffleDegrees.Path);
    }

    [Fact]
    public void OffNothingIsMuffledAndThePlainFrameIsThePlainPan()
    {
        muffle = false;

        Assert.Equal(Tone with { Pan = CameraFrame.Pan(Math.PI) }, Placement.Place(Tone, Math.PI));
        Assert.Equal(Tone with { Pan = -1 }, CuePlacement.Plain.Place(Tone, Math.PI / 2, Math.PI / 3));
    }

    [Fact]
    public void TheHeightPanNarrowsTheSideBySourcesAboveAndBelow()
    {
        // A source at the left, 60° up: half the pan, only while the height pan is on.
        var pose = new FieldPose(0, 0, 0, 0, CameraHeading: 0);
        var up = 300 * Math.Sqrt(3);

        Assert.Equal(-1, Placement.Toward(Tone, pose, 300, up, 0).Pan, 6);
        height = true;
        Assert.Equal(-0.5, Placement.Toward(Tone, pose, 300, up, 0).Pan, 6);
        Assert.Equal(-0.5, Placement.Toward(Tone, pose, 300, -up, 0).Pan, 6);
    }
}
