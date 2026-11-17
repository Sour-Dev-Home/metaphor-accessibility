using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Messages;

/// <summary>The #103 check of whether each requested line starts a voice cue: the cues set within the window after a
/// line, reported when the next line comes. Made-up labels, cue numbers and handles only.</summary>
[Trait("Category", "Unit")]
public sealed class VoiceCueCheckTests
{
    private readonly FakeMessages messages = new();
    private readonly List<VoiceCueChecked> reports = [];
    private long now;

    private VoiceCueCheck Check() => new(messages, () => now, reports.Add, windowMilliseconds: 1000);

    [Fact]
    public void ATaggedLineReportsTheCueSetAfterItWhenTheNextLineComes()
    {
        messages.Add("L1", "<VOICE \"v1\">Hello.");
        var check = Check();

        now = 100;
        check.Observe(new LineRequested(0, "L1"));
        now = 101;
        check.CueSet(42, 0x10);
        Assert.Empty(reports);
        check.Observe(new LineRequested(0, "L2"));

        var report = Assert.Single(reports);
        Assert.Equal("L1", report.Label);
        Assert.True(report.Tagged);
        Assert.Equal([new VoiceCueHeard(42, 0x10, null, 1)], report.Cues);
    }

    [Fact]
    public void AnUntaggedLineWithNoCueReportsNone()
    {
        messages.Add("L1", "Hello.");
        var check = Check();

        check.Observe(new LineRequested(0, "L1"));
        check.Flush();

        var report = Assert.Single(reports);
        Assert.False(report.Tagged);
        Assert.Empty(report.Cues);
        Assert.Equal("kind=0 label=\"L1\" tagged=no cues=none", report.ToString());
    }

    [Fact]
    public void ALineWhoseMessageIsUnknownReportsTaggedAsUnknown()
    {
        var check = Check();

        check.Observe(new LineRequested(1, "missing"));
        check.Flush();

        Assert.Null(Assert.Single(reports).Tagged);
        Assert.Contains("tagged=unknown", reports[0].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ACuePastTheWindowClosesTheCheckWithoutIt()
    {
        messages.Add("L1", "<VOICE \"v1\">Hello.");
        var check = Check();

        check.Observe(new LineRequested(0, "L1"));
        now = 1001;
        check.CueSet(42, 0x10);

        Assert.Empty(Assert.Single(reports).Cues);
        check.Flush();
        Assert.Single(reports);
    }

    [Fact]
    public void ACueCarriesItsPlayersCategoryAndPrintsWithIt()
    {
        messages.Add("L1", "<VOICE \"v1\">Hello.");
        var check = Check();
        check.PlayerCategory(0x10, 3);

        check.Observe(new LineRequested(0, "L1"));
        now = 5;
        check.CueSet(42, 0x10);
        check.CueSet(7, 0x20);
        check.Flush();

        var report = Assert.Single(reports);
        Assert.Equal(3u, report.Cues[0].Category);
        Assert.Equal("kind=0 label=\"L1\" tagged=yes cues=42@5ms/player=0x10/cat=3,7@5ms/player=0x20/cat=?",
            report.ToString());
    }

    [Fact]
    public void ACueWithNoLineWaitingReportsNothing()
    {
        var check = Check();

        check.CueSet(42, 0x10);
        check.Flush();

        Assert.Empty(reports);
    }

    [Fact]
    public void ItKeepsAtMostTheFirstEightCuesOfALine()
    {
        messages.Add("L1", "Hello.");
        var check = Check();

        check.Observe(new LineRequested(0, "L1"));
        for (var i = 0; i < 12; i++)
        {
            check.CueSet(i, 0x10);
        }
        check.Flush();

        Assert.Equal(VoiceCueCheck.MaxCues, Assert.Single(reports).Cues.Count);
    }

    private sealed class FakeMessages : IMessageLookup
    {
        private readonly Dictionary<string, GameMessage> byLabel = [];

        public void Add(string label, string text) => byLabel[label] = new GameMessage(label, null, text);

        public GameMessage? Find(string label) => byLabel.GetValueOrDefault(label);
    }
}
