using Metaphor.Core.Cues;
using Metaphor.Core.Settings;
using Metaphor.Features.Battle;
using Metaphor.Features.Dialogue;
using Metaphor.Features.Field;
using Metaphor.Features.Menus;
using Metaphor.Features.Updates;
using Metaphor.SettingsReference;

namespace Metaphor.Settings.Tests;

/// <summary>A toggle for every feature (#661), declared in its own project (#784): each has its own key, the keys
/// and their places on the page are unique, and every feature is on by default unless it is still being tested.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FeatureDefaultsTests
{
    private static readonly SettingCatalogue Features = AllFeatures.Catalogue();

    private readonly List<string> logged = [];

    [Fact]
    public void EveryFeatureAndCueHasItsOwnKeySpokenNameAndDescription()
    {
        var keys = Features.Features.Select(SettingKey.Of).ToList();
        var all = keys.Concat(Enum.GetValues<Cue>().Select(cue => SettingKey.Of(cue))).ToList();

        Assert.All(all, key => Assert.False(string.IsNullOrWhiteSpace(key.Spoken)));
        Assert.All(all, key => Assert.False(string.IsNullOrWhiteSpace(key.Description)));
        Assert.Equal(all.Count, all.Select(key => key.Path).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(keys.Count, keys.Select(key => key.Order).Distinct().Count());
    }

    [Fact]
    public void EveryFeatureEnumIsInTheCatalogue()
    {
        Enum[] declared =
        [
            .. Enum.GetValues<BattleFeature>().Select(feature => (Enum)feature),
            .. Enum.GetValues<DialogueFeature>().Select(feature => (Enum)feature),
            .. Enum.GetValues<FieldFeature>().Select(feature => (Enum)feature),
            .. Enum.GetValues<MenusFeature>().Select(feature => (Enum)feature),
            SpokenDetail.Positions,
            .. Enum.GetValues<UpdatesFeature>().Select(feature => (Enum)feature),
        ];

        Assert.Equal(declared.Length, Features.Features.Count);
        Assert.All(declared, feature => Assert.Contains(feature, Features.Features));
    }

    // Passed by ear in the 2026-11-22 session, so they ship on (#622, #193, #733, #254, #730).
    [Fact]
    public void AFeatureThatPassedItsByEarCheckIsOnByDefault()
    {
        Enum[] passed =
        [
            MenusFeature.SaveSlots, MenusFeature.Calendar, MenusFeature.SystemMenu, BattleFeature.BattleOutcomes,
            DialogueFeature.ChoiceCount,
        ];

        Assert.All(passed, feature =>
        {
            Assert.False(SettingKey.Of(feature).Testing);
            Assert.True(ModSettings.Defaults.IsOn(feature));
        });
    }

    // Passed by ear in the 2026-11-23 session, so they ship on (#770, #772, #767, #736).
    [Fact]
    public void AFeatureThatPassedTheSecondByEarCheckIsOnByDefault()
    {
        Enum[] passed =
        [
            MenusFeature.LearnedSettingValues, FieldFeature.StunCallouts, BattleFeature.BattleEnemyKeys,
            MenusFeature.ShopMenu, DialogueFeature.WindowCloseStop, DialogueFeature.TermNames,
        ];

        Assert.All(passed, feature =>
        {
            Assert.False(SettingKey.Of(feature).Testing);
            Assert.True(ModSettings.Defaults.IsOn(feature));
        });
    }

    // Passed by ear in the 2026-11-23 evening session, so they ship on.
    [Fact]
    public void AFeatureThatPassedTheThirdByEarCheckIsOnByDefault()
    {
        Enum[] passed =
        [
            MenusFeature.ReturnToTitleQuestion, DialogueFeature.ChoiceDelay, DialogueFeature.ButtonNames,
            MenusFeature.NameReadBack, FieldFeature.CameraTilt, MenusFeature.BattleOrderMenu,
            MenusFeature.BattleListViews, MenusFeature.ItemMenu, MenusFeature.QuestList, MenusFeature.FollowerList,
            BattleFeature.BattleStart, BattleFeature.BattleBanners,
        ];

        Assert.All(passed, feature =>
        {
            Assert.False(SettingKey.Of(feature).Testing);
            Assert.True(ModSettings.Defaults.IsOn(feature));
        });
    }

    // Passed by ear on 2026-11-24, so they ship on; battleAffinities ships untested on the maintainer's decision.
    [Fact]
    public void AFeatureThatPassedTheFourthByEarCheckIsOnByDefault()
    {
        Enum[] passed =
        [
            DialogueFeature.DialogueKeys, FieldFeature.CornerAdvice, FieldFeature.GuidancePause,
            FieldFeature.ShoulderHold, FieldFeature.ObjectiveGuidance, FieldFeature.WallTones,
            FieldFeature.FieldStrikes, MenusFeature.AutosaveNotice, MenusFeature.ArchetypeTree,
            MenusFeature.SettingsMenu, MenusFeature.KeyHelp, MenusFeature.SettingsHelp, MenusFeature.BattleItemTabs,
            BattleFeature.BattleAffinities, BattleFeature.BattleBannerNames,
        ];

        Assert.All(passed, feature =>
        {
            Assert.False(SettingKey.Of(feature).Testing);
            Assert.True(ModSettings.Defaults.IsOn(feature));
        });
    }

    [Fact]
    public void AFeatureIsOffInANormalInstallExactlyWhenItsKeyMarksItTesting()
    {
        Assert.All(Features.Features, feature =>
            Assert.Equal(!SettingKey.Of(feature).Testing, ModSettings.Defaults.IsOn(feature)));
        Assert.All(Enum.GetValues<Cue>(), cue => Assert.Equal(new CueSetting(true, 100), ModSettings.Defaults.Sound(cue)));
    }

    public static TheoryData<string, Enum> OneOff => new()
    {
        { """{ "speech": { "dialogue": false } }""", DialogueFeature.Dialogue },
        { """{ "speech": { "menus": false } }""", MenusFeature.Menus },
        { """{ "speech": { "nameEntry": false } }""", MenusFeature.NameEntry },
        { """{ "speech": { "travelMap": false } }""", MenusFeature.TravelMap },
        { """{ "speech": { "placeNames": false } }""", MenusFeature.PlaceNames },
        { """{ "speech": { "updateNotice": false } }""", UpdatesFeature.UpdateNotice },
        { """{ "field": { "nearby": false } }""", FieldFeature.Nearby },
        { """{ "field": { "whereAmI": false } }""", FieldFeature.WhereAmI },
    };

    [Theory]
    [MemberData(nameof(OneOff))]
    public void AFeatureTurnsOffAloneFromItsKey(string json, Enum off)
    {
        var settings = ModSettings.Parse(json, Features, logged.Add);

        Assert.All(Features.Features.Where(feature => !SettingKey.Of(feature).Testing),
            feature => Assert.Equal(!feature.Equals(off), settings.IsOn(feature)));
        Assert.Empty(logged);
    }
}
