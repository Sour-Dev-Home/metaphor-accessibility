using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The game's message log read by position (#685), with a fake screen: the cursor inputs the 2026-11-21 session
/// logged when the log opened (index 28 of 29, cmd 0x1, flags 0x47) and moved (cmd 0x15 and 0x14).</summary>
[Trait("Category", "Unit")]
public sealed class MessageLogFocusTests
{
    private const long Manager = 0x42813ab0;
    private const long SharedCursorClass = 0x22045e8;

    private readonly RecordingAnnouncer said = new();
    private readonly MessageLogFocus focus;

    public MessageLogFocusTests() => focus = new MessageLogFocus(said);

    private static CursorInput Settled(int index, string? screen = MessageLogFocus.ScreenClass, long manager = Manager) =>
        new(manager, SharedCursorClass, Command: 0x1, Result: 1, Top: index, Row: 0, Flags: 0x47, screen, Count: 29);

    private static CursorInput Move(int index) =>
        new(Manager, SharedCursorClass, Command: 0x15, Result: 1, Top: index, Row: 0, Flags: 0x47,
            MessageLogFocus.ScreenClass, Count: 29);

    private static Announcement Said(string text) => new(text, Interrupt: true);

    [Fact]
    public void OpeningTheLogSaysItsNameAndTheFocusedLinesPosition()
    {
        focus.Observe(Settled(28));

        Assert.Equal([Said("Message log, line 29 of 29")], said.Said);
    }

    [Fact]
    public void EachLineSettledOnSaysItsPosition()
    {
        focus.Observe(Settled(28));
        focus.Observe(Move(27));
        focus.Observe(Settled(27));
        focus.Observe(Settled(26));

        Assert.Equal(
            [Said("Message log, line 29 of 29"), Said("Line 28 of 29"), Said("Line 27 of 29")], said.Said);
    }

    // An equal input reaches observers only when the log went uncalled for a while: it was closed and opened again.
    [Fact]
    public void TheSameLineToldAgainIsTheLogOpenedAgain()
    {
        focus.Observe(Settled(28));
        focus.Observe(Settled(28));

        Assert.Equal([Said("Message log, line 29 of 29"), Said("Message log, line 29 of 29")], said.Said);
    }

    [Fact]
    public void AnotherScreensCursorIsNotTheLogs()
    {
        focus.Observe(Settled(3, screen: "RootMenuChoice@ope@ui@app"));
        focus.Observe(Settled(3, screen: null));

        Assert.Empty(said.Said);
    }

    [Fact]
    public void AfterAnotherScreenTheLogSaysItsNameAgain()
    {
        focus.Observe(Settled(28));
        focus.Observe(Settled(0, screen: "YesNoChoiceSample@ope@ui@app", manager: 0x5000));
        focus.Observe(Settled(27));

        Assert.Equal([Said("Message log, line 29 of 29"), Said("Message log, line 28 of 29")], said.Said);
    }

    // A reply choice waiting for its cursor mustn't take the log's (ChoiceFocus).
    [Fact]
    public void AChoiceWaitingForItsCursorDoesNotTakeTheLogs()
    {
        var messages = new MessageLibrary();
        messages.Compiled("@MES_c SEL 0\n{\n<SEL_ITEM>North\n\nSouth\n<WAIT>\n}");
        var options = new RecordingAnnouncer();
        var choice = new ChoiceFocus(messages, options, new TextCleaner(TextCleanerRules.None));
        choice.Observe(new ChoiceRequested(0, "MES_q", "MES_c"));

        choice.Observe(Settled(0));
        choice.Observe(Settled(1, screen: null, manager: 0xc0));

        Assert.Equal(["South, 2 of 2"], options.Said.Select(option => option.Text));
    }
}
