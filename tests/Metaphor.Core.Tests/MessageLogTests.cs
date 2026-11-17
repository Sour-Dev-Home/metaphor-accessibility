using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests;

/// <summary>The #55 spike's message log: what the listening hooks saw, in order, for reading after a play session.
/// Made-up messages only.</summary>
[Trait("Category", "Unit")]
public sealed class MessageLogTests
{
    private static (MessageLog Log, StringWriter Text) NewLog(long now = 1500)
    {
        var text = new StringWriter();
        return (new MessageLog(text, () => now), text);
    }

    [Fact]
    public void RecordsACompiledMessageFileWithItsSourceIndented()
    {
        var (log, text) = NewLog();

        log.Observe(new MessageFileCompiled(Path: "event/e100.msg", Id: 7, Source: "@line_1\n<NS>Ada<NE>The bridge is out."));

        Assert.Equal(
            """
            1500 ms compile id=7 path=event/e100.msg chars=37
              | @line_1
              | <NS>Ada<NE>The bridge is out.

            """.ReplaceLineEndings(),
            text.ToString());
    }

    [Fact]
    public void RecordsALookupWithItsMessageAndLabel()
    {
        var (log, text) = NewLog(now: 2750);

        log.LookedUp(messageId: 7, labelIndex: 0);

        Assert.Equal($"2750 ms lookup id=7 label=0{Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void RecordsAScriptFunctionTheGameRegistered()
    {
        var (log, text) = NewLog(now: 900);

        log.Registered(name: "MSG_WND_OPEN", rva: 0x1a2b3c, viaUserdata: true);
        log.Registered(name: "print", rva: 0x99, viaUserdata: false);

        Assert.Equal(
            $"900 ms register name=MSG_WND_OPEN rva=0x1a2b3c userdata{Environment.NewLine}" +
            $"900 ms register name=print rva=0x99 direct{Environment.NewLine}",
            text.ToString());
    }

    [Fact]
    public void RecordsAMessageRequestWithItsFunctionAndArguments()
    {
        var (log, text) = NewLog(now: 4200);

        log.Requested(function: "MsgRequestByName", arguments: "kind=0 text=\"line_1\"");

        Assert.Equal(
            $"4200 ms request via=MsgRequestByName kind=0 text=\"line_1\"{Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void RecordsASoundCueWithItsHandlesAndCaller()
    {
        var (log, text) = NewLog(now: 3300);

        log.CueSet(cueId: 42, acb: 0x1f2e3d40, player: 0x2a00, callerRva: 0x12345, callTargetRva: 0x126438d0);
        log.CueSet(cueId: -1, acb: 0, player: 0, callerRva: null, callTargetRva: null);

        Assert.Equal(
            $"3300 ms cue id=42 acb=0x1f2e3d40 player=0x2a00 caller=0x12345 call=0x126438d0{Environment.NewLine}" +
            $"3300 ms cue id=-1 acb=0x0 player=0x0 caller=(unknown) call=(unknown){Environment.NewLine}",
            text.ToString());
    }

    [Fact]
    public void RecordsAMenuCursorInputAsNumbers()
    {
        var (log, text) = NewLog(now: 32257);

        log.Observe(new Metaphor.Core.Menus.CursorInput(
            Manager: 0xa0, ClassRva: 0x100, Command: 0x16, Result: 22, Top: 1, Row: 2, Flags: 0x46));
        log.Observe(new Metaphor.Core.Menus.CursorInput(
            Manager: 0xa0, ClassRva: null, Command: 0x1, Result: 1, Top: 0, Row: 0, Flags: 0x66));

        Assert.Equal(
            $"32257 ms cursor mgr=0xa0 class=0x100 cmd=0x16 result=22 index=3 top=1 row=2 flags=0x46{Environment.NewLine}" +
            $"32257 ms cursor mgr=0xa0 class=? cmd=0x1 result=1 index=0 top=0 row=0 flags=0x66{Environment.NewLine}",
            text.ToString());
    }

    [Fact]
    public void RecordsAResolvedMessage()
    {
        var (log, text) = NewLog(now: 77);

        log.Resolved(function: "MessageSerialByLabel", arguments: "text=\"label_1\" serial=3");

        Assert.Equal(
            $"77 ms resolve via=MessageSerialByLabel text=\"label_1\" serial=3{Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void RecordsAPlayerNameSightingWithoutItsText()
    {
        var (log, text) = NewLog(now: 5);

        log.PlayerNameSeen("CreateMsgFromString", new PlayerNameCheck("Ardent").Sight("Ardent", "heap"));

        Assert.Equal(
            $"5 ms player-name via=CreateMsgFromString bytes=6 match=exact at=heap{Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void RecordsALineTheGameRequestedByName()
    {
        var (log, text) = NewLog(now: 4200);

        log.Observe(new LineRequested(Kind: 0, Label: "MES_a"));

        Assert.Equal(
            $"4200 ms request via=MsgRequestByName kind=0 text=\"MES_a\"{Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void RecordsAChoiceWithBothLabels()
    {
        var (log, text) = NewLog(now: 4200);

        log.Observe(new ChoiceRequested(Kind: 0, PromptLabel: "MES_a", ChoiceLabel: null));

        Assert.Equal(
            $"4200 ms select kind=0 first=\"MES_a\" second=(unreadable){Environment.NewLine}",
            text.ToString());
    }

    [Fact]
    public void ASelectedLabelStaysOnOneLine()
    {
        var (log, text) = NewLog(now: 1);

        log.Observe(new ChoiceRequested(0, "a\"b\nc", ""));

        Assert.Equal($"1 ms select kind=0 first=\"a\\x22b\\x0ac\" second=\"\"{Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void RecordsTheSelectedId()
    {
        var (log, text) = NewLog(now: 7);

        log.SelectedId(-1);

        Assert.Equal($"7 ms selected id=-1{Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void ARequestedLabelStaysOnOneLine()
    {
        var (log, text) = NewLog(now: 1);

        log.Observe(new LineRequested(2, "a\"b\\c\nd"));

        Assert.Equal(
            $"1 ms request via=MsgRequestByName kind=2 text=\"a\\x22b\\x5cc\\x0ad\"{Environment.NewLine}", text.ToString());
    }

    [Fact]
    public void NeverThrowsEvenOnValuesItCannotRecord()
    {
        var (log, text) = NewLog();

        log.Observe(new MessageFileCompiled(null!, 1, null!));
        log.Observe((LineRequested)null!);
        log.Registered(null!, 0, viaUserdata: false);

        Assert.Contains(" compile id=1 ", text.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsWritingAfterTheWriterFails()
    {
        var log = new MessageLog(new FailingWriter(), () => 0);

        log.Observe(new MessageFileCompiled("a.msg", 1, "x"));
        log.LookedUp(1, 0);
    }

    [Fact]
    public void LinesFromManyThreadsNeverInterleave()
    {
        var (log, text) = NewLog();

        Parallel.For(0, 200, i => log.Observe(new MessageFileCompiled("f.msg", i, "one\ntwo")));

        var lines = text.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(600, lines.Length);
        for (var i = 0; i < lines.Length; i += 3)
        {
            Assert.Contains(" compile id=", lines[i], StringComparison.Ordinal);
            Assert.Equal(["  | one", "  | two"], lines[(i + 1)..(i + 3)]);
        }
    }

    private sealed class FailingWriter : StringWriter
    {
        public override void Write(string? value) => throw new IOException("disk full");
    }
}
