using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Replay;

namespace Metaphor.Features.Menus.Tests;

/// <summary>Leaving the menus (#268) replayed through the seams the hooks feed: cursor inputs, the game's menus-open
/// reads and pad reads, with the menu-screens module and the menu controls wired as the mod wires them. The fixture
/// follows capture session 6's order of events: the title, which reads open, a save loaded to the field, the main menu
/// opened and closed, a battle, a dialogue's choice and a return to the title.</summary>
[Trait("Category", "Unit")]
public sealed class MenusClosedReplayTests
{
    private const int R3 = 0x0004;
    private const string Title = "TitleChoice@ope@ui@app";
    private const string Root = "RootMenuChoice@ope@ui@app";
    private const string TalkChoice = "CommonTalkChoice@ope@ui@app";

    private readonly RecordingAnnouncer announcer = new();
    private readonly List<string> screenEvents = [];
    private readonly List<int> presses = [];

    public MenusClosedReplayTests()
    {
        var screens = new MenuScreens([TitleMenuFocus.Screen, MainMenuFocus.Screen]);
        var cursors = new CursorEvents();
        cursors.Observe(screens);
        screens.Observe(new Recording(screenEvents));
        var controls = new MenuControls(MenuBindings.Default, screens, new Reader(announcer), []);
        screens.Observe(controls);
        var fixture = SessionFixture.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sessions", "menus-closed-session6.txt")));
        fixture.Replay(new MessageEvents(), cursors, pad: read =>
        {
            var handed = controls.Pad(read.Pad, read.Buttons);
            if ((read.Buttons & R3) != 0)
            {
                presses.Add(handed);
            }
        }, menus: screens.MenusRead);
    }

    [Fact]
    public void TheDetailsKeyIsTakenOnTheTitleAndInTheMainMenuAndReachesTheGameEverywhereElse()
    {
        Assert.Equal(
            [
                0,  // the title, which reads open
                R3, // the field, after the save loaded
                0,  // the main menu
                R3, // the field, after it closed
                R3, // a battle
                R3, // a dialogue's choice
                0,  // the title again
            ],
            presses);
        Assert.Equal(["No more details", "No more details", "No more details"], announcer.Said.Select(said => said.Text));
    }

    [Fact]
    public void EachCloseLeavesTheScreenThatWasOpen()
    {
        Assert.Equal(
            [
                $"Entered {Title} 0", $"FocusMoved {Title} 1", $"Left {Title} 1",
                $"Entered {Root} 0", $"Left {Root} 0",
                $"Entered {TalkChoice} 0",
                $"Left {TalkChoice} 0", $"Entered {Root} 8", $"Left {Root} 8",
                $"Entered {Title} 0",
            ],
            screenEvents);
    }

    private sealed class Recording(List<string> seen) : IScreenObserver
    {
        public void Observe(ScreenEvent change) => seen.Add($"{change.Change} {change.ScreenClass} {change.Index}");
    }

    /// <summary>A reader that is always on.</summary>
    private sealed class Reader(IAnnouncer announcer) : IReaderSwitch
    {
        public bool IsOn => true;

        public void Announce(Announcement announcement) => announcer.Announce(announcement);

        public void Toggle()
        {
        }
    }
}
