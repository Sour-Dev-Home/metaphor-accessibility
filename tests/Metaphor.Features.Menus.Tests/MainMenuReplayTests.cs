using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Replay;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The main menu and the unknown-screen fallback (#97) against a recorded session (#149): replays a fixture
/// under <c>tests/fixtures/sessions/</c> through the cursor seam, as the hooks feed it, with the menu features wired as
/// the mod wires them, and checks what was announced, in what order.</summary>
[Trait("Category", "Unit")]
public sealed class MainMenuReplayTests
{
    private readonly RecordingAnnouncer announcer = new();

    private void Replay(string scenario)
    {
        var screens = new MenuScreens([TitleMenuFocus.Screen, MainMenuFocus.Screen]);
        var cursors = new CursorEvents();
        screens.Observe(new MainMenuFocus(announcer));
        screens.Observe(new UnknownScreenFocus(announcer));
        cursors.Observe(screens);
        var fixture = SessionFixture.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sessions", scenario)));
        fixture.Replay(new MessageEvents(), cursors);
    }

    [Fact]
    public void TheMainMenuIsSpokenByPositionAndEachScreenItOpensByItsFocus()
    {
        Replay("main-menu-screen-class.txt");

        // The run of 2026-11-17 (build 548ddc95): the main menu from its third entry to System, which opens; inputs
        // without a class (skipped) and a Yes/No dialog; back on the main menu, Equipment, Party and Item opened from
        // their entries (3, 4 and 2) and spoken as unknown screens; then System again.
        Assert.Equal(
            [
                "Menu, Equipment, 3 of 9", "Party, 4 of 9", "Follower, 5 of 9", "Quest, 6 of 9", "Calendar, 7 of 9",
                "Memorandum, 8 of 9", "System, 9 of 9", "1",
                "1",
                "Menu, Equipment, 3 of 9", "Party, 4 of 9", "Follower, 5 of 9", "Party, 4 of 9", "Equipment, 3 of 9", "1",
                "Menu, Party, 4 of 9", "1",
                "Menu, Equipment, 3 of 9", "Item, 2 of 9", "1",
                "Menu, Equipment, 3 of 9", "Party, 4 of 9", "Follower, 5 of 9", "Quest, 6 of 9", "Calendar, 7 of 9",
                "Memorandum, 8 of 9", "System, 9 of 9", "1", "2", "1", "2", "1",
            ],
            announcer.Said.Select(said => said.Text));
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }
}
