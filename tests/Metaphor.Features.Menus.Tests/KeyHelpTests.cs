using Metaphor.Core.Announcements;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The help key (#868): F1, or the chord with Y, says the mod's keys for where the player is now, from the
/// keys the modules listed, as bound now, only for features that are on.</summary>
[Trait("Category", "Unit")]
public sealed class KeyHelpTests
{
    private const int F1 = 0x70;
    private const int W = 0x57;
    private const int LT = 0x0100;
    private const int RT = 0x0200;
    private const int Y = 0x1000;

    private static readonly BoundKey Details = new("menus", "detailsKey", 0x46);
    private static readonly BoundKey Mark = new("report", "markKey", 0x77);

    private readonly RecordingAnnouncer said = new();
    private readonly List<KeyEntry> keys =
    [
        new(KeyPlaces.Menu, MenusFeature.Menus, new("Details", Details)),
        new(KeyPlaces.Field, MenusFeature.Menus, new("Nearby", new BoundKey("field", "nearbyKey", 0x4E))),
        new(KeyPlaces.Battle, MenusFeature.Menus, new("Status", new BoundKey("battle", "statusKey", 0x48))),
        new(KeyPlaces.Dialogue, MenusFeature.Menus, new("Repeat", new BoundKey("dialogue", "repeatKey", 0x52))),
        new(KeyPlaces.SettingsMenu, MenusFeature.SettingsMenu, new("Move") { Fixed = "Up and Down" }),
        new(KeyPlaces.Anywhere | KeyPlaces.SettingsMenu, MenusFeature.MarkKey, new("Mark", Mark)),
        new(KeyPlaces.Field, MenusFeature.Menus, new("Debug") { Fixed = "I", Hidden = true }),
    ];

    private ModSettings settings = ModSettings.Defaults.WithTesting(true);
    private KeyPlaces places = KeyPlaces.None;
    private KeyHelp? built;

    private KeyHelp Help => built ??= new KeyHelp(
        keys, () => settings, said, () => SettingsMenuBindings.Default, () => places);

    private string Last => said.Said[^1].Text;

    [Fact]
    public void InTheFieldItSaysTheFieldsKeysThenThoseThatWorkAnywhere()
    {
        Assert.Empty(Help.Keys([F1]));

        Assert.Equal("Keys in the field. Nearby: N. Mark: F8.", Last);
    }

    [Fact]
    public void InAMenuItSaysTheMenusKeys()
    {
        places = KeyPlaces.Menu;

        Help.Keys([F1]);

        Assert.Equal("Keys in menus. Details: F. Mark: F8.", Last);
    }

    [Fact]
    public void InABattleItSaysTheBattlesKeys()
    {
        places = KeyPlaces.Battle;

        Help.Keys([F1]);

        Assert.Equal("Keys in battle. Status: H. Mark: F8.", Last);
    }

    [Fact]
    public void WithALineUpItSaysTheDialoguesKeys()
    {
        Help.WindowRead(0x1234);

        Help.Keys([F1]);

        Assert.Equal("Keys in dialogue. Repeat: R. Mark: F8.", Last);
    }

    [Fact]
    public void InTheSettingsMenuItSaysOnlyItsKeys()
    {
        places = KeyPlaces.SettingsMenu;

        Help.Keys([F1]);

        Assert.Equal("Keys in the settings menu. Move: Up and Down. Mark: F8.", Last);
    }

    [Fact]
    public void AKeyWhoseFeatureIsOffIsNotSaid()
    {
        settings = ModSettings.Parse("""{ "report": { "markKey": false } }""", SettingCatalogue.Of<MenusFeature>(), _ => { });

        Help.Keys([F1]);

        Assert.Equal("Keys in the field. Nearby: N.", Last);
    }

    [Fact]
    public void ItFollowsARebinding()
    {
        settings = ModSettings.Parse("""{ "keys": { "field": { "nearbyKey": "Q" } } }""", SettingCatalogue.Empty, _ => { });

        Help.Keys([F1]);

        Assert.Equal("Keys in the field. Nearby: Q. Mark: F8.", Last);
    }

    [Fact]
    public void F1IsKeptFromTheGameAndSpeaksOncePerPress()
    {
        Assert.Equal([W], Help.Keys([F1, W]));
        Assert.Empty(Help.Keys([F1]));
        Assert.Single(said.Said);
        Assert.Equal([W], Help.Keys([W]));
    }

    [Fact]
    public void TheChordWithYSpeaksAndKeepsY()
    {
        Assert.Equal(Y, Help.Pad(0, Y, made: true));
        Assert.Empty(said.Said);
        Help.Pad(0, 0, made: true);

        Assert.Equal(LT | RT, Help.Pad(0, LT | RT | Y, made: true));
        Assert.Single(said.Said);
    }
}
