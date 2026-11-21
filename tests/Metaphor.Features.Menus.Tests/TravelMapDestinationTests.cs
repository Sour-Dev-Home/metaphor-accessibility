using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The travel map's destination panel (#545): each destination shown is said by its name and detail line,
/// cutting off the last, from the message references the panel is handed
/// (docs/findings/travel-map-destination-text-static.md). Made-up file text only.</summary>
[Trait("Category", "Unit")]
public sealed class TravelMapDestinationTests
{
    private const int Places = 34;
    private const int Lines = 66;

    private readonly RecordingAnnouncer announcer = new();
    private readonly MessageSerials serials = new([
        new SerialRange("message/Test_Places.msg", 700, 3, Entry: Places),
        new SerialRange("message/system/Test_Lines.msg", 900, 2, Entry: Lines),
    ]);
    private long now;
    private readonly TravelMapDestination destination;

    public TravelMapDestinationTests()
    {
        serials.Observe(new MessageFileCompiled("EN/message/Test_Places.msg", -1,
            Source("Test_Place", "Harbour", "Old Mill<COLOR 3>", "")));
        serials.Observe(new MessageFileCompiled("EN/message/system/Test_Lines.msg", -1,
            Source("Test_Line", "A town by the sea.", "Grain is ground here.")));
        destination = new TravelMapDestination(serials, announcer, new TextCleaner(TextCleanerRules.None), () => now);
    }

    private static string Source(string label, params string[] texts) =>
        string.Join("\n\n", texts.Select((text, i) => $"@{label}_{i:000}\n{{\n{text}\n}}"));

    private IEnumerable<string> Said() => announcer.Said.Select(said => said.Text);

    [Fact]
    public void EachDestinationIsSaidByNameThenDetailCuttingOffTheLast()
    {
        destination.Shown(new(Places, 0), new(Lines, 0));
        now += 100;
        destination.Shown(new(Places, 1), new(Lines, 1));

        Assert.Equal(["Harbour. A town by the sea.", "Old Mill. Grain is ground here."], Said());
        Assert.All(announcer.Said, said => Assert.True(said.Interrupt));
    }

    [Fact]
    public void AnUnknownPartIsLeftOutAndNothingIsSaidWithoutEither()
    {
        destination.Shown(new(Places, 0), new(Lines, 5));
        destination.Shown(null, new(Lines, 1));
        destination.Shown(new(Places, 2), null);
        destination.Shown(new(12, 0), new(13, 0));

        Assert.Equal(["Harbour", "Grain is ground here."], Said());
    }

    [Fact]
    public void TheSameDestinationRedrawnSoonIsNotSaidAgainButIsLater()
    {
        destination.Shown(new(Places, 0), new(Lines, 0));
        now += TravelMapDestination.RepeatMilliseconds - 1;
        destination.Shown(new(Places, 0), new(Lines, 0));
        now += TravelMapDestination.RepeatMilliseconds;
        destination.Shown(new(Places, 0), new(Lines, 0));

        Assert.Equal(["Harbour. A town by the sea.", "Harbour. A town by the sea."], Said());
    }

    [Fact]
    public void MovingAwayAndBackSaysTheDestinationAgainAtOnce()
    {
        destination.Shown(new(Places, 0), new(Lines, 0));
        destination.Shown(new(Places, 1), new(Lines, 1));
        destination.Shown(new(Places, 0), new(Lines, 0));

        Assert.Equal(3, Said().Count());
    }
}
