using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Replay;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>Spoken dialogue (#56) and choices (#82) against recorded sessions (#149): replays fixtures from
/// <c>tests/fixtures/sessions/</c> through the message seam, wired as the hook layer wires it, and checks what was
/// announced, in what order, and that nothing was said twice. Message text is made up, keyed by the real label; the
/// wording is the formatters' tests' job.</summary>
[Trait("Category", "Unit")]
public sealed class DialogueReplayTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageLibrary library = new();
    private readonly MessageEvents messages = new();

    public DialogueReplayTests()
    {
        var dialogue = new DialogueAnnouncer(announcer, new TextCleaner(TextCleanerRules.None), new DialogueHistory());
        messages.ObserveFiles(library);
        messages.ObserveLines(new DialogueLines(library, dialogue));
        messages.ObserveChoices(new DialogueChoices(library, dialogue));
    }

    // Makes up a message for each label the fixture asks for: "Line <the label's last part>".
    private void Replay(string scenario, params string[] choiceSources)
    {
        var fixture = SessionFixture.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sessions", scenario)));
        foreach (var line in fixture.Records.OfType<LineRecord>())
        {
            library.Compiled($"@{line.Line.Label}\n{{\nLine {line.Line.Label[(line.Line.Label.LastIndexOf('_') + 1)..]}\n}}");
        }
        foreach (var source in choiceSources)
        {
            library.Compiled(source);
        }
        fixture.Replay(messages, new CursorEvents());
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EachLineIsSpokenOnceInTheOrderTheGameAskedForIt()
    {
        Replay("lines-in-order.txt");

        Assert.Equal(
            ["Line 000", "Line 001", "Line 002", "Line 003", "Line 004", "Line 005", "Line 006", "Line 007", "Line 008"],
            Said());
    }

    [Fact]
    public void AChoiceSpeaksItsPromptThenItsOptionsOnceBetweenTheLinesAroundIt()
    {
        Replay("choice-prompt-then-options.txt",
            "@MEScommon_AKADEMEIA_CHECK_01\n{\nThe prompt\n}",
            "@MEScommon_AKADEMEIA_CHOICE_01 SEL 0\n{\n<SEL_ITEM>Yes\n<SEL_ITEM>......\n}");

        // The kind 1 request at the end is a system message, which dialogue doesn't speak.
        Assert.Equal(
            [
                "Line 19", "Line 02", "The prompt", "Choose: Yes; dot dot dot",
                "Line 00", "Line 01", "Line 02", "Line 03", "Line 05", "Line 08", "Line 09",
            ],
            Said());
        Assert.False(announcer.Said[3].Interrupt);
    }
}
