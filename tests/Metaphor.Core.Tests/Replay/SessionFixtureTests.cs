using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Replay;

namespace Metaphor.Core.Tests.Replay;

/// <summary>A fixture (#149) replays its records, in order, through the seams the hooks feed: compiled files, line and
/// choice requests through <see cref="MessageEvents"/>, cursor inputs through <see cref="CursorEvents"/>, pad reads to a
/// callback. Made-up fixtures and text only.</summary>
[Trait("Category", "Unit")]
public sealed class SessionFixtureTests
{
    private const string Build = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";

    private const string Fixture = $"""
        # A made-up scenario
        # source: docs/raw-sources/example.md
        # game build: {Build}
        0 compile id=56 path=EN/message/system/Title_System.msg
        500 request kind=0 label=MES_x_001
        700 select kind=0 prompt=- choice=MES_x_003
        800 cursor mgr=0x51df310 class=0x22045e8 cmd=0x14 result=20 top=0 row=2 flags=0x46
        900 pad pad=1 buttons=0x8000 returned=0
        """;

    private sealed class Seen : IMessageFileObserver, ILineRequestObserver, IChoiceRequestObserver, ICursorObserver
    {
        public List<object> Events { get; } = [];

        public void Observe(MessageFileCompiled file) => Events.Add(file);

        public void Observe(LineRequested line) => Events.Add(line);

        public void Observe(ChoiceRequested choice) => Events.Add(choice);

        public void Observe(CursorInput input) => Events.Add(input);
    }

    [Fact]
    public void ReplaysEachRecordInOrderThroughTheSeamsWithTheSourceGivenForEachFile()
    {
        var seen = new Seen();
        var messages = new MessageEvents();
        var cursors = new CursorEvents();
        messages.ObserveFiles(seen);
        messages.ObserveLines(seen);
        messages.ObserveChoices(seen);
        cursors.Observe(seen);

        SessionFixture.Parse(Fixture).Replay(messages, cursors, pad => seen.Events.Add(pad),
            path => path == "EN/message/system/Title_System.msg" ? "@Title_System_08\n{\nMade up\n}" : "");

        Assert.Equal(
            [
                new MessageFileCompiled("EN/message/system/Title_System.msg", 56, "@Title_System_08\n{\nMade up\n}"),
                new LineRequested(0, "MES_x_001"),
                new ChoiceRequested(0, null, "MES_x_003"),
                new CursorInput(0x51df310, 0x22045e8, 0x14, 20, 0, 2, 0x46),
                new PadRead(1, 0x8000, 0),
            ],
            seen.Events);
    }

    [Fact]
    public void KeepsTheGameBuildAndTheTimes()
    {
        var fixture = SessionFixture.Parse(Fixture);

        Assert.Equal(Build, fixture.GameBuild);
        Assert.Equal([0L, 500, 700, 800, 900], fixture.Records.Select(record => record.Milliseconds));
    }

    [Fact]
    public void ReadsWhatTheConverterWrites()
    {
        var written = SessionLogConverter.Convert(
            ["10 ms cursor mgr=0xa0 class=? cmd=0x1 result=1 index=1 top=0 row=1 flags=0x46"],
            new FixtureWindow(0, 100, ["cursor"]), new FixtureHeader("Made up", "docs/raw-sources/example.md", Build));

        var record = Assert.Single(SessionFixture.Parse(written).Records);

        Assert.Equal(new CursorInput(0xa0, null, 0x1, 1, 0, 1, 0x46), Assert.IsType<CursorRecord>(record).Input);
    }

    [Fact]
    public void ReplaysEachMenusOpenReadInOrderWithTheOtherRecords()
    {
        var seen = new List<object>();
        var cursors = new CursorEvents();
        var fixture = SessionFixture.Parse($"""
            # game build: {Build}
            0 menus open=1
            10 cursor mgr=0xa0 class=0x22045e8 cmd=0x1 result=1 top=0 row=0 flags=0x46
            20 menus open=0
            """);

        fixture.Replay(new MessageEvents(), cursors, pad => seen.Add(pad), menus: open => seen.Add(open));

        Assert.Equal([true, false], seen);
        Assert.Equal([0L, 10, 20], fixture.Records.Select(record => record.Milliseconds));
        Assert.Equal(new MenusRecord(20, Open: false), fixture.Records[^1]);
    }

    [Fact]
    public void ReplaysWindowReadsInOrderBetweenPadReads()
    {
        var seen = new List<object?>();

        SessionFixture.Parse($"""
            # game build: {Build}
            0 window current=0x13387b350
            5 pad pad=0 buttons=0x8000 returned=1
            10 window current=0x0
            20 window current=-
            """).Replay(new MessageEvents(), new CursorEvents(), pad => seen.Add(pad), window: current => seen.Add(current));

        Assert.Equal([0x13387b350L, new PadRead(0, 0x8000, 1), 0L, null], seen);
    }

    [Theory]
    [InlineData("# A made-up scenario\n0 request kind=0 label=MES_x_001")]
    [InlineData($"# game build: {Build}\n0 window current=Made-up")]
    [InlineData($"# game build: {Build}\n0 request kind=0 label=Made up words")]
    [InlineData($"# game build: {Build}\n0 lookup id=1 label=2")]
    [InlineData($"# game build: {Build}\n0 menus open=2")]
    public void RefusesAFixtureWithoutABuildOrWithARecordItDoesNotKnow(string text) =>
        Assert.Throws<FormatException>(() => SessionFixture.Parse(text));
}
