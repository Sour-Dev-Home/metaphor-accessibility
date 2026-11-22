using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>A reply choice the game opens outside its scripts (#541): its select window alone reports it, then the
/// window shows its prompt. Wired as the hook layer wires it, checked through the recording announcer; made-up messages
/// only.</summary>
[Trait("Category", "Unit")]
public sealed class SelectWindowChoicesTests
{
    private const string Prompt = "@MES_t_001\n{\n<NS>Ada<NE>\nWhich way?\n<WAIT>\n}";
    private const string Options = "@MES_t_002 SEL 0\n{\n<SEL_ITEM>North\n<SEL_ITEM>South\n}";
    private const string YesNo = "@System_Choice_YESNO SEL 0\n{\n<SEL_ITEM>Yes\n<SEL_ITEM>No\n}";
    private const long Cursor = 0xc0;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageLibrary library = new();
    private readonly MessageEvents events = new();
    private readonly ChoiceFocus focus;

    public SelectWindowChoicesTests()
    {
        library.Compiled(Prompt);
        library.Compiled(Options);
        library.Compiled(YesNo);
        var cleaner = new TextCleaner(TextCleanerRules.None);
        var dialogue = new DialogueAnnouncer(announcer, cleaner, new DialogueHistory());
        var lines = new DialogueLines(library, dialogue);
        events.ObserveLines(lines);
        events.ObserveShownLines(lines);
        var choices = new DialogueChoices(library, dialogue);
        focus = new ChoiceFocus(library, announcer, cleaner);
        events.ObserveChoices(lines);
        events.ObserveChoices(choices);
        events.ObserveChoices(focus);
        events.ObserveLines(focus);
        var selects = new SelectWindowChoices(lines, choices, focus);
        events.ObserveChoices(selects);
        events.ObserveSelects(selects);
    }

    private static CursorInput Settle(int row) =>
        new(Cursor, ClassRva: 0x100, Command: 0x1, Result: 1, Top: 0, Row: row, Flags: 0x46);

    // The window shows the prompt in the same call that opens the choice.
    private void WindowOpens(int kind = 0, string choice = "MES_t_002")
    {
        events.SelectOpened(new SelectRequested(kind, "MES_t_001", choice));
        events.Shown(new LineShown(kind, MessageLibrary.LabelHash("MES_t_001")));
    }

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void AChoiceOpenedOnlyByItsWindowSpeaksItsPromptOptionsAndFocus()
    {
        WindowOpens();
        focus.Observe(Settle(0));
        focus.Observe(Settle(1));

        Assert.Equal(["Ada: Which way?", "Choice, 2 options", "North, 1 of 2", "South, 2 of 2"], Said());
        Assert.Equal([true, false, false, true], announcer.Said.Select(said => said.Interrupt));
    }

    [Fact]
    public void AScriptedChoiceIsSpokenOnceThoughItsWindowAlsoReportsIt()
    {
        events.ChoiceOpened(new ChoiceRequested(0, "MES_t_001", "MES_t_002"));
        WindowOpens();
        focus.Observe(Settle(0));

        Assert.Equal(["Ada: Which way?", "Choice, 2 options", "North, 1 of 2"], Said());
    }

    [Fact]
    public void TheSameChoiceOpenedAgainByItsWindowIsSpokenAgain()
    {
        events.ChoiceOpened(new ChoiceRequested(0, "MES_t_001", "MES_t_002"));
        WindowOpens();

        WindowOpens();

        // The speaker is said on the first only (#539).
        Assert.Equal(["Ada: Which way?", "Choice, 2 options", "Which way?", "Choice, 2 options"], Said());
    }

    // Kind 1 is the name entry's confirmation (docs/raw-sources/yes-no-dialog-text-2026-11-20.md); only kind 0 has been
    // seen with a reply choice. A yes/no window is the yes/no dialog's to speak (#569).
    [Theory]
    [InlineData(1, "MES_t_002")]
    [InlineData(0, "System_Choice_YESNO")]
    public void OnlyAKindZeroReplyWindowIsTakenForAChoice(int kind, string choice)
    {
        events.SelectOpened(new SelectRequested(kind, "MES_t_001", choice));
        focus.Observe(Settle(0));

        Assert.Empty(Said());
    }
}
