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
    public void AToggleTurnsOverEitherWay()
    {
        var menus = Row("Menus");

        Assert.Equal("on", menus.Value(ModSettings.Defaults));
        Assert.Equal("speech.menus", PathOf(menus.Change(ModSettings.Defaults, -1)));
        Assert.Equal("false", menus.Change(ModSettings.Defaults, 1)!.Json);
        Assert.Equal("true", menus.Change(Parse("""{ "speech": { "menus": false } }"""), -1)!.Json);
    }

    [Fact]
    public void ATestingFeatureIsMarkedAndOffByDefault()
    {
        var fresh = Row("Fresh");

        Assert.True(fresh.Testing);
        Assert.Equal("off", fresh.Value(ModSettings.Defaults));
    }

    [Fact]
    public void AChoiceStepsRoundBothWays()
    {
        var voiced = Row("Voiced lines");

        Assert.Equal("auto", voiced.Value(ModSettings.Defaults));
        Assert.Equal("\"nameOnly\"", voiced.Change(ModSettings.Defaults, 1)!.Json);
        Assert.Equal("\"nothing\"", voiced.Change(ModSettings.Defaults, -1)!.Json);
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
        Assert.Equal("sounds.mark.on", PathOf(Row("Mark sound").Change(ModSettings.Defaults, 1)));
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
