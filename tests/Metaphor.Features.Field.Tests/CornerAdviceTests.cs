using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Field.Tests;

/// <summary>The corner advice (#587): a report line the game opens, by its label or its label's hash, said with its
/// speaker's name from the compiled report file, queued, in free roam. A made-up report file in the shape the
/// 2026-11-23 logs show (docs/findings/corner-advice-report-message-open-static.md).</summary>
[Trait("Category", "Unit")]
public sealed class CornerAdviceTests
{
    private const string Report = """
        @SUP01_Field005_0001
        {
        <NS>Gallica<NE><VOICE "v_co_test"><REPORT 1 1 0 0 0>Made-up advice about appraisal.
        }
        @SUP01_Field005_0002
        {
        <REPORT 1 0 0 0 0>A line with no speaker.
        }
        """;

    private readonly RecordingAnnouncer announcer = new();
    private readonly RoamField field = new();
    private readonly MessageLibrary library = new();
    private readonly List<string> log = [];
    private readonly CornerAdvice advice;
    private long now = 1000;

    public CornerAdviceTests()
    {
        library.Compiled(Report);
        advice = new CornerAdvice(
            library, new TextCleaner(TextCleanerRules.None), announcer, new FreeRoamGate(field), () => now, log.Add);
    }

    private sealed class RoamField : IFieldReads
    {
        public bool Roaming { get; set; } = true;

        public bool InFreeRoam() => Roaming;

        public FieldView? Read() => null;

        public int? MapId() => null;

        public FieldPose? ReadPose() => null;
    }

    private string[] Said() => [.. announcer.Said.Select(said => said.Text)];

    [Fact]
    public void ALineOpenedByItsLabelHashIsSaidWithItsSpeakerAndQueued()
    {
        advice.Opened(null, MessageLibrary.LabelHash("SUP01_Field005_0001"));

        Assert.Equal(["Gallica: Made-up advice about appraisal."], Said());
        Assert.False(announcer.Said[0].Interrupt);
        Assert.Contains(log, line => line.Contains("SUP01_Field005_0001", StringComparison.Ordinal));
    }

    [Fact]
    public void ALineOpenedByItsLabelIsSaid()
    {
        advice.Opened("SUP01_Field005_0002", null);

        Assert.Equal(["A line with no speaker."], Said());
    }

    [Fact]
    public void AnUnknownKeySaysNothingAndLogsTheMiss()
    {
        advice.Opened(null, MessageLibrary.LabelHash("NO_SUCH_LABEL"));
        advice.Opened("NO_SUCH_LABEL", null);
        advice.Opened(null, null);

        Assert.Empty(announcer.Said);
        Assert.Contains(log, line => line.Contains(MessageLibrary.LabelHash("NO_SUCH_LABEL"), StringComparison.Ordinal));
        Assert.Contains(log, line => line.Contains("NO_SUCH_LABEL", StringComparison.Ordinal));
    }

    [Fact]
    public void ALineOpenedOutsideFreeRoamWaitsForItAndIsSaidOnce()
    {
        field.Roaming = false;
        advice.Opened("SUP01_Field005_0002", null);
        advice.Pad(0, 0, made: true);
        Assert.Empty(announcer.Said);

        field.Roaming = true;
        now += 500;
        Assert.Equal(0x30, advice.Pad(0, 0x30, made: true));
        advice.Keys([]);

        Assert.Equal(["A line with no speaker."], Said());
    }

    [Fact]
    public void ALineHeldTooLongIsDropped()
    {
        field.Roaming = false;
        advice.Opened("SUP01_Field005_0002", null);

        field.Roaming = true;
        now += CornerAdvice.HoldMilliseconds + 1;
        advice.Keys([]);

        Assert.Empty(announcer.Said);
    }
}
