using Metaphor.Core.Cues;
using Metaphor.Core.Settings;
using Metaphor.SettingsReference;

namespace Metaphor.Settings.Tests;

/// <summary>Every setting as it stood before #784 moved them into their feature projects: key, spoken label, default
/// with no file, and whether it is still being tested. Taken from the old central enum before the move; the move must
/// leave every line as it is, and a later change to one is a change a reviewer sees here.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsSnapshotTests
{
    private static readonly string[] Expected =
    [
        "field.guidance | Guidance beep | on | done",
        "field.guidancePause | Guidance pause | off | testing",
        "field.nearby | Nearby list | on | done",
        "field.nearbyCategories | Nearby categories | on | done",
        "field.reach | Reach callouts | on | done",
        "field.stunCallouts | Stun callouts | off | testing",
        "field.whereAmI | Where am I | on | done",
        "report.markKey | Mark key | on | done",
        "report.silentScreens | Silent screen list | on | done",
        "sounds.allyHit | Ally hit sound | on | done",
        "sounds.enemyHit | Enemy hit sound | on | done",
        "sounds.mark | Mark sound | on | done",
        "speech.battleBanners | Battle banners | off | testing",
        "speech.battleHits | Battle hits | on | done",
        "speech.battleListViews | Battle list views | off | testing",
        "speech.battleLists | Battle lists | on | done",
        "speech.battleNumberReset | Battle number reset | off | testing",
        "speech.battleOutcomes | Battle outcomes | on | done",
        "speech.calendar | Calendar | on | done",
        "speech.choiceCount | Choice count | on | done",
        "speech.choiceDelay | Choice delay | off | testing",
        "speech.dialogue | Dialogue | on | done",
        "speech.equipmentMenu | Equipment menu | off | testing",
        "speech.itemMenu | Item menu | off | testing",
        "speech.learnedSettingValues | Learned setting values | off | testing",
        "speech.menus | Menus | on | done",
        "speech.nameEntry | Name entry | on | done",
        "speech.placeNames | Place names | on | done",
        "speech.saveSlots | Save slots | on | done",
        "speech.shopMenu | Shop menu | off | testing",
        "speech.systemMenu | System menu | on | done",
        "speech.termNames | Term names | off | testing",
        "speech.travelMap | Travel map | on | done",
        "speech.tutorials | Tutorials | on | done",
        "speech.updateNotice | Update notice | on | done",
        "speech.welcome | Welcome | on | done",
        "speech.windowCloseStop | Window close stop | off | testing",
    ];

    [Fact]
    public void EverySettingKeepsItsKeyLabelDefaultAndTestingMark()
    {
        var defaults = ModSettings.Defaults;
        var features = AllFeatures.Catalogue().Features.Select(feature => Line(
            SettingKey.Of(feature), defaults.IsOn(feature)));
        var cues = Enum.GetValues<Cue>().Select(cue => Line(SettingKey.Of(cue), defaults.Sound(cue).On));

        Assert.Equal(Expected, features.Concat(cues).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ASessionThatTurnsTestingOnTurnsEveryFeatureOn() =>
        Assert.All(AllFeatures.Catalogue().Features,
            feature => Assert.True(ModSettings.Defaults.WithTesting(true).IsOn(feature)));

    private static string Line(SettingKey key, bool on) =>
        $"{key.Path} | {key.Spoken} | {(on ? "on" : "off")} | {(key.Testing ? "testing" : "done")}";
}
