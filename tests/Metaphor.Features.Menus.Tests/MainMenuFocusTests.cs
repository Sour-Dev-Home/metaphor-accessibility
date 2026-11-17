using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The in-game main menu is recognised by its screen class and its focus spoken (#97): its name with the
/// focused entry's position when the screen is entered, then each position the cursor settles on, each cutting off the
/// last. Hook events in (cursor inputs carrying a screen class), announcements out through the recording announcer.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MainMenuFocusTests
{
    private const string MainMenuClass = "RootMenuChoice@ope@ui@app";
    private const long MainMenu = 0xb0;
    private const int Settle = 0x1;
    private const int Right = 0x14;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();

    public MainMenuFocusTests()
    {
        var screens = new MenuScreens([TitleMenuFocus.Screen, MainMenuFocus.Screen]);
        cursors.Observe(screens);
        screens.Observe(new MainMenuFocus(announcer));
    }

    // The main menu's cursor moves `top`, not `row` (docs/raw-sources/main-menu-session-2026-11-17.md).
    private void Cursor(int command, int top, string? screenClass = MainMenuClass, long manager = MainMenu) =>
        cursors.Handled(new CursorInput(manager, ClassRva: 0x100, command, Result: 1, top, Row: 0, Flags: 0x46, screenClass));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EnteringTheMainMenuSaysItsNameAndTheFocusedPosition()
    {
        Cursor(Settle, top: 2);

        Assert.Equal([new Announcement("Menu, 3 of 9", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void EachEntryTheCursorSettlesOnIsAnnouncedWithItsPositionCuttingOffTheLast()
    {
        Cursor(Settle, top: 0);

        Cursor(Right, top: 1);
        Cursor(Settle, top: 1);
        Cursor(Settle, top: 8);

        Assert.Equal(
            [new Announcement("2 of 9", Interrupt: true), new Announcement("9 of 9", Interrupt: true)],
            announcer.Said.Skip(1));
    }

    [Fact]
    public void ReturningFromAScreenItOpenedSaysItsNameAgain()
    {
        Cursor(Settle, top: 1);
        Cursor(Settle, top: 0, "ItemMenuChoice@ope@ui@app", manager: 0xc0);

        Cursor(Settle, top: 1, manager: 0xd0);

        Assert.Equal(["Menu, 2 of 9", "Menu, 2 of 9"], Said());
    }

    [Fact]
    public void APositionPastTheMenusEntriesIsNotAnnounced()
    {
        Cursor(Settle, top: 0);

        Cursor(Settle, top: 9);

        Assert.Equal(["Menu, 1 of 9"], Said());
    }

    [Fact]
    public void OtherScreensAreLeftToTheirOwnFeatures()
    {
        Cursor(Settle, top: 0, TitleMenuFocus.Screen.ScreenClass, manager: 0xa0);
        Cursor(Settle, top: 1, "ItemMenuChoice@ope@ui@app", manager: 0xc0);

        Assert.Empty(announcer.Said);
    }
}
