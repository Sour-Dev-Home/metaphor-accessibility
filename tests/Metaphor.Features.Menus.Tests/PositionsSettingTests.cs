using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The "of total" after a menu item's number is a setting (#664): on, "Menu, Equipment, 3 of 9" as before; off,
/// the item alone, and the number alone when it has no name. Hook events in, announcements out.</summary>
[Trait("Category", "Unit")]
public sealed class PositionsSettingTests
{
    private const string MainMenuClass = "RootMenuChoice@ope@ui@app";
    private const long MainMenu = 0xb0;
    private const int Settle = 0x1;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();
    private bool say = true;

    public PositionsSettingTests()
    {
        var screens = new MenuScreens(KnownScreens.All, () => say);
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        screens.Observe(new MainMenuFocus(announcer));
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(int top, string screenClass = MainMenuClass) =>
        cursors.Handled(new CursorInput(MainMenu, ClassRva: 0x100, Settle, Result: 1, top, Row: 0, Flags: 0x46, screenClass));

    [Fact]
    public void ByDefaultTheListSaysItsPosition()
    {
        Cursor(top: 2);

        Assert.Equal([new Announcement("Menu, Equipment, 3 of 9", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void TurnedOffTheListSaysTheItemAlone()
    {
        say = false;

        Cursor(top: 2);

        Assert.Equal([new Announcement("Menu, Equipment", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void TurnedOffAScreenWithNoNamesStillSaysItsNumber()
    {
        say = false;

        Cursor(top: 4, screenClass: "SomethingNew@ope@ui@app");

        Assert.Equal([new Announcement("5", Interrupt: true)], announcer.Said);
    }
}
