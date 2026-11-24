using Metaphor.Core.Audio;

namespace Metaphor.Core.Tests.Audio;

[Trait("Category", "Unit")]
public sealed class VolumeDuckTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("metaphor-duck-");
    private readonly FakeVolume _volume = new() { Level = 0.8f };

    public void Dispose() => _folder.Delete(recursive: true);

    private string Marker => Path.Combine(_folder.FullName, VolumeDuck.MarkerName);

    private VolumeDuck Duck() => new(_volume, _folder.FullName);

    [Fact]
    public void DuckingLowersTheLevelAndLeavesAMarkerWithTheOldOne()
    {
        Duck().Duck();

        Assert.Equal(0.8f * VolumeDuck.DuckedShare, _volume.Level, 5);
        Assert.Equal("0.8", File.ReadAllText(Marker));
    }

    [Fact]
    public void RestoringPutsTheLevelBackAndRemovesTheMarker()
    {
        var duck = Duck();
        duck.Duck();

        duck.Restore();

        Assert.Equal(0.8f, _volume.Level);
        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public void ASecondDuckDoesNotLowerTheLevelAgain()
    {
        var duck = Duck();
        duck.Duck();
        duck.Duck();

        duck.Restore();

        Assert.Equal(0.8f, _volume.Level);
    }

    [Fact]
    public void RestoringWhenNotDuckedChangesNothing()
    {
        Duck().Restore();

        Assert.Equal(0.8f, _volume.Level);
        Assert.Equal(0, _volume.Sets);
    }

    [Fact]
    public void AnUnreadableLevelIsNotDucked()
    {
        _volume.Readable = false;

        Duck().Duck();

        Assert.Equal(0, _volume.Sets);
        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public void AFailedDuckLeavesNoMarker()
    {
        _volume.Settable = false;

        Duck().Duck();

        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public void AStartAfterACrashWhileDuckedPutsTheLevelBack()
    {
        Duck().Duck();

        // The run ended while ducked; the next start's duck finds its marker.
        Duck().RestoreLeftover();

        Assert.Equal(0.8f, _volume.Level);
        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public void AStartWithNoMarkerChangesNothing()
    {
        Duck().RestoreLeftover();

        Assert.Equal(0, _volume.Sets);
    }

    [Theory]
    [InlineData("loud")]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    public void AMarkerThatIsNotALevelIsDroppedUnused(string text)
    {
        File.WriteAllText(Marker, text);

        Duck().RestoreLeftover();

        Assert.Equal(0, _volume.Sets);
        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public void AMarkerWhoseLevelCannotBeSetIsKeptForTheNextStart()
    {
        File.WriteAllText(Marker, "0.5");
        _volume.Settable = false;

        Duck().RestoreLeftover();

        Assert.True(File.Exists(Marker));
    }

    [Fact]
    public void ADuckAfterAnUnrestoredLeftoverKeepsTheOriginalLevel()
    {
        File.WriteAllText(Marker, "0.8");
        _volume.Level = 0.24f;
        var duck = Duck();
        _volume.Settable = false;
        duck.RestoreLeftover();
        _volume.Settable = true;

        duck.Duck();
        duck.Restore();

        Assert.Equal(0.8f, _volume.Level);
    }

    private sealed class FakeVolume : IAppVolume
    {
        public float Level { get; set; }

        public bool Readable { get; set; } = true;

        public bool Settable { get; set; } = true;

        public int Sets { get; private set; }

        public float? Read() => Readable ? Level : null;

        public bool Write(float level)
        {
            Sets++;
            if (Settable)
            {
                Level = level;
            }
            return Settable;
        }
    }
}
