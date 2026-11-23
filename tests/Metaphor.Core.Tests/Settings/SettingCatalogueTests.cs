using Metaphor.Core.Features;
using Metaphor.Core.Settings;

namespace Metaphor.Core.Tests.Settings;

/// <summary>The settings the feature modules declare, gathered for the settings reader (#784).</summary>
[Trait("Category", "Unit")]
public sealed class SettingCatalogueTests
{
    [Fact]
    public void TheCatalogueListsEveryModulesSettingsInTheirOrder()
    {
        var catalogue = SettingCatalogue.From([new Module([SampleFeature.Fresh, SampleFeature.Dialogue]),
            new Module([SampleFeature.Nearby]), new Module([])]);

        Assert.Equal([SampleFeature.Dialogue, SampleFeature.Nearby, SampleFeature.Fresh], catalogue.Features);
    }

    [Fact]
    public void TwoSettingsWithOneKeyAreAMistake()
    {
        var thrown = Assert.Throws<ArgumentException>(() => SettingCatalogue.From(
            [new Module([SampleFeature.Menus]), new Module([Twin.Menus])]));

        Assert.Contains("speech.menus", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASettingWithNoKeyIsAMistake() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SettingCatalogue.From([DayOfWeek.Monday]));

    [Fact]
    public void TheReaderBindingIsTheDialoguesUnlessTheFileMovesIt()
    {
        var log = new List<string>();

        Assert.Equal(new ReaderBinding(0xC00, 0x54), ReaderBinding.From(ModSettings.Defaults));
        Assert.Equal(
            new ReaderBinding(0xC00, 0x51),
            ReaderBinding.From(ModSettings.Parse(
                """{ "keys": { "dialogue": { "readerKey": "Q" } } }""", SettingCatalogue.Empty, log.Add)));
        Assert.Empty(log);
    }

    private enum Twin
    {
        [SettingKey("speech", "menus", "Menus again")]
        Menus,
    }

    private sealed class Module(Enum[] settings) : IFeatureModule
    {
        public IEnumerable<Enum> Settings => settings;

        public void Register(FeatureContext context)
        {
        }
    }
}
