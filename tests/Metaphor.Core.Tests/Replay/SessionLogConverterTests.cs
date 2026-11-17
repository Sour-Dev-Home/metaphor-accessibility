using Metaphor.Core.Replay;

namespace Metaphor.Core.Tests.Replay;

/// <summary>The converter (#149) turns a window of a local <c>message-log.txt</c> into a replay fixture that holds only
/// record kinds, labels, numbers and relative timings, and refuses a record whose kept field looks like text. Checked on
/// a made-up log.</summary>
[Trait("Category", "Unit")]
public sealed class SessionLogConverterTests
{
    private const string Build = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";

    private static readonly string[] Log =
    [
        "900 ms request via=MsgRequestByName kind=0 text=\"MES_x_000\"",
        "1000 ms compile id=56 path=EN/message/system/Title_System.msg chars=24",
        "  | @Title_System_08",
        "  | Made-up words",
        "1010 ms pane 0x1 colour=0x2 text=elem+0x10 \"Made-up words\"",
        "1020 ms lookup id=56 label=3",
        "1500 ms request via=MsgRequestByName kind=0 text=\"MES_x_001\"",
        "1600 ms request via=TutorialWindowOpenRequest text=\"Made-up words\"",
        "1700 ms select kind=0 first=\"MES_x_002\" second=\"MES_x_003\"",
        "1800 ms cursor mgr=0x51df310 class=0x22045e8 cmd=0x14 result=20 index=2 top=0 row=2 flags=0x46",
        "1900 ms input pad1 buttons=0x8000 handed=0x8000 returned=0",
        "1950 ms input keyboard keys=0x00000000 held=- handed=- returned=1",
        "2500 ms request via=MsgRequestByName kind=0 text=\"MES_x_004\"",
    ];

    private static readonly string[] AllKinds = ["compile", "request", "select", "cursor", "pad"];

    private static string Convert(IEnumerable<string> log, long from = 1000, long to = 2000, string[]? kinds = null) =>
        SessionLogConverter.Convert(log, new FixtureWindow(from, to, kinds ?? AllKinds),
            new FixtureHeader("A made-up scenario", "docs/raw-sources/example.md", Build));

    [Fact]
    public void KeepsOnlyKindsLabelsAndNumbersWithTimesFromTheWindowsFirstRecord()
    {
        Assert.Equal(
            """
            # A made-up scenario
            # source: docs/raw-sources/example.md
            # game build: 548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504
            0 compile id=56 path=EN/message/system/Title_System.msg
            500 request kind=0 label=MES_x_001
            700 select kind=0 prompt=MES_x_002 choice=MES_x_003
            800 cursor mgr=0x51df310 class=0x22045e8 cmd=0x14 result=20 top=0 row=2 flags=0x46
            900 pad pad=1 buttons=0x8000 returned=0

            """.ReplaceLineEndings("\n"),
            Convert(Log));
    }

    [Fact]
    public void KeepsOnlyTheKindsAskedFor()
    {
        var fixture = Convert(Log, kinds: ["cursor"]);

        Assert.Equal("0 cursor mgr=0x51df310 class=0x22045e8 cmd=0x14 result=20 top=0 row=2 flags=0x46",
            fixture.Split('\n')[3]);
        Assert.Equal(5, fixture.Split('\n').Length);
    }

    [Theory]
    [InlineData("1500 ms request via=MsgRequestByName kind=0 text=\"Made-up words\"")]
    [InlineData("1500 ms request via=MsgRequestByName kind=0 text=\"Hello\"")]
    [InlineData("1700 ms select kind=0 first=\"MES_x_002\" second=\"Pick one, please\"")]
    [InlineData("1000 ms compile id=56 path=D:/Games/someone/Title_System.msg chars=24")]
    [InlineData("1800 ms cursor mgr=0x51df310 class=0x22045e8 cmd=one result=20 index=2 top=0 row=2 flags=0x46")]
    public void RefusesARecordWhoseKeptFieldLooksLikeTextWithoutRepeatingIt(string record)
    {
        var refused = Assert.Throws<FormatException>(() => Convert([record]));

        Assert.Contains("line 1", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Made-up", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Pick one", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("someone", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsAnUnreadableLabelAsADash()
    {
        var fixture = Convert(["1700 ms select kind=0 first=(unreadable) second=\"MES_x_003\""]);

        Assert.Contains("0 select kind=0 prompt=- choice=MES_x_003", fixture, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAHeaderWithoutAGameBuild() =>
        Assert.Throws<ArgumentException>(() => SessionLogConverter.Convert(Log, new FixtureWindow(0, 1, AllKinds),
            new FixtureHeader("A made-up scenario", "docs/raw-sources/example.md", "unknown")));
}
