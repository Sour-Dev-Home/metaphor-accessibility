using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The game-over screen (#623): its voiced line as the window shows it, then the screen's words and how to go
/// on once the window closes, checked through the recording announcer.</summary>
[Trait("Category", "Unit")]
public sealed class GameOverScreenTests
{
    private const string Lines =
        "@MES_GAMEOVER_000_000\n{\n<NS>Ada<NE>\n<VOICE \"v_co_00001_c051\">\nIs this the end?\n<WAIT>\n}\n"
        + "@MES_a\n{\n<NS>Ada<NE>\nThe bridge is out.\n<WAIT>\n}";

    private const string Words = "Fantasy is Dead. Press A to continue.";

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageLibrary messages = new();

    public GameOverScreenTests() => messages.Compiled(Lines);

    private GameOverScreen Screen() =>
        new(messages, new DialogueAnnouncer(announcer, new TextCleaner(TextCleanerRules.None), new DialogueHistory()), announcer);

    private static LineShown Shown(int kind, string label) => new(kind, MessageLibrary.LabelHash(label));

    [Fact]
    public void TheVoicedLineIsSpokenAsTheWindowShowsIt()
    {
        Screen().Observe(Shown(2, "MES_GAMEOVER_000_000"));

        Assert.Equal([new Announcement("Ada: Is this the end?", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void TheScreensWordsFollowOnceTheWindowCloses()
    {
        var screen = Screen();
        screen.Observe(Shown(2, "MES_GAMEOVER_000_000"));

        screen.WindowClosed();

        Assert.Equal(new Announcement(Words, Interrupt: true), announcer.Said[^1]);
    }

    [Fact]
    public void AWindowClosingAfterAnyOtherLineSaysNothing()
    {
        var screen = Screen();
        screen.Observe(Shown(2, "MES_a"));
        screen.Observe(Shown(0, "MES_a"));

        screen.WindowClosed();

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void TheWordsAreSaidOncePerGameOver()
    {
        var screen = Screen();
        screen.Observe(Shown(2, "MES_GAMEOVER_000_000"));
        screen.WindowClosed();
        screen.WindowClosed();

        Assert.Equal(2, announcer.Said.Count);
    }
}
