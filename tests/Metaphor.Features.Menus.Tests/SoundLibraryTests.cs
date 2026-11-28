using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The settings menu's sound library (#906, #651): lists every <see cref="Cue"/> and navigation sound cue,
/// plays it on focus and on Enter/A with its spoken name and meaning, and silences preview channels on move or close.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SoundLibraryTests
{
    private const int F2 = 0x71;
    private const int Down = 0x28;
    private const int PageUp = 0x21;
    private const int Enter = 0x0D;
    private const int F = 0x46;
    private const int Escape = 0x1B;

    private readonly RecordingAnnouncer said = new();
    private readonly RecordingCues cues = new();
    private bool libraryOn = true;

    private SettingsMenu MakeMenu()
    {
        var library = new SoundLibrary(cues);
        var rows = library.Rows();
        var settings = new SettingsReloader(new SilentAnnouncer(), SettingCatalogue.Of<MenusFeature>(), _ => { });
        return new SettingsMenu(
            () => PlayerSettings.GeneralSettings,
            () => settings.Current,
            (_, _) => true,
            said,
            () => SettingsMenuBindings.Default,
            () => MenuBindings.Default,
            () => true,
            () => true)
        {
            ExtraRows = () => libraryOn ? rows : [],
            QuietPreview = library.Quiet,
        };
    }

    private static void Press(SettingsMenu menu, int key)
    {
        menu.Keys([key]);
        menu.Keys([]);
    }

    [Fact]
    public void EveryCueInTheCatalogueIsListedAndPlayable()
    {
        var listedCues = SoundLibrary.Entries.Select(entry => entry.Cue).OfType<Cue>().ToList();

        Assert.Equal(Enum.GetValues<Cue>(), listedCues);
        Assert.Throws<ArgumentOutOfRangeException>(() => SoundLibrary.ForCue((Cue)999));

        var library = new SoundLibrary(cues);
        foreach (var entry in SoundLibrary.Entries)
        {
            var before = cues.PlayedCues.Count + cues.HeldTones.Count;
            library.Play(entry);
            Assert.Equal(before + 1, cues.PlayedCues.Count + cues.HeldTones.Count);
        }
    }

    [Fact]
    public void FocusingAndActivatingSoundLibraryRowsPlaysCuesSpeaksMeaningAndSilencesOnMoveOrClose()
    {
        var menu = MakeMenu();
        Press(menu, F2);

        // PageUp from General jumps to the last section: Sound library.
        Press(menu, PageUp);
        Assert.Equal(
            $"Sound library. Ally hit, someone in your party was hit, 1 of {SoundLibrary.Entries.Count}",
            said.Said[^1].Text);
        Assert.Equal([Cue.AllyHit], cues.PlayedCues);

        // Enter replays the cue and repeats its name and meaning without saving to settings.json.
        Press(menu, Enter);
        Assert.Equal("Ally hit, someone in your party was hit", said.Said[^1].Text);
        Assert.Equal([Cue.AllyHit, Cue.AllyHit], cues.PlayedCues);

        // Details key reads the fuller description including what it sounds like.
        Press(menu, F);
        Assert.Contains("Two low notes going down", said.Said[^1].Text, StringComparison.Ordinal);

        // Step down past the 5 Cue enum rows to Guidance beep (held tone preview).
        for (var i = 0; i < Enum.GetValues<Cue>().Length; i++)
        {
            Press(menu, Down);
        }
        Assert.Contains("Guidance beep, points toward what the mod is guiding you to", said.Said[^1].Text, StringComparison.Ordinal);
        Assert.Contains(cues.HeldTones, item => item.Channel == ToneChannel.Guidance && !item.Tone.Loops && item.Tone.Sample is not null);

        // Moving to the next row silences the Guidance preview channel before playing Sonar.
        Press(menu, Down);
        Assert.Contains(ToneChannel.Guidance, cues.Silenced);
        Assert.Contains(cues.HeldTones, item => item.Channel == ToneChannel.Sonar);

        // Closing silences the active preview channel.
        Press(menu, Escape);
        Assert.Contains(ToneChannel.Sonar, cues.Silenced);
    }

    [Fact]
    public void WithSoundLibraryOffItsSectionIsOmitted()
    {
        libraryOn = false;
        var menu = MakeMenu();

        Press(menu, F2);
        Press(menu, PageUp);

        Assert.StartsWith("General.", said.Said[^1].Text, StringComparison.Ordinal);
        Assert.Empty(cues.PlayedCues);
        Assert.Empty(cues.HeldTones);
    }

    private sealed class RecordingCues : ICuePlayer
    {
        public List<Cue> PlayedCues { get; } = [];

        public List<(ToneChannel Channel, HeldTone Tone)> HeldTones { get; } = [];

        public List<ToneChannel> Silenced { get; } = [];

        public void Play(Cue cue) => PlayedCues.Add(cue);

        public void Play(Cue cue, double pan) => PlayedCues.Add(cue);

        public void Hold(ToneChannel channel, HeldTone tone) => HeldTones.Add((channel, tone));

        public void Silence(ToneChannel channel) => Silenced.Add(channel);
    }
}
