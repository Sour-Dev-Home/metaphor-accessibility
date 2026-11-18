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

    private static readonly string[] AllKinds = ["compile", "request", "select", "cursor", "pad", "window"];

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
    public void KeepsACursorsScreenClassAndItemCount()
    {
        var fixture = Convert(
            ["1000 ms cursor mgr=0xa0 class=0x22045e8 cmd=0x1 result=1 index=0 top=0 row=0 flags=0x46 screen=A@ope@ui@app count=7"],
            kinds: ["cursor"]);

        Assert.Equal("0 cursor mgr=0xa0 class=0x22045e8 cmd=0x1 result=1 top=0 row=0 flags=0x46 screen=A@ope@ui@app count=7",
            fixture.Split('\n')[3]);
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
    public void KeepsACursorsScreenClass()
    {
        var fixture = Convert(
            ["1800 ms cursor mgr=0x51df310 class=0x22045e8 cmd=0x1 result=1 index=0 top=0 row=0 flags=0x46 screen=TitleChoice@ope@ui@app"],
            kinds: ["cursor"]);

        Assert.Equal("0 cursor mgr=0x51df310 class=0x22045e8 cmd=0x1 result=1 top=0 row=0 flags=0x46 screen=TitleChoice@ope@ui@app",
            fixture.Split('\n')[3]);
    }

    [Fact]
    public void TakesASpikeLogsScreenRecordAsTheScreenClassOfTheCursorJustBeforeIt()
    {
        var fixture = Convert(
            [
                "1800 ms cursor mgr=0x51df310 class=0x22045e8 cmd=0x1 result=1 index=0 top=0 row=0 flags=0x46",
                "1800 ms screen mgr=0x51df310 mgrClass=CursorManager@ope@ui@app owner=0x4 ownerClass=?$ConfigTopChoicePc@VConfigTopChoicePcLayoutInit@ui@app@@@ope@ui@app fields=none",
                "1900 ms cursor mgr=0x51df320 class=0x22045e8 cmd=0x1 result=1 index=1 top=0 row=1 flags=0x46",
                "1900 ms screen mgr=0x51df320 mgrClass=CursorManager@ope@ui@app owner=none ownerClass=? fields=none",
            ],
            kinds: ["cursor"]);

        Assert.Equal(
            [
                "0 cursor mgr=0x51df310 class=0x22045e8 cmd=0x1 result=1 top=0 row=0 flags=0x46 screen=?$ConfigTopChoicePc@VConfigTopChoicePcLayoutInit@ui@app@@@ope@ui@app",
                "100 cursor mgr=0x51df320 class=0x22045e8 cmd=0x1 result=1 top=0 row=1 flags=0x46",
            ],
            fixture.Split('\n')[3..5]);
    }

    [Theory]
    [InlineData("1800 ms cursor mgr=0x51df310 class=0x22045e8 cmd=0x1 result=1 index=0 top=0 row=0 flags=0x46 screen=Made-up")]
    [InlineData("1800 ms screen mgr=0x51df310 mgrClass=? owner=0x4 ownerClass=Made up words fields=none")]
    public void RefusesAScreenClassThatIsNotAClassName(string record)
    {
        var refused = Assert.Throws<FormatException>(() => Convert(
            ["1800 ms cursor mgr=0x51df310 class=0x22045e8 cmd=0x1 result=1 index=0 top=0 row=0 flags=0x46", record]));

        Assert.DoesNotContain("Made", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsAnUnreadableLabelAsADash()
    {
        var fixture = Convert(["1700 ms select kind=0 first=(unreadable) second=\"MES_x_003\""]);

        Assert.Contains("0 select kind=0 prompt=- choice=MES_x_003", fixture, StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsTheWindowsMessagePointerAndADashWhenItWasNotRead()
    {
        // The capture-1 watch's format (dialogue-window-flag-session5-2026-11-17.md) and the Release reader's.
        var fixture = Convert(
        [
            "1000 ms msgwindow window=none handles=none",
            "1100 ms msgwindow current=0x0 state=6 msg+48=- handles=0+0",
            "1200 ms msgwindow current=0x13387b350 state=0 msg+48=1 handles=0+1",
            "1300 ms msgwindow current=? state=? msg+48=- handles=(unreadable)",
            "1400 ms msgwindow window=(unreadable) handles=1+0",
            "1500 ms msgwindow current=0x46474c30",
            "1550 ms msgwindow current=?",
            "1600 ms msgwindow-hook open",
        ], kinds: ["window"]);

        Assert.Equal(
            ["0 window current=-", "100 window current=0x0", "200 window current=0x13387b350", "300 window current=-",
             "400 window current=-", "500 window current=0x46474c30", "550 window current=-", ""],
            fixture.Split('\n')[3..]);
    }

    [Fact]
    public void RefusesAWindowRecordWithText() =>
        Assert.Throws<FormatException>(() => Convert(["1000 ms msgwindow current=Made-up words"], kinds: ["window"]));

    [Fact]
    public void RefusesAHeaderWithoutAGameBuild() =>
        Assert.Throws<ArgumentException>(() => SessionLogConverter.Convert(Log, new FixtureWindow(0, 1, AllKinds),
            new FixtureHeader("A made-up scenario", "docs/raw-sources/example.md", "unknown")));
}
