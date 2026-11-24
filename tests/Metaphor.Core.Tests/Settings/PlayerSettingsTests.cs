using Metaphor.Core.Features;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>The settings menu's rows (#754): every player setting, grouped as the settings reference lists them, each
/// changed by its kind: a toggle turns over, a choice steps round, a number moves by its step within its range, a key
/// only says itself.</summary>
[Trait("Category", "Unit")]
public sealed class PlayerSettingsTests
{
    private static readonly SliderLimit Wait = new("pad", "wait", 30, 10, 90, 5) { Spoken = "Wait", Order = 1 };

    private static ModSettings Parse(string json) => ModSettings.Parse(json, SampleSettings.Catalogue, _ => { });

    private static IReadOnlyList<PlayerSetting> Rows(Func<Enum, bool>? hidden = null) =>
        PlayerSettings.Rows(
            SettingCatalogue.From([new SampleModule()]),
            [new KeySetting(PlayerSettings.Keys, "Repeat", _ => "R")], hidden);

    [Fact]
    public void RowsFollowTheReferencesSections()
    {
        var sections = Rows().Select(row => row.Section).Distinct().ToList();

        Assert.Equal(["General", "Speech", "Field", "Sounds", "Limits", "Keys"], sections);
    }

    [Fact]
    public void AToggleIsOffOnTheLeftAndOnOnTheRight()
    {
        var menus = Row("Menus");
        var off = Parse("""{ "speech": { "menus": false } }""");

        Assert.Equal("on", menus.Value(ModSettings.Defaults));
        Assert.Equal("speech.menus", PathOf(menus.Change(ModSettings.Defaults, -1)));
        Assert.Equal("false", menus.Change(ModSettings.Defaults, -1)!.Json);
        Assert.Null(menus.Change(ModSettings.Defaults, 1));
        Assert.Equal("true", menus.Change(off, 1)!.Json);
        Assert.Null(menus.Change(off, -1));
    }

    [Fact]
    public void EnterTurnsAToggleOverAndStepsAChoiceRound()
    {
        Assert.Equal("false", Row("Menus").Activate(ModSettings.Defaults)!.Json);
        Assert.Equal("true", Row("Menus").Activate(Parse("""{ "speech": { "menus": false } }"""))!.Json);
        Assert.Equal("\"auto\"", Row("Voiced lines").Activate(Parse("""{ "voicedLines": "nothing" }"""))!.Json);
        Assert.Equal("35", Row("Wait").Activate(ModSettings.Defaults)!.Json);
    }

    // #959: one rule for every setting, so a player never has to learn which way a row runs. Left never raises a
    // value or turns one on, Right never lowers one or turns one off, and neither wraps past an end. Idea credit:
    // Arcana 0.5.0's release notes (github.com/ChipsAhoiMcCoy/arcana); no code taken.
    [Fact]
    public void EverySettingRunsOffOrLowOnTheLeftAndOnOrHighOnTheRight()
    {
        foreach (var row in Rows().Where(row => row is not KeySetting))
        {
            foreach (var steps in new[] { 1, 5 })
            {
                var at = ModSettings.Defaults;
                for (var i = 0; i < 40 && row.Change(at, -steps) is { } left; i++)
                {
                    var next = Saved(left);
                    Assert.True(Rank(row, next) < Rank(row, at), $"{row.Spoken}: Left raised it");
                    at = next;
                }
                Assert.Equal(Lowest(row), Rank(row, at));
                for (var i = 0; i < 40 && row.Change(at, steps) is { } right; i++)
                {
                    var next = Saved(right);
                    Assert.True(Rank(row, next) > Rank(row, at), $"{row.Spoken}: Right lowered it");
                    at = next;
                }
                Assert.Equal(Highest(row), Rank(row, at));
            }
        }
    }

    // Where a value sits on its row's line, from left to right.
    private static int Rank(PlayerSetting row, ModSettings at) => row switch
    {
        ToggleSetting toggle => toggle.Read(at) ? 1 : 0,
        ChoiceSetting choice => choice.Read(at),
        NumberSetting number => number.Read(at),
        _ => 0,
    };

    private static int Lowest(PlayerSetting row) => row is NumberSetting number ? number.Min : 0;

    private static int Highest(PlayerSetting row) => row switch
    {
        ToggleSetting => 1,
        ChoiceSetting choice => choice.Names.Count - 1,
        NumberSetting number => number.Max,
        _ => 0,
    };

    // The settings after one change, as the file would read with only that row set: each walk moves one row only.
    private static ModSettings Saved(SettingChange change)
    {
        var json = change.Json;
        for (var i = change.Path.Count - 1; i >= 0; i--)
        {
            json = $"{{ \"{change.Path[i]}\": {json} }}";
        }
        return Parse(json);
    }

    [Fact]
    public void ATestingFeatureIsMarkedAndOffByDefault()
    {
        var fresh = Row("Fresh");

        Assert.True(fresh.Testing);
        Assert.Equal("off", fresh.Value(ModSettings.Defaults));
    }

    [Fact]
    public void AChoiceStepsBothWaysAndStopsAtItsEnds()
    {
        var voiced = Row("Voiced lines");

        Assert.Equal("auto", voiced.Value(ModSettings.Defaults));
        Assert.Equal("\"nameOnly\"", voiced.Change(ModSettings.Defaults, 1)!.Json);
        Assert.Null(voiced.Change(ModSettings.Defaults, -1));
        Assert.Null(voiced.Change(Parse("""{ "voicedLines": "nothing" }"""), 1));
        Assert.Equal("full line", voiced.Value(Parse("""{ "voicedLines": "fullLine" }""")));
    }

    [Fact]
    public void ANumberMovesByItsStepAndStopsAtItsEnds()
    {
        var wait = Row("Wait");

        Assert.Equal("30", wait.Value(ModSettings.Defaults));
        Assert.Equal("limits.pad.wait", PathOf(wait.Change(ModSettings.Defaults, 1)));
        Assert.Equal("35", wait.Change(ModSettings.Defaults, 1)!.Json);
        Assert.Equal("55", wait.Change(ModSettings.Defaults, 5)!.Json);
        Assert.Equal("90", wait.Change(ModSettings.Defaults, 100)!.Json);
        Assert.Null(wait.Change(Parse("""{ "limits": { "pad": { "wait": 10 } } }"""), -1));
    }

    [Fact]
    public void ACuesOnAndVolumeAreRowsOfTheirOwn()
    {
        var volume = Row("Mark sound volume");

        Assert.Equal("sounds.mark.volume", PathOf(volume.Change(ModSettings.Defaults, -1)));
        Assert.Equal("95", volume.Change(ModSettings.Defaults, -1)!.Json);
        Assert.Equal("sounds.mark.on", PathOf(Row("Mark sound").Change(ModSettings.Defaults, -1)));
    }

    private static string PathOf(SettingChange? change) => string.Join('.', change!.Path);

    [Fact]
    public void AKeyIsSaidButNeverChanged()
    {
        var repeat = Row("Repeat");

        Assert.Equal("R", repeat.Value(ModSettings.Defaults));
        Assert.Null(repeat.Change(ModSettings.Defaults, 1));
    }

    [Fact]
    public void AHiddenFeatureAndTheWelcomeFlagAreNotListed()
    {
        var rows = Rows(feature => feature is SampleFeature.Dialogue);

        Assert.DoesNotContain(rows, row => row.Spoken == "Dialogue");
        Assert.DoesNotContain(rows, row => row.Spoken.Contains("elcome", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Spoken == "Menus");
    }

    private static PlayerSetting Row(string spoken) => Rows().Single(row => row.Spoken == spoken);

    private sealed class SampleModule : IFeatureModule
    {
        public IEnumerable<Enum> Settings => Enum.GetValues<SampleFeature>().Select(feature => (Enum)feature);

        public IEnumerable<SliderLimit> Limits => [Wait];

        public void Register(FeatureContext context)
        {
        }
    }
}
