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

    private DialogueChoices Choices() =>
        new(messages, new DialogueAnnouncer(announcer, new TextCleaner(TextCleanerRules.None), history));

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

        Choices().Observe(new ChoiceRequested(0, "MES_q", "MES_c"));

        Assert.Equal(
            [
                new Announcement("Ada: Which way?", Interrupt: true),
                new Announcement("Choose: North; South", Interrupt: false),
            ],
            announcer.Said);
    }

    [Fact]
    public void AChoiceLabelNoCompiledFileHasSaysChoice()
    {
        messages.Compiled(Prompt);

        Choices().Observe(new ChoiceRequested(0, "MES_q", "MES_missing"));

        Assert.Equal(
            [new Announcement("Ada: Which way?", Interrupt: true), new Announcement("Choice", Interrupt: false)],
            announcer.Said);
    }

    [Fact]
    public void AnUnreadableChoiceLabelSaysChoice()
    {
        Choices().Observe(new ChoiceRequested(0, null, null));

        Assert.Equal([new Announcement("Choice", Interrupt: false)], announcer.Said);
    }

    [Fact]
    public void AChoiceMessageOfOnlyTagsSaysChoice()
    {
        messages.Compiled("@MES_c\n{\n<SEL_ITEM>\n<WAIT>\n}");

        Choices().Observe(new ChoiceRequested(0, "MES_missing", "MES_c"));

        Assert.Equal([new Announcement("Choice", Interrupt: false)], announcer.Said);
    }
}
