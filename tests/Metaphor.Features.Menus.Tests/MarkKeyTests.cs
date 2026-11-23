using Metaphor.Core;
using Metaphor.Core.Cues;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The mark key (#597): each press writes a numbered marker to the report log and plays the mark cue, and the
/// key never reaches the game while the feature is on.</summary>
[Trait("Category", "Unit")]
public sealed class MarkKeyTests
{
    private const int F8 = 0x77;
    private const int W = 0x57;

    private readonly Report report = new();
    private readonly Cues cues = new();
    private ModSettings settings = ModSettings.Defaults;

    private MarkKey Key() => new(() => settings, cues, report);

    [Fact]
    public void ThePressWritesAMarkerAndPlaysTheCue()
    {
        Key().Keys([F8]);

        Assert.Equal([1], report.Marks);
        Assert.Equal([Cue.Mark], cues.Played);
    }

    [Fact]
    public void HoldingTheKeyIsOnePressAndEachNewPressCountsUp()
    {
        var key = Key();

        key.Keys([F8]);
        key.Keys([F8]);
        key.Keys([]);
        key.Keys([W, F8]);

        Assert.Equal([1, 2], report.Marks);
    }

    [Fact]
    public void TheKeyNeverReachesTheGameAndOtherKeysDo()
    {
        Assert.Equal([W], Key().Keys([W, F8]));
    }

    [Fact]
    public void TheDefaultKeyIsF8() => Assert.Equal(F8, MarkKey.DefaultKey);

    [Fact]
    public void TheSettingsFileMovesTheKey()
    {
        settings = ModSettings.Parse(
            """{ "keys": { "report": { "markKey": "F9" } } }""", SettingCatalogue.Of<MenusFeature>(), _ => { });
        var key = Key();

        Assert.Equal([F8], key.Keys([F8]));
        key.Keys([0x78]);

        Assert.Equal([1], report.Marks);
    }

    [Fact]
    public void TurnedOffTheKeyGoesToTheGameAndNothingIsWritten()
    {
        settings = ModSettings.Parse(
            """{ "report": { "markKey": false } }""", SettingCatalogue.Of<MenusFeature>(), _ => { });

        Assert.Equal([F8], Key().Keys([F8]));
        Assert.Empty(report.Marks);
        Assert.Empty(cues.Played);
    }

    [Fact]
    public void PadReadsPassThroughUntilTheControllerLayerLands() => Assert.Equal(0x0400, Key().Pad(0, 0x0400, made: true));

    private sealed class Report : ISessionReport
    {
        public List<int> Marks { get; } = [];

        public void Mark(int number) => Marks.Add(number);

        public void Silent(ScreenEvent change)
        {
        }

        public void SilentListFull(int limit)
        {
        }
    }

    private sealed class Cues : ICuePlayer
    {
        public List<Cue> Played { get; } = [];

        public void Play(Cue cue) => Played.Add(cue);

        public void Play(Cue cue, double pan) => Played.Add(cue);

        public void Hold(ToneChannel channel, HeldTone tone)
        {
        }

        public void Silence(ToneChannel channel)
        {
        }
    }
}
