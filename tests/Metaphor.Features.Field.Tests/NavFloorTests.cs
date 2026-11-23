using Metaphor.Core.Cues;

namespace Metaphor.Features.Field.Tests;

/// <summary>The navigation sounds take turns (#823): the sonar waits for the guidance beep and a route ping, and the
/// wall tones duck under them.</summary>
[Trait("Category", "Unit")]
public sealed class NavFloorTests
{
    private bool guiding;
    private long now = 1000;
    private readonly NavFloor floor;

    public NavFloorTests() => floor = new NavFloor(() => now, () => guiding);

    [Fact]
    public void AnOpenFloorLetsEverythingPlay()
    {
        Assert.True(floor.SonarMayPing());
        Assert.Equal(100, floor.WallPercent());
    }

    [Fact]
    public void TheGuidanceBeepHoldsTheSonarAndDucksTheWalls()
    {
        guiding = true;

        Assert.False(floor.SonarMayPing());
        Assert.Equal(NavFloor.DuckedPercent, floor.WallPercent());
    }

    [Fact]
    public void ARoutePingHoldsTheFloorForAWhile()
    {
        floor.Hold();
        now += NavFloor.PingHoldMilliseconds - 1;
        Assert.False(floor.SonarMayPing());
        Assert.Equal(NavFloor.DuckedPercent, floor.WallPercent());

        now += 1;

        Assert.True(floor.SonarMayPing());
        Assert.Equal(100, floor.WallPercent());
    }

    [Fact]
    public void TheSonarWaitsWhileTheFloorIsHeld()
    {
        var field = new NavField { Units = [new FieldUnit(FieldKind.Person, 300, 0, 0)] };
        var cues = new ChannelCues();
        var sonar = new Sonar(field, cues, () => now, () => true, floor: floor);
        guiding = true;

        for (var end = now + Sonar.SweepMilliseconds; now < end; now += 10)
        {
            sonar.Tick();
        }
        Assert.Empty(cues.Held(ToneChannel.Sonar));

        guiding = false;
        sonar.Tick();

        Assert.Single(cues.Held(ToneChannel.Sonar));
    }
}
