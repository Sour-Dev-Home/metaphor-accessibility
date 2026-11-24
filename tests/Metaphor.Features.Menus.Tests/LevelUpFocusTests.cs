using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The level-up point screen (#624): entering says the game's heading and the stat, each move the stat and its
/// position, and the details key the stat's description; it is told while the menus read closed, after a battle. Hook
/// events in, announcements out; made-up file text only.</summary>
[Trait("Category", "Unit")]
public sealed class LevelUpFocusTests
{
    private const string PointsClass = "ResultDistributionPointChoice@ope@ui@app";
    private const int First = 1700;
    private const int Count = 60;

    private readonly RecordingAnnouncer announcer = new();
    private readonly CursorEvents cursors = new();
    private readonly LevelUpFocus focus;
    private readonly MenuScreens screens = new(KnownScreens.All);
    private bool on = true;

    public LevelUpFocusTests()
    {
        var serials = new MessageSerials([new SerialRange("message/system/System_Message.msg", First, Count)]);
        var messages = new MessageEvents();
        messages.ObserveFiles(serials);
        messages.Compiled(new MessageFileCompiled("EN/message/system/System_Message.msg", 6, string.Join("\n\n",
            Enumerable.Range(0, Count).Select(at => $"//--------\n@System_{at:00}\n{{\nText {First + at}\n<WAIT>\n}}"))));
        // After a battle: the menus read closed.
        screens.MenusRead(open: false);
        cursors.Observe(screens);
        focus = new LevelUpFocus(serials, announcer, new TextCleaner(TextCleanerRules.None), () => on);
        screens.Observe(focus);
        screens.Observe(new UnknownScreenFocus(announcer));
    }

    private ScreenEvent Cursor(int index)
    {
        ScreenEvent? seen = null;
        var watcher = new Watcher(change => seen = change);
        screens.Observe(watcher);
        cursors.Handled(new CursorInput(0xa0, ClassRva: 0x100, Command: 0x1, Result: 1, Top: index, Row: 0, Flags: 0x47,
            PointsClass, Count: 5));
        return seen!;
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    // #624: nothing was said on the point screen.
    [Fact]
    public void EnteringSaysTheHeadingThenTheStat()
    {
        Cursor(0);
        Cursor(3);

        Assert.Equal(["Text 1747, Strength, 1 of 5", "Agility, 4 of 5"], Said());
    }

    [Fact]
    public void TheDetailsKeyReadsTheStatsDescription()
    {
        var focused = Cursor(2);

        Assert.Equal("Text 1754", focus.Details(focused));
    }

    [Fact]
    public void OffTheScreenSaysItsNameAndPositionsOnly()
    {
        on = false;
        var focused = Cursor(0);
        Cursor(1);

        Assert.Equal(["Level up, 1 of 5", "2 of 5"], Said());
        Assert.Null(focus.Details(focused));
    }

    private sealed class Watcher(Action<ScreenEvent> seen) : IScreenObserver
    {
        public void Observe(ScreenEvent change) => seen(change);
    }
}
