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
        "field.cameraTilt | Camera tilt | on | done",
        "field.enemyAwareness | Enemy awareness | off | testing",
        "field.guidance | Guidance beep | on | done",
        "field.guidancePause | Guidance pause | off | testing",
        "field.nearby | Nearby list | on | done",
        "field.nearbyCategories | Nearby categories | on | done",
        "field.reach | Reach callouts | on | done",
        "field.routePings | Route pings | off | testing",
        "field.shoulderHold | Shoulder hold or tap | off | testing",
        "field.sonar | Sonar | off | testing",
        "field.soundFiles | Navigation sound files | off | testing",
        "field.stunCallouts | Stun callouts | on | done",
        "field.wallTones | Wall tones | off | testing",
        "field.whereAmI | Where am I | on | done",
        "report.markKey | Mark key | on | done",
        "report.silentScreens | Silent screen list | on | done",
        "sounds.allyHit | Ally hit sound | on | done",
        "sounds.enemyGaveUp | Enemy gave up sound | on | done",
        "sounds.enemyHit | Enemy hit sound | on | done",
        "sounds.enemyNoticed | Enemy noticed sound | on | done",
        "sounds.mark | Mark sound | on | done",
        "speech.archetypeTree | Archetype Tree | off | testing",
        "speech.battleBanners | Battle banners | on | done",
        "speech.battleConfirms | Battle confirm prompts | off | testing",
        "speech.battleEnd | Battle end | off | testing",
        "speech.battleEnemyKeys | Battle enemy keys | on | done",
        "speech.battleEnemyList | Battle enemy list | off | testing",
        "speech.battleEnemyMaxHp | Battle enemy max HP | on | done",
        "speech.battleEnemyPad | Battle enemy pad chords | off | testing",
        "speech.battleEnemyTarget | Battle targeted enemy first | off | testing",
        "speech.battleHitNumbers | Battle hit numbers only | off | testing",
        "speech.battleHits | Battle hits | on | done",
        "speech.battleHpMp | Battle HP and MP key | off | testing",
        "speech.battleItemTabs | Battle item tabs | off | testing",
        "speech.battleListViews | Battle list views | on | done",
        "speech.battleLists | Battle lists | on | done",
        "speech.battleNumberReset | Battle number reset | off | testing",
        "speech.battleOrderMenu | Battle order menu | on | done",
        "speech.battleOutcomes | Battle outcomes | on | done",
        "speech.battleStart | Battle start | on | done",
        "speech.battleTargetSpeech | Battle target cursor | off | testing",
        "speech.battleTurnLine | Battle turn line | off | testing",
        "speech.buttonNames | Button names | on | done",
        "speech.calendar | Calendar | on | done",
        "speech.choiceCount | Choice count | on | done",
        "speech.choiceDelay | Choice delay | on | done",
        "speech.dialogue | Dialogue | on | done",
        "speech.equipmentMenu | Equipment menu | off | testing",
        "speech.followerList | Follower list | on | done",
        "speech.itemMenu | Item menu | on | done",
        "speech.keyHelp | Key help | off | testing",
        "speech.learnedSettingValues | Learned setting values | on | done",
        "speech.levelUpPoints | Level-up points | off | testing",
        "speech.menus | Menus | on | done",
        "speech.moreTermNames | More term names | off | testing",
        "speech.nameEntry | Name entry | on | done",
        "speech.nameReadBack | Name read-back | on | done",
        "speech.nameStandIn | Name stand-in | off | testing",
        "speech.placeNames | Place names | on | done",
        "speech.positions | Positions | on | done",
        "speech.questList | Quest list | on | done",
        "speech.returnToTitleQuestion | Return to title question | on | done",
        "speech.saveSlots | Save slots | on | done",
        "speech.settingsMenu | Settings menu | off | testing",
        "speech.shopMenu | Shop menu | on | done",
        "speech.skipSceneQuestion | Skip scene question | off | testing",
        "speech.systemMenu | System menu | on | done",
        "speech.termNames | Term names | on | done",
        "speech.travelMap | Travel map | on | done",
        "speech.tutorials | Tutorials | on | done",
        "speech.updateNotice | Update notice | on | done",
        "speech.welcome | Welcome | on | done",
        "speech.windowCloseStop | Window close stop | on | done",
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
