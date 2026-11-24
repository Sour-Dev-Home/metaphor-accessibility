using Metaphor.Core.Announcements;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The mod's spoken settings menu (#754, the maintainer's decisions of 2026-11-23): F2 or the chord opens it,
/// focus says the row, Left and Right change and save at once, F reads more, Escape or B closes, and while it is open
/// the game gets nothing.</summary>
[Trait("Category", "Unit")]
public sealed class SettingsMenuTests
{
    private const int F2 = 0x71;
    private const int F = 0x46;
    private const int Escape = 0x1B;
    private const int Up = 0x26;
    private const int Down = 0x28;
    private const int Right = 0x27;
    private const int Left = 0x25;
    private const int Shift = 0xA0;
    private const int PageDown = 0x22;
    private const int W = 0x57;
    private const int LT = 0x0100;
    private const int RT = 0x0200;
    private const int View = 0x0001;
    private const int PadB = 0x2000;
    private const int PadDown = 0x0040;
    private const int PadA = 0x4000;
    private const int Enter = 0x0D;

    private readonly RecordingAnnouncer said = new();
    private readonly SettingsReloader settings =
        new(new SilentAnnouncer(), SettingCatalogue.Of<MenusFeature>(), _ => { });
    private readonly TextStore store = new();
    private bool positions = true;
    private SettingsMenu? built;

    private SettingsMenu Menu => built ??= new SettingsMenu(
        () => [
            .. PlayerSettings.GeneralSettings,
            new ToggleSetting("Speech", "Menus", "The `title` menu.", ["speech", "menus"],
                now => now.IsOn(MenusFeature.Menus)),
            new ToggleSetting("Speech", "Equipment menu", "Gear.", ["speech", "equipmentMenu"],
                now => now.IsOn(MenusFeature.EquipmentMenu)) { Testing = true },
            new KeySetting(PlayerSettings.Keys, "Repeat the last line", _ => "R"),
        ],
        () => settings.Current, new SettingsEditor(store, settings).Set, said,
        () => SettingsMenuBindings.Default, () => MenuBindings.Default, () => true, () => positions)
    {
        SayIntro = () => sayIntro,
    };

    private bool sayIntro;

    private string Last => said.Said[^1].Text;

    // Each key pressed and let go, as the game reads it.
    private IReadOnlyList<int> Press(params int[] keys)
    {
        var handed = Menu.Keys(keys);
        Menu.Keys([]);
        return handed;
    }

    [Fact]
    public void F2OpensItAndSaysItsNameTheSectionAndTheRow()
    {
        Assert.Empty(Menu.Keys([F2]));

        Assert.True(Menu.IsOpen);
        Assert.Equal("Mod settings. General. Voiced lines, auto, 1 of 4", Last);
        Assert.True(said.Said[^1].Interrupt);
        Assert.True(Menu.BlocksKeys);
        Assert.True(Menu.BlocksPad);
    }

    [Fact]
    public void ClosedItHandsEveryReadOnAndBlocksNothing()
    {
        Assert.Equal([W], Menu.Keys([W]));
        Assert.Equal(PadDown, Menu.Pad(0, PadDown, made: true));
        Assert.False(Menu.BlocksKeys);
        Assert.False(Menu.BlocksPad);
        Assert.Empty(said.Said);
    }

    [Fact]
    public void WithPositionsOffARowIsItsNameAndValue()
    {
        positions = false;

        Press(F2);

        Assert.Equal("Mod settings. General. Voiced lines, auto", Last);
    }

    [Fact]
    public void UpAndDownMoveAndASectionIsNamedWhenCrossed()
    {
        Press(F2);

        Press(Down);
        Assert.Equal("Update check, on, 2 of 4", Last);
        Press(Down);
        Press(Down);
        Press(Down);
        Assert.Equal("Speech. Menus, on, 1 of 2", Last);
        Press(Down);
        Assert.Equal("Equipment menu, testing, off, 2 of 2", Last);
        Press(Up);
        Assert.Equal("Menus, on, 1 of 2", Last);
    }

    [Fact]
    public void PageDownJumpsToTheNextSection()
    {
        Press(F2);

        Press(PageDown);
        Assert.Equal("Speech. Menus, on, 1 of 2", Last);
        Press(PageDown);
        Assert.Equal("Keys. Repeat the last line, R, 1 of 1", Last);
    }

    [Fact]
    public void LeftTurnsAToggleOffSavesItAndSaysSo()
    {
        Press(F2);
        Press(PageDown);

        Press(Left);

        Assert.Equal("off, saved.", Last);
        Assert.False(settings.Current.IsOn(MenusFeature.Menus));
        Assert.Equal("{ \"speech\": { \"menus\": false } }" + Environment.NewLine, store.Text);
    }

    // #959: Off on the left, On on the right; at its end a toggle says its value and saves nothing.
    [Fact]
    public void RightOnAToggleAlreadyOnSaysOnAndSavesNothing()
    {
        Press(F2);
        Press(PageDown);

        Press(Right);

        Assert.Equal("on", Last);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void EnterOrATurnsAToggleOver()
    {
        Press(F2);
        Press(PageDown);

        Press(Enter);
        Assert.Equal("off, saved.", Last);
        Menu.Pad(0, PadA, made: true);
        Menu.Pad(0, 0, made: true);
        Assert.Equal("on, saved.", Last);
        Assert.True(settings.Current.IsOn(MenusFeature.Menus));
    }

    [Fact]
    public void AChoiceStepsAndStopsAtItsEnds()
    {
        Press(F2);

        Press(Left);
        Assert.Equal("auto", Last);
        Press(Right);
        Assert.Equal("name only, saved.", Last);
        Press(Right);
        Press(Right);
        Assert.Equal("nothing, saved.", Last);
        Press(Right);
        Assert.Equal("nothing", Last);
        Assert.Equal(VoicedLinesMode.Nothing, settings.Current.VoicedLines);
    }

    // #959: Escape, or B, goes back out of the menu from every row of every section.
    [Fact]
    public void EscapeOrBClosesItFromEveryRow()
    {
        for (var row = 0; row < 7; row++)
        {
            Press(F2);
            for (var down = 0; down < row; down++)
            {
                Press(Down);
            }
            Press(Escape);
            Assert.False(Menu.IsOpen);
            Assert.Equal("Settings closed.", Last);

            Press(F2);
            Menu.Pad(0, PadB, made: true);
            Menu.Pad(0, 0, made: true);
            Assert.False(Menu.IsOpen);
        }
    }

    // #927: the layout line, while its setting is on.
    [Fact]
    public void WithTheLayoutOnOpeningSaysTheSectionsAndTheirKeys()
    {
        sayIntro = true;

        Press(F2);

        Assert.Equal(
            "Mod settings. 3 sections: General, Speech, Keys. Page Down and Page Up, or RB and LB, move between " +
            "them. Up and Down move, Left is off or less, Right is on or more, Escape or B closes. General. Voiced " +
            "lines, auto, 1 of 4",
            Last);
    }

    [Fact]
    public void ANumberMovesByItsStepAndByFiveStepsWithShift()
    {
        Press(F2);
        Press(Down);
        Press(Down);
        Press(Down);

        Press(Right);
        Assert.Equal("55, saved.", Last);
        Press(Shift, Left);
        Assert.Equal("30, saved.", Last);
        Assert.Equal(30, settings.Current.SoundCueVolume);
    }

    [Fact]
    public void AtItsEndANumberSaysItsValueAndSavesNothing()
    {
        store.Text = """{ "soundCueVolume": 100 }""";
        settings.Load(store.Text);
        Press(F2);
        // Up from the first row wraps to the last, the key list, then climbs back into the general settings.
        Press(Up);
        Press(Up);
        Press(Up);
        Press(Up);
        Assert.Equal("General. Sound cue volume, 100, 4 of 4", Last);

        Press(Right);

        Assert.Equal("100", Last);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void ReadMoreSaysWhatTheRowDoesAndThatATestingOneIsBeingTested()
    {
        Press(F2);
        Press(PageDown);

        Press(F);
        Assert.Equal("The title menu.", Last);
        Press(Down);
        Press(F);
        Assert.Equal("Gear. Still being tested.", Last);
    }

    [Fact]
    public void AKeyIsReadOnly()
    {
        Press(F2);
        Press(Up);

        Press(Right);

        Assert.Equal("To change a key, edit settings.json.", Last);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void AFailedSaveSaysSo()
    {
        store.Fails = true;
        Press(F2);

        Press(Right);

        Assert.Equal("Not saved.", Last);
        Assert.Equal(VoicedLinesMode.Auto, settings.Current.VoicedLines);
    }

    [Fact]
    public void WhileOpenNoKeyIsHandedOn()
    {
        Press(F2);

        Assert.Empty(Menu.Keys([W, Down]));
        Assert.Equal(0, Menu.Pad(0, PadDown, made: true));
    }

    // The Escape that closed it is kept from the game until it is let go, so the game never sees half a press.
    [Fact]
    public void EscapeClosesItAndStaysKeptUntilLetGo()
    {
        Press(F2);

        Assert.Empty(Menu.Keys([Escape]));
        Assert.Equal("Settings closed.", Last);
        Assert.False(Menu.IsOpen);
        Assert.True(Menu.BlocksKeys);
        Assert.Empty(Menu.Keys([Escape]));
        Assert.Equal([W], Menu.Keys([W]));
        Assert.False(Menu.BlocksKeys);
    }

    [Fact]
    public void TheChordOpensItAndBClosesIt()
    {
        Assert.Equal(LT | RT, Menu.Pad(0, LT | RT, made: true));
        Assert.Equal(0, Menu.Pad(0, LT | RT | View, made: true));
        Assert.True(Menu.IsOpen);
        Menu.Pad(0, 0, made: true);

        Assert.Equal(0, Menu.Pad(0, PadB, made: true));
        Assert.False(Menu.IsOpen);
        Assert.True(Menu.BlocksPad);
        Menu.Pad(0, 0, made: true);
        Assert.False(Menu.BlocksPad);
    }

    [Fact]
    public void ViewAloneOrWithOneTriggerDoesNotOpenIt()
    {
        Assert.Equal(View, Menu.Pad(0, View, made: true));
        Menu.Pad(0, 0, made: true);
        Assert.Equal(LT | View, Menu.Pad(0, LT | View, made: true));

        Assert.False(Menu.IsOpen);
    }

    // Opened while a dialogue line is up, the menu's first line queues behind it rather than cutting it off.
    [Fact]
    public void OpenedDuringALineItWaitsForTheLine()
    {
        Menu.WindowRead(0x1234);

        Press(F2);

        Assert.False(said.Said[^1].Interrupt);
        Press(Down);
        Assert.True(said.Said[^1].Interrupt);
    }

    private sealed class TextStore : ISettingsStore
    {
        public string? Text { get; set; }

        public bool Fails { get; set; }

        public int Writes { get; private set; }

        public string? Read() => Text;

        public void Write(string text)
        {
            if (Fails)
            {
                throw new IOException("denied");
            }
            Writes++;
            Text = text;
        }
    }
}
