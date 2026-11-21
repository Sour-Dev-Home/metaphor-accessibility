using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The title menu is recognised by its screen class and its focus spoken (#94, #208): "Main menu" with the
/// focused item when the screen is entered, then each item the cursor settles on as "item, n of total", each cutting off
/// the last. Hook events in (cursor inputs carrying a screen class, message files compiled), announcements out through
/// the recording announcer; made-up item text only.</summary>
[Trait("Category", "Unit")]
public sealed class TitleMenuFocusTests
{
    private const string TitleFile = "EN/message/system/Title_System.msg";
    private const string TitleSource = "@Title_System_07\n{\nYes\nNo\n<WAIT>\n}\n\n@Title_System_08\n{\nBegin\nResume\n<COLOR 3>Options<COLOR -1>\n<WAIT>\n}";
    private const string TitleClass = "TitleChoice@ope@ui@app";
    private const string MainMenuClass = "RootMenuChoice@ope@ui@app";
    private const long Title = 0xa0;
    private const int Settle = 0x1;
    private const int Down = 0x16;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageEvents messages = new();
    private readonly CursorEvents cursors = new();

    public TitleMenuFocusTests()
    {
        var library = new MessageLibrary();
        var screens = new MenuScreens([TitleMenuFocus.Screen]);
        screens.MenusRead(open: true);
        messages.ObserveFiles(library);
        cursors.Observe(screens);
        screens.Observe(new TitleMenuFocus(library, announcer, new TextCleaner(TextCleanerRules.None)));
    }

    // TitleChoice's mode field: 1 on the title menu, 2 in its System submenu (#210).
    private const int TitleMode = 1;
    private const int SystemMode = 2;

    private void Cursor(int command, int row, string? screenClass = TitleClass, long manager = Title, int? mode = null) =>
        cursors.Handled(new CursorInput(manager, ClassRva: 0x100, command, Result: 1, Top: 0, Row: row, Flags: 0x46, screenClass,
            Count: 3, Mode: mode));

    private void TitleFileCompiled(string path = TitleFile) => messages.Compiled(new MessageFileCompiled(path, 56, TitleSource));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EnteringTheTitleMenuSaysItsNameAndTheFocusedItem()
    {
        TitleFileCompiled();

        Cursor(Settle, row: 0);

        Assert.Equal([new Announcement("Main menu, Begin, 1 of 3", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void EachItemTheCursorSettlesOnIsAnnouncedWithItsPositionCuttingOffTheLast()
    {
        TitleFileCompiled();
        Cursor(Settle, row: 0);

        Cursor(Settle, row: 1);
        Cursor(Settle, row: 2);
        Cursor(Settle, row: 0);

        Assert.Equal(
            [
                new Announcement("Resume, 2 of 3", Interrupt: true),
                new Announcement("System, 3 of 3", Interrupt: true),
                new Announcement("Begin, 1 of 3", Interrupt: true),
            ],
            announcer.Said.Skip(1));
    }

    [Fact]
    public void AMoveIsAnnouncedOnlyWhereTheCursorSettles()
    {
        TitleFileCompiled();
        Cursor(Settle, row: 0);

        Cursor(Down, row: 1);
        Cursor(Settle, row: 0);
        Cursor(Down, row: 1);
        Cursor(Settle, row: 1);

        Assert.Equal(["Main menu, Begin, 1 of 3", "Resume, 2 of 3"], Said());
    }

    [Fact]
    public void ACursorThatStaysOnItsItemIsNotAnnouncedAgain()
    {
        TitleFileCompiled();
        Cursor(Settle, row: 1);

        cursors.Handled(new CursorInput(Title, 0x100, Settle, 1, 0, 1, Flags: 0x66, TitleClass));

        Assert.Equal(["Main menu, Resume, 2 of 3"], Said());
    }

    [Fact]
    public void TheTitleFileCompilingDuringPlayDoesNotMakeAnotherScreenTheTitleMenu()
    {
        TitleFileCompiled();
        Cursor(Settle, row: 0, MainMenuClass, manager: 0xb0);
        TitleFileCompiled();

        Cursor(Settle, row: 1, MainMenuClass, manager: 0xb0);
        Cursor(Settle, row: 2, "GlossaryListChoice@ope@ui@app", manager: 0xc0);

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ACursorWithoutAScreenClassIsNotAnnounced()
    {
        TitleFileCompiled();

        Cursor(Settle, row: 0, screenClass: null);

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ReturningToTheTitleMenuFromAnotherScreenSaysItsNameAgain()
    {
        TitleFileCompiled();
        Cursor(Settle, row: 2);
        Cursor(Settle, row: 0, "?$ConfigTopChoicePc@VConfigTopChoicePcLayoutInit@ui@app@@@ope@ui@app", manager: 0xb0);

        Cursor(Settle, row: 1);

        Assert.Equal(["Main menu, System, 3 of 3", "Main menu, Resume, 2 of 3"], Said());
    }

    [Fact]
    public void ATitleMenuCursorMadeForANewVisitIsTheSameScreen()
    {
        TitleFileCompiled();
        Cursor(Settle, row: 0);

        Cursor(Settle, row: 1, manager: 0xd0);

        Assert.Equal(["Main menu, Begin, 1 of 3", "Resume, 2 of 3"], Said());
    }

    [Fact]
    public void APositionPastTheMenusItemsIsNotAnnounced()
    {
        TitleFileCompiled();
        Cursor(Settle, row: 0);

        Cursor(Settle, row: 3);

        Assert.Equal(["Main menu, Begin, 1 of 3"], Said());
    }

    [Fact]
    public void AnItemWhoseTextIsMissingIsAnnouncedByItsPosition()
    {
        Cursor(Settle, row: 1);

        Assert.Equal(["Main menu, 2 of 3"], Said());
    }

    [Fact]
    public void TheItemsAreFoundWhicheverLanguageFolderTheTitleFileIsIn()
    {
        TitleFileCompiled(@"JP\message\system\title_system.msg");

        Cursor(Settle, row: 1);

        Assert.Equal(["Main menu, Resume, 2 of 3"], Said());
    }

    // The game draws the title items from images, and its third shows "System" while the message's third line says
    // Settings (#537, docs/raw-sources/title-menu-item-images-2026-11-20.md).
    [Fact]
    public void TheThirdItemIsSpokenAsSystemTheWordItsImageShowsNotTheMessagesLine()
    {
        TitleFileCompiled();

        Cursor(Settle, row: 2);

        Assert.Equal(["Main menu, System, 3 of 3"], Said());
    }

    [Fact]
    public void TheThirdItemIsSpokenAsSystemEvenWithoutTheMessage()
    {
        Cursor(Settle, row: 2);

        Assert.Equal(["Main menu, System, 3 of 3"], Said());
    }

    // #210: the System submenu is the same choice and cursor (count 3 too), told apart by the choice's mode field. Its
    // items are images, so their words are ours (docs/raw-sources/title-menu-item-images-2026-11-20.md).
    [Fact]
    public void TheSystemSubmenuIsSpokenAsItsOwnTwoItems()
    {
        TitleFileCompiled();
        Cursor(Settle, row: 2, mode: TitleMode);

        Cursor(Settle, row: 0, mode: SystemMode);
        Cursor(Down, row: 1, mode: SystemMode);
        Cursor(Settle, row: 1, mode: SystemMode);
        Cursor(Settle, row: 0, mode: SystemMode);

        Assert.Equal(["Main menu, System, 3 of 3", "Settings, 1 of 2", "Credits, 2 of 2", "Settings, 1 of 2"], Said());
    }

    [Fact]
    public void BackingOutOfTheSystemSubmenuSpeaksTheTitlesThirdItem()
    {
        TitleFileCompiled();
        Cursor(Settle, row: 2, mode: TitleMode);
        Cursor(Settle, row: 1, mode: SystemMode);

        Cursor(Settle, row: 2, mode: TitleMode);
        Cursor(Settle, row: 1, mode: TitleMode);

        Assert.Equal(["Main menu, System, 3 of 3", "Credits, 2 of 2", "System, 3 of 3", "Resume, 2 of 3"], Said());
    }

    // #557, as in the by-ear check of 2026-11-20 (game build 548ddc95…): Settings opened from the submenu, then backed
    // out of; the submenu's cursor comes back with the very input it last had, after Settings' screens.
    [Fact]
    public void BackingOutOfSettingsToTheSystemSubmenuSpeaksItsFocusAsOnEntry()
    {
        var now = 0L;
        var timed = new CursorEvents(() => now);
        var screens = new MenuScreens([TitleMenuFocus.Screen]);
        screens.MenusRead(open: true);
        timed.Observe(screens);
        screens.Observe(new TitleMenuFocus(new MessageLibrary(), announcer, new TextCleaner(TextCleanerRules.None)));
        void Timed(long manager, string screenClass, int row, int? mode) =>
            timed.Handled(new CursorInput(manager, 0x100, Settle, 1, 0, row, 0x46, screenClass, 3, mode));

        Timed(Title, TitleClass, row: 0, mode: SystemMode);
        now += 17;
        Timed(0xb0, "?$ConfigTopChoicePc@VConfigTopChoicePcLayoutInit@ui@app@@@ope@ui@app", row: 0, mode: null);
        now += 7000;
        Timed(Title, TitleClass, row: 0, mode: SystemMode);

        Assert.Equal(["Settings, 1 of 2", "Settings, 1 of 2"], Said());
    }

    [Fact]
    public void TheSystemSubmenuEnteredAfreshSaysOnlyItsFocus()
    {
        Cursor(Settle, row: 1, mode: SystemMode);

        Assert.Equal(["Credits, 2 of 2"], Said());
    }

    [Fact]
    public void APositionPastTheSystemSubmenusItemsIsNotAnnounced()
    {
        Cursor(Settle, row: 0, mode: SystemMode);

        Cursor(Settle, row: 2, mode: SystemMode);

        Assert.Equal(["Settings, 1 of 2"], Said());
    }

    // #555: before a button is pressed the choice's mode is 0 and "Press any button" shows, no menu; the decide handler
    // then sets it to 1 and shows the items, and back on the title menu sets it to 0 again
    // (docs/raw-sources/title-press-any-button-mode-2026-11-20.md).
    private const int PressAnyButtonMode = 0;
    private const string PromptSource = "@Title_PressAnyKey\n{\n<COLOR 3>Tap to start<COLOR -1>\n<WAIT>\n}";

    private void TitleFileWithPromptCompiled() =>
        messages.Compiled(new MessageFileCompiled(TitleFile, 56, TitleSource + "\n\n" + PromptSource));

    [Fact]
    public void ThePressAnyButtonScreenSpeaksTheGamesPromptNotTheMenu()
    {
        TitleFileWithPromptCompiled();

        Cursor(Settle, row: 0, mode: PressAnyButtonMode);

        Assert.Equal([new Announcement("Tap to start", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void ThePressAnyButtonScreenSpeaksOurWordsWithoutThePromptMessage()
    {
        TitleFileCompiled();

        Cursor(Settle, row: 0, mode: PressAnyButtonMode);

        Assert.Equal(["Press any button"], Said());
    }

    [Theory]
    [InlineData(0, "Main menu, Begin, 1 of 3")]
    [InlineData(1, "Main menu, Resume, 2 of 3")]
    public void TheOptionsAppearingSayTheMenusNameAndTheFocusedItem(int row, string expected)
    {
        TitleFileWithPromptCompiled();
        Cursor(Settle, row: 0, mode: PressAnyButtonMode);

        Cursor(Settle, row: row, mode: TitleMode);
        Cursor(Settle, row: 2, mode: TitleMode);

        Assert.Equal(["Tap to start", expected, "System, 3 of 3"], Said());
    }

    [Fact]
    public void BackingOutOfTheTitleMenuSpeaksThePromptAgainThenTheMenuOnceItReturns()
    {
        TitleFileWithPromptCompiled();
        Cursor(Settle, row: 1, mode: TitleMode);

        Cursor(Settle, row: 1, mode: PressAnyButtonMode);
        Cursor(Settle, row: 0, mode: PressAnyButtonMode);
        Cursor(Settle, row: 1, mode: TitleMode);

        Assert.Equal(["Main menu, Resume, 2 of 3", "Tap to start", "Main menu, Resume, 2 of 3"], Said());
    }
}
