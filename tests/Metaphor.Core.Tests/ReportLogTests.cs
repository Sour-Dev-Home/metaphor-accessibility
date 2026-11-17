using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests;

/// <summary>The report log a player attaches to a bug report (#188): labels, events, hook counts and errors, never
/// message text. Made-up messages only.</summary>
[Trait("Category", "Unit")]
public sealed class ReportLogTests
{
    private const string Secret = "The bridge is out, Ada.";

    private static (ReportLog Log, StringWriter Text) NewLog(long now = 1500, params string[] redact)
    {
        var text = new StringWriter();
        return (new ReportLog(text, () => now, redact), text);
    }

    [Fact]
    public void RecordsACompiledMessageFileWithoutItsSource()
    {
        var (log, text) = NewLog();

        log.Observe(new MessageFileCompiled(Path: "event/e100.msg", Id: 7, Source: $"[msg line_1]\n{Secret}"));

        Assert.Equal($"1500 ms compile id=7 path=(not a label, 14 chars) chars=36{Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void RecordsARequestedLineByKindAndLabel()
    {
        var (log, text) = NewLog(now: 2750);

        log.Observe(new LineRequested(Kind: 2, Label: "line_1"));

        Assert.Equal($"2750 ms request kind=2 label=line_1{Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void RecordsACursorInputByItsNumbers()
    {
        var (log, text) = NewLog(now: 300);

        log.Observe(new CursorInput(Manager: 0x1a0, ClassRva: 0x2b0, Command: 0x15, Result: 1, Top: 2, Row: 1, Flags: 0x8));

        Assert.Equal(
            $"300 ms cursor mgr=0x1a0 class=0x2b0 cmd=0x15 result=1 index=3 top=2 row=1 flags=0x8{Environment.NewLine}",
            text.ToString());
    }

    [Fact]
    public void RecordsAnOpenedChoiceByKindAndLabels()
    {
        var (log, text) = NewLog(now: 300);

        log.Observe(new ChoiceRequested(Kind: 1, PromptLabel: "sel_q", ChoiceLabel: null));

        Assert.Equal($"300 ms select kind=1 prompt=sel_q choice=(unreadable){Environment.NewLine}", text.ToString());
    }

    /// <summary>A label read from a bad pointer could be message text, so anything not shaped like the game's labels
    /// is counted, not written.</summary>
    [Fact]
    public void WritesOnlyTheLengthOfALabelNotShapedLikeOne()
    {
        var (log, text) = NewLog(now: 10);

        log.Observe(new LineRequested(Kind: 0, Label: Secret));
        log.Observe(new ChoiceRequested(Kind: 0, PromptLabel: "Go_\nback", ChoiceLabel: "_Go"));
        log.Observe(new MessageFileCompiled(Path: Secret, Id: 1, Source: ""));

        Assert.Equal(
            """
            10 ms request kind=0 label=(not a label, 23 chars)
            10 ms select kind=0 prompt=(not a label, 8 chars) choice=(not a label, 3 chars)
            10 ms compile id=1 path=(not a label, 23 chars) chars=0

            """.ReplaceLineEndings(),
            text.ToString());
    }

    /// <summary>A one-word line of game text has no underscore, so it isn't a label.</summary>
    [Fact]
    public void WritesOnlyTheLengthOfAOneWordMessage()
    {
        var (log, text) = NewLog(now: 10);

        log.Observe(new ChoiceRequested(Kind: 0, PromptLabel: "Yes", ChoiceLabel: "Okay"));

        Assert.Equal($"10 ms select kind=0 prompt=(not a label, 3 chars) choice=(not a label, 4 chars){Environment.NewLine}", text.ToString());
    }

    /// <summary>Labels in the game's shape: letters and digits joined by underscores.</summary>
    [Fact]
    public void WritesALabelShapedLikeTheGames()
    {
        var (log, text) = NewLog(now: 10);

        log.Observe(new ChoiceRequested(Kind: 0, PromptLabel: "MES_e00_002_001_004", ChoiceLabel: "Title_System_08"));

        Assert.Equal($"10 ms select kind=0 prompt=MES_e00_002_001_004 choice=Title_System_08{Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void RecordsANoteWithEachRedactedStringReplaced()
    {
        var (log, text) = NewLog(500, @"D:\Profiles\someone", "someone");

        log.Note(@"hooks 3/4 (can't read D:\Profiles\someone\game.exe as someone)");

        Assert.Equal($"500 ms hooks 3/4 (can't read <redacted>\\game.exe as <redacted>){Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void NeverThrowsWhenTheFileCantBeWritten()
    {
        var log = new ReportLog(new BrokenWriter(), () => 0, []);

        log.Note("hooks 0/1");
        log.Observe(new LineRequested(0, "line_1"));
    }

    /// <summary>The issue's done-when (#188): every way into the log, fed message text, writes none of it.</summary>
    [Fact]
    public void NoMessageTextReachesTheLog()
    {
        var (log, text) = NewLog();

        log.Observe(new MessageFileCompiled(Path: Secret, Id: 1, Source: Secret));
        log.Observe(new LineRequested(Kind: 1, Label: Secret));
        log.Observe(new ChoiceRequested(Kind: 1, PromptLabel: Secret, ChoiceLabel: Secret));
        foreach (var word in Secret.Split(' '))
        {
            log.Observe(new LineRequested(Kind: 1, Label: word));
        }

        Assert.All(Secret.Split(' '), word => Assert.DoesNotContain(word, text.ToString(), StringComparison.Ordinal));
    }

    private sealed class BrokenWriter : StringWriter
    {
        public override void Write(string? value) => throw new IOException("disk full");
    }
}
