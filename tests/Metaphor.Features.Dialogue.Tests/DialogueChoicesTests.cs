using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The game opens a choice (#82): the prompt is spoken as a dialogue line, then the options, without cutting
/// the prompt off. Checked through the recording announcer; made-up messages only.</summary>
[Trait("Category", "Unit")]
public sealed class DialogueChoicesTests
{
    private const string Prompt = "@MES_q\n{\n<NS>Ada<NE>\n<VOICE \"v_q\">\nWhich way?\n<WAIT>\n}";
    private const string Options = "@MES_c SEL 0\n{\n<SEL_ITEM>North\n\n<COLOR 3>South</COLOR>\n<WAIT>\n}";

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageLibrary messages = new();
    private readonly DialogueHistory history = new();

    private readonly FakeClock clock = new();
    private readonly DialogueAnnouncer dialogue;

    public DialogueChoicesTests() =>
        dialogue = new DialogueAnnouncer(
            announcer, new TextCleaner(TextCleanerRules.None), history, timeProvider: clock);

    private DialogueChoices Choices() => new(messages, dialogue);

    // A choice opens and its window follows.
    private void Open(ChoiceRequested choice)
    {
        var choices = Choices();
        choices.Observe(choice);
        choices.Observe(Window);
    }

    // The select window opening, which shows the options (#744).
    private static readonly SelectRequested Window = new(0, "MES_q", "MES_c");

    [Fact]
    public void ThePromptIsKeptInTheHistoryButTheOptionsAreNot()
    {
        messages.Compiled(Prompt);
        messages.Compiled(Options);

        Choices().Observe(new ChoiceRequested(0, "MES_q", "MES_c"));

        Assert.Equal(["Ada: Which way?"], history.Lines());
    }

    [Fact]
    public void ThePromptIsSpokenAsALineThenTheOptionsWithoutCuttingItOff()
    {
        messages.Compiled(Prompt);
        messages.Compiled(Options);

        var choices = Choices();
        choices.Observe(new ChoiceRequested(0, "MES_q", "MES_c"));
        choices.Observe(Window);

        Assert.Equal(
            [
                new Announcement("Ada: Which way?", Interrupt: true),
                new Announcement("Choose: North; South", Interrupt: false),
            ],
            announcer.Said);
    }

    // The options appear on screen some seconds after the line (about 4 s, session c, #744): they are held until the
    // select window opens, not said after a fixed delay.
    [Fact]
    public void OptionsWaitForTheWindowToOpenHoweverLongItTakes()
    {
        messages.Compiled(Prompt);
        messages.Compiled(Options);
        var choices = Choices();

        choices.Observe(new ChoiceRequested(0, "MES_q", "MES_c"));
        clock.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal([new Announcement("Ada: Which way?", Interrupt: true)], announcer.Said);

        choices.Observe(Window);

        Assert.Equal(2, announcer.Said.Count);
        Assert.Equal("Choose: North; South", announcer.Said[1].Text);
    }

    [Fact]
    public void OptionsAreSaidOnceThoughTheWindowOpensAgain()
    {
        messages.Compiled(Prompt);
        messages.Compiled(Options);
        var choices = Choices();

        choices.Observe(new ChoiceRequested(0, "MES_q", "MES_c"));
        choices.Observe(Window);
        choices.Observe(Window);

        Assert.Equal(2, announcer.Said.Count);
    }

    [Fact]
    public void OptionsAreSaidAnywayIfTheWindowNeverReportsOpening()
    {
        messages.Compiled(Prompt);
        messages.Compiled(Options);

        Choices().Observe(new ChoiceRequested(0, "MES_q", "MES_c"));
        clock.Advance(DialogueAnnouncer.HoldLimit);

        Assert.Equal("Choose: North; South", announcer.Said[^1].Text);
    }

    [Fact]
    public void OptionsAreDroppedWhenTheConversationEndsBeforeTheWindowOpens()
    {
        messages.Compiled(Prompt);
        messages.Compiled(Options);
        var choices = Choices();

        choices.Observe(new ChoiceRequested(0, "MES_q", "MES_c"));
        dialogue.EndConversation();
        choices.Observe(Window);
        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal([new Announcement("Ada: Which way?", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void AReplacedChoiceDropsTheOptionsStillHeld()
    {
        messages.Compiled(Prompt);
        messages.Compiled(Options);
        messages.Compiled("@MES_d SEL 0\n{\n<SEL_ITEM>East\n<WAIT>\n}");
        var choices = Choices();

        choices.Observe(new ChoiceRequested(0, "MES_q", "MES_c"));
        choices.Observe(new ChoiceRequested(0, "MES_q", "MES_d"));
        choices.Observe(Window);

        Assert.Equal(
            ["Ada: Which way?", "Which way?", "Choose: East"],
            announcer.Said.Select(said => said.Text));
    }

    [Fact]
    public void AChoiceLabelNoCompiledFileHasSaysChoice()
    {
        messages.Compiled(Prompt);

        var choices = Choices();
        choices.Observe(new ChoiceRequested(0, "MES_q", "MES_missing"));
        choices.Observe(Window);

        Assert.Equal(
            [new Announcement("Ada: Which way?", Interrupt: true), new Announcement("Choice", Interrupt: false)],
            announcer.Said);
    }

    [Fact]
    public void AnUnreadableChoiceLabelSaysChoice()
    {
        Open(new ChoiceRequested(0, null, null));

        Assert.Equal([new Announcement("Choice", Interrupt: false)], announcer.Said);
    }

    [Fact]
    public void AChoiceMessageOfOnlyTagsSaysChoice()
    {
        messages.Compiled("@MES_c\n{\n<SEL_ITEM>\n<WAIT>\n}");

        Open(new ChoiceRequested(0, "MES_missing", "MES_c"));

        Assert.Equal([new Announcement("Choice", Interrupt: false)], announcer.Said);
    }

    [Theory]
    [InlineData("......")]
    [InlineData("…")]
    [InlineData(". . .")]
    public void AnOptionOfOnlyDotsIsSpokenAsDotDotDot(string dots)
    {
        messages.Compiled($"@MES_c SEL 0\n{{\n<SEL_ITEM>North\n\n{dots}\n<WAIT>\n}}");

        Open(new ChoiceRequested(0, "MES_missing", "MES_c"));

        Assert.Equal([new Announcement("Choose: North; dot dot dot", Interrupt: false)], announcer.Said);
    }

    [Fact]
    public void AnOptionMerelyEndingInDotsIsLeftAsItIs()
    {
        messages.Compiled("@MES_c SEL 0\n{\n<SEL_ITEM>Well...\n<WAIT>\n}");

        Open(new ChoiceRequested(0, "MES_missing", "MES_c"));

        Assert.Equal([new Announcement("Choose: Well...", Interrupt: false)], announcer.Said);
    }
}
