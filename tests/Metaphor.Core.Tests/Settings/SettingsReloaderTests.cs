using Metaphor.Core.Announcements;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>Applying the settings file at start and on each change (#104): a new value is announced, an unchanged one
/// is not.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsReloaderTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly List<string> logged = [];

    private SettingsReloader Reloader() => new(announcer, logged.Add);

    [Fact]
    public void StartsWithDefaults()
    {
        Assert.Equal(VoicedLinesMode.Auto, Reloader().Current.VoicedLines);
    }

    [Fact]
    public void TheStartingValueIsAppliedWithoutAnnouncing()
    {
        var reloader = Reloader();

        reloader.Load("""{ "voicedLines": "nothing" }""");

        Assert.Equal(VoicedLinesMode.Nothing, reloader.Current.VoicedLines);
        Assert.Empty(announcer.Said);
    }

    [Theory]
    [InlineData("auto", "Voiced lines: auto.")]
    [InlineData("nameOnly", "Voiced lines: name only.")]
    [InlineData("fullLine", "Voiced lines: full line.")]
    [InlineData("nothing", "Voiced lines: nothing.")]
    public void AChangeIsAnnounced(string value, string said)
    {
        var reloader = Reloader();
        reloader.Load("""{ "voicedLines": "fullLine" }""");
        if (value == "fullLine")
        {
            reloader.Load("""{ "voicedLines": "nothing" }""");
        }
        var before = announcer.Said.Count;

        reloader.Reload($$"""{ "voicedLines": "{{value}}" }""");

        Assert.Equal(new Announcement(said, Interrupt: true), announcer.Said[before]);
    }

    [Fact]
    public void AnUnchangedValueIsNotAnnounced()
    {
        var reloader = Reloader();
        reloader.Load("""{ "voicedLines": "nameOnly" }""");

        reloader.Reload("""{ "voicedLines": "nameOnly" }""");

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ADeletedFileGoesBackToDefaultsAndSaysSo()
    {
        var reloader = Reloader();
        reloader.Load("""{ "voicedLines": "nothing" }""");

        reloader.Reload(null);

        Assert.Equal(VoicedLinesMode.Auto, reloader.Current.VoicedLines);
        Assert.Equal([new Announcement("Voiced lines: auto.", Interrupt: true)], announcer.Said);
    }

    [Theory]
    [InlineData(false, "Update check: off.")]
    [InlineData(true, "Update check: on.")]
    public void AChangedUpdateCheckIsAnnounced(bool value, string said)
    {
        var reloader = Reloader();
        reloader.Load($$"""{ "checkForUpdates": {{(!value).ToString().ToLowerInvariant()}} }""");

        reloader.Reload($$"""{ "checkForUpdates": {{value.ToString().ToLowerInvariant()}} }""");

        Assert.Equal(value, reloader.Current.CheckForUpdates);
        Assert.Equal([new Announcement(said, Interrupt: true)], announcer.Said);
    }

    // Both said, the second after the first.
    [Fact]
    public void TwoChangesAreBothAnnounced()
    {
        var reloader = Reloader();

        reloader.Reload("""{ "voicedLines": "nothing", "checkForUpdates": false }""");

        Assert.Equal(
            [new Announcement("Voiced lines: nothing.", Interrupt: true), new Announcement("Update check: off.", Interrupt: false)],
            announcer.Said);
    }

    [Fact]
    public void TurningSoundCuesOffOrOnIsSaid()
    {
        var reloader = Reloader();
        reloader.Load("{}");

        reloader.Reload("""{ "soundCues": false }""");
        reloader.Reload("""{ "soundCues": true }""");

        Assert.Equal(
            [new Announcement("Sound cues: off.", Interrupt: true), new Announcement("Sound cues: on.", Interrupt: true)],
            announcer.Said);
    }

    [Fact]
    public void ANewSoundCueVolumeIsSaid()
    {
        var reloader = Reloader();
        reloader.Load("{}");

        reloader.Reload("""{ "soundCueVolume": 80 }""");

        Assert.Equal(80, reloader.Current.SoundCueVolume);
        Assert.Equal([new Announcement("Sound cue volume: 80.", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void SeveralChangesAreSaidInTurnTheFirstInterrupting()
    {
        var reloader = Reloader();
        reloader.Load("{}");

        reloader.Reload("""{ "voicedLines": "nothing", "soundCues": false, "soundCueVolume": 20 }""");

        Assert.Equal(
            [
                new Announcement("Voiced lines: nothing.", Interrupt: true),
                new Announcement("Sound cues: off.", Interrupt: false),
                new Announcement("Sound cue volume: 20.", Interrupt: false),
            ],
            announcer.Said);
    }

    [Fact]
    public void ABrokenFileFallsBackToDefaultsAndLogs()
    {
        var reloader = Reloader();
        reloader.Load("""{ "voicedLines": "nothing" }""");

        reloader.Reload("{ oops");

        Assert.Equal(VoicedLinesMode.Auto, reloader.Current.VoicedLines);
        Assert.Single(logged);
    }
}
