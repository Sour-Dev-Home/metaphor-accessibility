using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Replay;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The title menu (#94, #208) against recorded sessions (#149): replays fixtures under
/// <c>tests/fixtures/sessions/</c> through the message and cursor seams, as the hooks feed them, and checks which items
/// were announced, in what order, through the recording announcer. Item text is made up, keyed by the real label; the
/// wording is <see cref="TitleMenuFocusTests"/>' job.</summary>
[Trait("Category", "Unit")]
public sealed class TitleMenuReplayTests
{
    private const string TitleFile = "EN/message/system/Title_System.msg";
    private const string TitleSource = "@Title_System_08\n{\nAlpha\nBravo\nCharlie\n<WAIT>\n}";

    private readonly RecordingAnnouncer announcer = new();

    private void Replay(string scenario)
    {
        var library = new MessageLibrary();
        var screens = new MenuScreens([TitleMenuFocus.Screen]);
        // These fixtures predate the menus-open read (#268); the title and the main menu read open (session 6).
        screens.MenusRead(open: true);
        var messages = new MessageEvents();
        var cursors = new CursorEvents();
        messages.ObserveFiles(library);
        cursors.Observe(screens);
        screens.Observe(new TitleMenuFocus(library, announcer, new TextCleaner(TextCleanerRules.None)));
        var fixture = SessionFixture.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sessions", scenario)));
        fixture.Replay(messages, cursors, source: path => path.EndsWith("Title_System.msg", StringComparison.Ordinal) ? TitleSource : "");
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheTitleMenuIsSpokenByItsScreenClassAndAgainWhenSettingsCloses()
    {
        Replay("title-screen-class.txt");

        // Session 4 (build 548ddc95): the title cursor settles, then (from 86487 ms) the System submenu, which the
        // same choice and cursor serve, so it is still read as the title's items (#210); then the title's Settings
        // screens, which aren't known; then back on the title's third item.
        Assert.Equal(
            [
                "Main menu, Alpha, 1 of 3", "Bravo, 2 of 3", "Alpha, 1 of 3", "Bravo, 2 of 3", "Charlie, 3 of 3",
                "Bravo, 2 of 3", "Alpha, 1 of 3", "Bravo, 2 of 3", "Charlie, 3 of 3",
                "Alpha, 1 of 3", "Bravo, 2 of 3", "Alpha, 1 of 3",
                "Main menu, Charlie, 3 of 3", "Bravo, 2 of 3",
            ],
            Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void TheTitleFileCompilingDuringPlaySaysNothingAboutTheTitleMenu()
    {
        Replay("title-file-during-play.txt");

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ARunWithoutScreenClassesSaysNothingAboutTheTitleMenu()
    {
        // The #154 run, before the cursor hook named screens: the title file compiled, then cursors with no class.
        Replay("title-menu-cursor.txt");

        Assert.Empty(announcer.Said);
    }
}
