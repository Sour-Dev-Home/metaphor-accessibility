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

        log.Compiled(id: 7, path: "event/e100.msg", source: "@line_1\n<NS>Ada<NE>The bridge is out.");

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
    public void NeverThrowsEvenOnValuesItCannotRecord()
    {
        var (log, text) = NewLog();

        log.Compiled(1, null!, null!);
        log.Registered(null!, 0, viaUserdata: false);

        Assert.Contains(" compile id=1 ", text.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsWritingAfterTheWriterFails()
    {
        var log = new MessageLog(new FailingWriter(), () => 0);

        log.Compiled(1, "a.msg", "x");
        log.LookedUp(1, 0);
    }

    [Fact]
    public void LinesFromManyThreadsNeverInterleave()
    {
        var (log, text) = NewLog();

        Parallel.For(0, 200, i => log.Compiled(i, "f.msg", "one\ntwo"));

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
