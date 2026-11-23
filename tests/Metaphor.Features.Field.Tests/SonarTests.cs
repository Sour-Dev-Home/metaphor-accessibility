using Metaphor.Core.Cues;

namespace Metaphor.Features.Field.Tests;

/// <summary>The sonar (#827): one ping per nearby person, object and exit, a pitch per kind, panned in the
/// camera-relative frame, quieter farther off, and silent when off or out of free roam. A stand-in field only.</summary>
[Trait("Category", "Unit")]
public sealed class SonarTests
{
    private readonly SonarField field = new();
    private readonly ToneLog cues = new();
    private readonly List<string> logged = [];
    private bool on = true;
    private long now = 1000;
    private readonly Sonar sonar;

    public SonarTests() => sonar = new Sonar(field, cues, () => now, () => on, logged.Add);

    // Ticks every 10 ms for the given time, as the keyboard reads do.
    private void Run(long milliseconds)
    {
        for (var end = now + milliseconds; now < end; now += 10)
        {
            sonar.Tick();
        }
    }

    [Fact]
    public void EachKindHasItsOwnSound()
    {
        Assert.Equal(3, new[] { SonarKind.People, SonarKind.Objects, SonarKind.Exits }
            .Select(Sonar.Hertz).Distinct().Count());
        Assert.Equal(SonarKind.People, Sonar.KindOf(NearbyKind.Person));
        Assert.Equal(SonarKind.Objects, Sonar.KindOf(NearbyKind.Chest));
        Assert.Equal(SonarKind.Objects, Sonar.KindOf(NearbyKind.Item));
        Assert.Equal(SonarKind.Objects, Sonar.KindOf(NearbyKind.Examine));
        Assert.Equal(SonarKind.Exits, Sonar.KindOf(NearbyKind.Door));
        Assert.Equal(SonarKind.Exits, Sonar.KindOf(NearbyKind.Exit));
        Assert.Null(Sonar.KindOf(NearbyKind.Enemy));
    }

    [Fact]
    public void VolumeFallsWithDistance()
    {
        Assert.Equal(Sonar.NearVolume, Sonar.Volume(0));
        Assert.Equal(Sonar.NearVolume, Sonar.Volume(Beacon.ArrivalDistance));
        Assert.Equal(Sonar.FarVolume, Sonar.Volume(Nearby.Radius));
        Assert.True(Sonar.Volume(500) > Sonar.Volume(2000));
    }

    [Fact]
    public void ASweepPingsEachThingNearestFirstWithItsKindsSoundPannedFromTheCamera()
    {
        // The character faces the camera (heading π); the camera looks along +z, so +x is on the screen's left.
        field.Pose = new FieldPose(0, 0, 0, Math.PI, CameraHeading: 0);
        field.Units =
        [
            new FieldUnit(FieldKind.Door, -1000, 0, 0),
            new FieldUnit(FieldKind.Person, 300, 0, 0),
            new FieldUnit(FieldKind.Enemy, 0, 0, 200),
            new FieldUnit(FieldKind.Chest, 0, 0, 600),
        ];

        Run(Sonar.SweepMilliseconds - 10);

        Assert.Collection(cues.Held,
            person =>
            {
                Assert.Equal(Sonar.Hertz(SonarKind.People), person.Hertz);
                Assert.Equal(-1, person.Pan, 6);
            },
            chest =>
            {
                Assert.Equal(Sonar.Hertz(SonarKind.Objects), chest.Hertz);
                Assert.Equal(0, chest.Pan, 6);
            },
            door =>
            {
                Assert.Equal(Sonar.Hertz(SonarKind.Exits), door.Hertz);
                Assert.Equal(1, door.Pan, 6);
                Assert.True(door.Volume < cues.Held[0].Volume);
            });
        Assert.True(cues.Silenced >= 3);
        Assert.All(cues.Channels, channel => Assert.Equal(ToneChannel.Sonar, channel));
    }

    [Fact]
    public void SweepsRepeat()
    {
        field.Units = [new FieldUnit(FieldKind.Person, 300, 0, 0)];

        Run(Sonar.SweepMilliseconds * 3);

        Assert.Equal(3, cues.Held.Count);
    }

    [Fact]
    public void APingPansFromWhereThePlayerIsWhenItSounds()
    {
        field.Units = [new FieldUnit(FieldKind.Person, 300, 0, 0), new FieldUnit(FieldKind.Chest, 0, 0, 600)];
        sonar.Tick();
        // The player steps past the chest before its ping: it is now behind, centred.
        field.Pose = new FieldPose(0, 0, 900, 0, CameraHeading: 0);
        Run(Sonar.PingGapMilliseconds + 10);

        Assert.Equal(2, cues.Held.Count);
        Assert.Equal(0, cues.Held[1].Pan, 6);
        Assert.Equal(Sonar.Volume(300), cues.Held[1].Volume);
    }

    [Fact]
    public void TheFirstPingOfEachThingIsLoggedWithItsDistance()
    {
        field.Units = [new FieldUnit(FieldKind.Person, 300, 0, 400, Id: "npc-1-2")];

        Run(Sonar.SweepMilliseconds * 2);

        var line = Assert.Single(logged);
        Assert.Contains("sonar first kind=People id=npc-1-2 distance=500", line, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingPlaysWhenOffOrOutOfFreeRoam()
    {
        field.Units = [new FieldUnit(FieldKind.Person, 300, 0, 0)];
        on = false;
        Run(Sonar.SweepMilliseconds);
        on = true;
        field.Roaming = false;
        Run(Sonar.SweepMilliseconds);

        Assert.Empty(cues.Held);
    }

    [Fact]
    public void LeavingFreeRoamMidPingSilencesIt()
    {
        field.Units = [new FieldUnit(FieldKind.Person, 300, 0, 0)];
        sonar.Tick();
        Assert.Single(cues.Held);

        field.Roaming = false;
        now += 10;
        sonar.Tick();

        Assert.Equal(1, cues.Silenced);
    }

    private sealed class SonarField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public FieldPose Pose { get; set; } = new(0, 0, 0, 0, CameraHeading: 0);

        public IReadOnlyList<FieldUnit> Units { get; set; } = [];

        public bool InFreeRoam() => Roaming;

        public FieldView? Read() =>
            new(Pose.X, Pose.Y, Pose.Z, Pose.Heading, Units, MapId: 1, CameraHeading: Pose.CameraHeading);

        public int? MapId() => 1;

        public FieldPose? ReadPose() => Pose;
    }

    private sealed class ToneLog : ICuePlayer
    {
        public List<HeldTone> Held { get; } = [];

        public List<ToneChannel> Channels { get; } = [];

        public int Silenced { get; private set; }

        public void Play(Cue cue)
        {
        }

        public void Play(Cue cue, double pan)
        {
        }

        public void Hold(ToneChannel channel, HeldTone tone)
        {
            Channels.Add(channel);
            Held.Add(tone);
        }

        public void Silence(ToneChannel channel)
        {
            Channels.Add(channel);
            Silenced++;
        }
    }
}
