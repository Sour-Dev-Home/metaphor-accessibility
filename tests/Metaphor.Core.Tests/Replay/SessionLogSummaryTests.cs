using Metaphor.Core.Replay;

namespace Metaphor.Core.Tests.Replay;

/// <summary>The summary (#244) reports kinds, counts, class names and times, and never any game text. Checked on a
/// made-up log that holds game-like text in every field that can carry it.</summary>
[Trait("Category", "Unit")]
public sealed class SessionLogSummaryTests
{
    private static readonly string[] Log =
    [
        "1000 ms compile id=56 path=EN/message/Secret_Path.msg chars=24",
        "  | Made-up secret sentence one",
        "1100 ms request via=MsgRequestByName kind=0 text=\"Hidden dialogue words\"",
        "1200 ms select kind=0 first=\"Hidden prompt\" second=\"Hidden choice\"",
        "1300 ms screen mgr=0x51df310 mgrClass=MenuScreen owner=0x1 ownerClass=Owner fields=none",
        "1400 ms cursor mgr=0x51df310 class=0x22045e8 cmd=0x14 result=20 index=2 top=0 row=2 flags=0x46",
        "1500 ms cursor mgr=0x99 class=? cmd=0x14 result=20 index=2 top=0 row=2 flags=0x46",
        "1600 ms screen mgr=0x51df310 mgrClass=MenuScreen owner=0x1 ownerClass=Owner fields=none",
        "1700 ms pane 0x1 colour=0x2 text=elem+0x10 \"Hidden pane words\"",
        "1800 ms mystery-kind label=\"Hidden label words\"",
        "Not a record: Hidden stray words",
    ];

    [Fact]
    public void PrintsNoGameTextFromAnyRecord()
    {
        var output = SessionLogSummary.Summarize(Log, new SummaryOptions(1000, 1500, 400));

        foreach (var leak in new[] { "secret", "Secret", "Hidden", "dialogue", "Made-up", "EN/message" })
        {
            Assert.DoesNotContain(leak, output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CountsByKindScreenClassAndWindowWithVideoTimes()
    {
        var output = SessionLogSummary.Summarize(Log, new SummaryOptions(1000, 1500, 400));

        Assert.Equal(
            """
records: 9, text continuation lines: 1, unrecognised lines: 1
log time: 1000ms(video 0:00.600) to 1800ms(video 0:01.400)
by kind:
  compile 1
  cursor 2
  mystery-kind 1
  pane 1
  request 1
  screen 2
  select 1
by screen class (first, last, screen records, cursor inputs):
  MenuScreen 1300ms(video 0:00.900) 1600ms(video 0:01.200) 2 1
  cursor inputs with no screen record: 1
window 1000 to 1500 ms: 6 records
  compile 1
  cursor 2
  request 1
  screen 1
  select 1

""".ReplaceLineEndings(), output.ReplaceLineEndings());
    }

    [Fact]
    public void ShowsPlainMillisecondsWithoutAnOffset()
    {
        Assert.Contains("log time: 1000ms to 1800ms", SessionLogSummary.Summarize(Log, new SummaryOptions()), StringComparison.Ordinal);
    }
}
