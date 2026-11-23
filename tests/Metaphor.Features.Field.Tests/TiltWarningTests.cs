using Metaphor.Core.Announcements;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field.Tests;

/// <summary>The camera-tilt warning (#828): once per crossing of a limit, a margin before "Camera level", nothing out
/// of free roam, and the on-demand line. A stand-in field only.</summary>
[Trait("Category", "Unit")]
public sealed class TiltWarningTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly TiltField field = new();
    private readonly TiltWarning warning;
    private TiltLimits limits = new(DownDegrees: 40, UpDegrees: 20, MarginDegrees: 5);
    private long now = 1000;

    public TiltWarningTests() => warning = new TiltWarning(field, announcer, () => now, () => limits);

    private List<string> Said() => [.. announcer.Said.Select(said => said.Text)];

    // Moves time past the check interval and checks at a pitch in degrees, negative looking down.
    private void At(double degrees)
    {
        field.Pitch = degrees * Math.PI / 180;
        now += TiltWarning.CheckIntervalMilliseconds;
        warning.Check();
    }

    [Fact]
    public void CrossingTheDownLimitIsSaidOnceAndLevelOnReturnPastTheMargin()
    {
        At(-10);
        At(-41);
        At(-50);
        At(-37);
        At(-41);
        At(-34);

        Assert.Equal(["Camera looking down", "Camera level"], Said());
    }

    [Fact]
    public void CrossingTheUpLimitIsSaidAndSwingingStraightDownSaysDown()
    {
        At(21);
        At(16);
        At(-45);

        Assert.Equal(["Camera looking up", "Camera looking down"], Said());
    }

    [Fact]
    public void TheLimitsAreReadOnEachCheck()
    {
        At(-35);
        limits = limits with { DownDegrees = 30 };
        At(-35);

        Assert.Equal(["Camera looking down"], Said());
    }

    [Fact]
    public void NothingIsSaidOutOfFreeRoamOrWithoutAPitch()
    {
        field.Roaming = false;
        At(-60);
        field.Roaming = true;
        field.Unread = true;
        At(-60);

        Assert.Empty(Said());
    }

    [Fact]
    public void ChecksComeAtMostOncePerInterval()
    {
        At(0);
        field.Pitch = -Math.PI / 3;
        now += 1;
        warning.Check();

        Assert.Empty(Said());
    }

    [Theory]
    [InlineData(-12, "Camera level, 12 degrees down")]
    [InlineData(0, "Camera level")]
    [InlineData(8, "Camera level, 8 degrees up")]
    [InlineData(-45, "Camera looking down, 45 degrees")]
    [InlineData(25, "Camera looking up, 25 degrees")]
    public void TheLineSaysTheTilt(double degrees, string line)
    {
        field.Pitch = degrees * Math.PI / 180;

        Assert.Equal(line, warning.Line());
    }

    [Fact]
    public void TheLineSaysWhenTheCameraIsUnread()
    {
        field.Unread = true;

        Assert.Equal("Camera tilt unknown", warning.Line());
    }

    [Fact]
    public void LimitsComeFromTheSettingsFileWithinRange()
    {
        var parsed = ModSettings.Parse(
            """{ "limits": { "field": { "cameraDownDegrees": 50, "cameraUpDegrees": 200, "cameraMarginDegrees": -3 } } }""",
            SettingCatalogue.Empty, _ => { });

        Assert.Equal(new TiltLimits(50, 89, 0), TiltLimits.From(parsed));
        Assert.Equal(TiltLimits.Default, TiltLimits.From(ModSettings.Defaults));
    }

    private sealed class TiltField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public bool Unread { get; set; }

        public double Pitch { get; set; }

        public bool InFreeRoam() => Roaming;

        public FieldView? Read() => null;

        public int? MapId() => 1;

        public FieldPose? ReadPose() => new FieldPose(0, 0, 0, 0, 0, Unread ? null : Pitch);
    }
}
