using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The player moves through a reply choice (#95): each option the cursor settles on is spoken as "option, n of
/// total", the first after "Choice, N options" and each later one cutting off the last. Checked through the recording
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

    // The save prompt of 2026-11-20 (game build 548ddc95…): a scripted yes/no whose cursor is the yes/no dialog's,
    // which the menus already speak; "Yes, 1 of 2" was heard twice. The choice is left to them, and closed, so no later
    // cursor is taken for it.
    [Fact]
    public void AChoiceOnTheYesNoDialogIsLeftToTheMenus()
    {
        ChoiceOpens();

        focus.Observe(Input(Choice, Settle, row: 0) with { ScreenClass = "YesNoChoiceSample@ope@ui@app" });
        focus.Observe(Input(Other, Settle, row: 1));

        Assert.Empty(announcer.Said);
    }

    // #814: the count held for the options is said just before the first of them, once per choice.
    [Fact]
    public void TheFirstOptionIsToldOnceJustBeforeItIsSaid()
    {
        var told = new List<int>();
        var counted = new ChoiceFocus(
            messages, announcer, new TextCleaner(TextCleanerRules.None), () => told.Add(announcer.Said.Count));
        counted.Observe(new ChoiceRequested(0, "MES_q", "MES_c"));

        counted.Observe(Input(Choice, Settle, row: 0));
        counted.Observe(Input(Choice, Settle, row: 1));

        Assert.Equal([0], told);
    }

    // #664: the "of total" is a setting; off says the option alone.
    [Fact]
    public void WithPositionsOffAnOptionIsSaidAlone()
    {
        var quiet = new ChoiceFocus(messages, announcer, new TextCleaner(TextCleanerRules.None)) { SayPositions = () => false };
        quiet.Observe(new ChoiceRequested(0, "MES_q", "MES_c"));

        quiet.Observe(Input(Choice, Settle, row: 0));

        Assert.Equal([new Announcement("North", Interrupt: false)], announcer.Said);
    }

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
