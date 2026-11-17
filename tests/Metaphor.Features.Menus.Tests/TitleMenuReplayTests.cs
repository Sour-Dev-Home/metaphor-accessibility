using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Replay;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The title menu's focus (#94) against a recorded session (#149): replays
/// <c>tests/fixtures/sessions/title-menu-cursor.txt</c> through the message and cursor seams, as the hooks feed them,
/// and checks which items were announced, in what order, through the recording announcer. Item text is made up, keyed
/// by the real label; the wording is <see cref="TitleMenuFocusTests"/>' job.</summary>
[Trait("Category", "Unit")]
public sealed class TitleMenuReplayTests
{
    private const string TitleFile = "EN/message/system/Title_System.msg";
    private const string TitleSource = "@Title_System_08\n{\nAlpha\nBravo\nCharlie\n<WAIT>\n}";

    private readonly RecordingAnnouncer announcer = new();

    private void Replay(string scenario)
    {
        var library = new MessageLibrary();
        var focus = new TitleMenuFocus(library, announcer, new TextCleaner(TextCleanerRules.None));
        var messages = new MessageEvents();
        var cursors = new CursorEvents();
        messages.ObserveFiles(library);
        messages.ObserveFiles(focus);
        cursors.Observe(focus);
        var fixture = SessionFixture.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sessions", scenario)));
        fixture.Replay(messages, cursors, source: path => path == TitleFile ? TitleSource : "");
    }

    // The item each announcement names.
    private IEnumerable<string> ItemsSaid() => announcer.Said.Select(said =>
        Items.Single(item => said.Text.Contains(item, StringComparison.Ordinal)));

    private static readonly string[] Items = ["Alpha", "Bravo", "Charlie"];

    [Fact]
    public void EachItemTheCursorSettlesOnIsAnnouncedOnceWhenItChangesAndMovesAreNot()
    {
        Replay("title-menu-cursor.txt");

        // The settled indexes in the fixture, with repeats of the same index dropped.
        Assert.Equal(
            [
                "Alpha", "Bravo", "Alpha", "Bravo", "Alpha", "Bravo", "Charlie", "Bravo", "Charlie",
                "Alpha", "Bravo", "Alpha", "Bravo", "Charlie", "Bravo", "Alpha", "Bravo",
            ],
            ItemsSaid());
    }

    [Fact]
    public void OnlyTheFirstAnnouncementNamesTheMenuAndEachCutsOffTheLast()
    {
        Replay("title-menu-cursor.txt");

        Assert.StartsWith("Main menu", announcer.Said[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain(announcer.Said.Skip(1), said => said.Text.StartsWith("Main menu", StringComparison.Ordinal));
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }
}
