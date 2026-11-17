using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The player moves through a reply choice (#95): each option the cursor settles on is spoken as "option, n of
/// total", the first after "Choose: …" and each later one cutting off the last. Checked through the recording
/// announcer; made-up messages only.</summary>
[Trait("Category", "Unit")]
public sealed class ChoiceFocusTests
{
    private const string Options = "@MES_c SEL 0\n{\n<SEL_ITEM>North\n\n<COLOR 3>South</COLOR>\n......\n<WAIT>\n}";
    private const long Choice = 0xc0;
    private const long Other = 0xd0;
    private const int Settle = 0x1;
    private const int Down = 0x16;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageLibrary messages = new();
    private readonly ChoiceFocus focus;

    public ChoiceFocusTests()
    {
        messages.Compiled(Options);
        focus = new(messages, announcer, new TextCleaner(TextCleanerRules.None));
    }

    private static CursorInput Input(long manager, int command, int row) =>
        new(manager, ClassRva: 0x100, command, Result: 1, Top: 0, Row: row, Flags: 0x46);

    private void ChoiceOpens(string? label = "MES_c") => focus.Observe(new ChoiceRequested(0, "MES_q", label));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void TheFirstFocusWaitsForTheOptionsToBeSpoken()
    {
        ChoiceOpens();

        focus.Observe(Input(Choice, Settle, row: 0));

        Assert.Equal([new Announcement("North, 1 of 3", Interrupt: false)], announcer.Said);
    }

    [Fact]
    public void EachLaterOptionIsAnnouncedWithItsPositionCuttingOffTheLast()
    {
        ChoiceOpens();
        focus.Observe(Input(Choice, Settle, row: 0));

        focus.Observe(Input(Choice, Settle, row: 1));
        focus.Observe(Input(Choice, Settle, row: 2));

        Assert.Equal(
            [
                new Announcement("South, 2 of 3", Interrupt: true),
                new Announcement("dot dot dot, 3 of 3", Interrupt: true),
            ],
            announcer.Said.Skip(1));
    }

    [Fact]
    public void AMoveIsAnnouncedOnlyWhereTheCursorSettles()
    {
        ChoiceOpens();
        focus.Observe(Input(Choice, Settle, row: 0));

        focus.Observe(Input(Choice, Down, row: 1));
        focus.Observe(Input(Choice, Settle, row: 0));
        focus.Observe(Input(Choice, Down, row: 1));
        focus.Observe(Input(Choice, Settle, row: 1));

        Assert.Equal(["North, 1 of 3", "South, 2 of 3"], Said());
    }

    [Fact]
    public void OnlyTheFirstCursorActiveAfterTheChoiceOpensIsFollowed()
    {
        ChoiceOpens();
        focus.Observe(Input(Choice, Settle, row: 0));

        focus.Observe(Input(Other, Settle, row: 1));

        Assert.Equal(["North, 1 of 3"], Said());
    }

    [Fact]
    public void NothingIsSaidBeforeAChoiceOpensOrAfterTheNextLine()
    {
        focus.Observe(Input(Choice, Settle, row: 0));
        ChoiceOpens();
        focus.Observe(new LineRequested(0, "MES_next"));

        focus.Observe(Input(Choice, Settle, row: 1));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ANewChoiceStartsAgainWithAWaitingAnnouncement()
    {
        ChoiceOpens();
        focus.Observe(Input(Choice, Settle, row: 1));

        ChoiceOpens();
        focus.Observe(Input(Other, Settle, row: 1));

        Assert.Equal(new Announcement("South, 2 of 3", Interrupt: false), announcer.Said[^1]);
    }

    [Fact]
    public void AChoiceWithoutAMessageOrACursorPastItsOptionsSaysNothing()
    {
        ChoiceOpens("MES_missing");
        focus.Observe(Input(Choice, Settle, row: 0));
        ChoiceOpens();
        focus.Observe(Input(Choice, Settle, row: 3));

        Assert.Empty(announcer.Said);
    }
}
