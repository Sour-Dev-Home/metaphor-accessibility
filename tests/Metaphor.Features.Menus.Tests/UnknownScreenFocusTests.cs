using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus.Tests;

/// <summary>A screen the mod doesn't know yet still speaks its focus's position (#97, spec #211), so the player is never
/// in silence; screens with their own speech are left to it. Hook events in (cursor inputs carrying a screen class),
/// announcements out through the recording announcer.</summary>
[Trait("Category", "Unit")]
public sealed class UnknownScreenFocusTests
{
    private const string CalendarClass = "CalendarChoice@ope@ui@app";
    private const long Calendar = 0xc0;
    private const int Settle = 0x1;
    private const int Down = 0x16;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();

    public UnknownScreenFocusTests()
    {
        var screens = new MenuScreens(KnownScreens.All);
        screens.MenusRead(open: true);
        cursors.Observe(screens);
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private void Cursor(int command, int row, string? screenClass = CalendarClass, long manager = Calendar, int top = 0) =>
        cursors.Handled(new CursorInput(manager, ClassRva: 0x100, command, Result: 1, top, row, Flags: 0x46, screenClass));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EachPositionTheCursorSettlesOnInAnUnknownScreenIsAnnouncedCuttingOffTheLast()
    {
        Cursor(Settle, row: 0);
        Cursor(Down, row: 1);
        Cursor(Settle, row: 1);
        Cursor(Settle, row: 3, top: 10);

        Assert.Equal(
            [
                new Announcement("1", Interrupt: true),
                new Announcement("2", Interrupt: true),
                new Announcement("14", Interrupt: true),
            ],
            announcer.Said);
    }

    [Fact]
    public void AScreenWhoseClassIsUnreadableIsAnnouncedToo()
    {
        Cursor(Settle, row: 4, screenClass: "?");

        Assert.Equal(["5"], Said());
    }

    [Fact]
    public void KnownScreensAreLeftToTheirOwnFeatures()
    {
        Cursor(Settle, row: 1, TitleMenuFocus.Screen.ScreenClass, manager: 0xa0);
        Cursor(Settle, row: 0, MainMenuFocus.Screen.ScreenClass, manager: 0xb0, top: 2);

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void DialogueChoicesAreLeftToTheirOwnSpeech()
    {
        Cursor(Settle, row: 0, "CommonTalkChoice@ope@ui@app", manager: 0xd0);
        Cursor(Settle, row: 1, "CommonTalkChoice@ope@ui@app", manager: 0xd0);

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ACursorWithoutAScreenClassIsNotAnnounced()
    {
        Cursor(Settle, row: 0, screenClass: null);

        Assert.Empty(announcer.Said);
    }
}
