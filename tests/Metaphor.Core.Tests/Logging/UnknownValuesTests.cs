using Metaphor.Core.Logging;

namespace Metaphor.Core.Tests.Logging;

/// <summary>Each value the mod has no mapping for is written once a session (#824), so a long session's log names every
/// gap without repeating it.</summary>
[Trait("Category", "Unit")]
public sealed class UnknownValuesTests
{
    private readonly List<string> lines = [];

    [Fact]
    public void EachValueIsWrittenOncePerKind()
    {
        var unknowns = new UnknownValues(lines.Add);

        unknowns.Report("icon", "PAD_ICON 80");
        unknowns.Report("icon", "PAD_ICON 80");
        unknowns.Report("icon", "PAD_ICON 81");
        unknowns.Report("screen class", "PAD_ICON 80");

        Assert.Equal(["icon PAD_ICON 80", "icon PAD_ICON 81", "screen class PAD_ICON 80"], lines);
    }

    [Fact]
    public void AValueThatCouldBeTextIsWrittenAsItsLength()
    {
        new UnknownValues(lines.Add).Report("tag", "The bridge is out!");

        Assert.Equal(["tag (18 chars)"], lines);
    }

    [Fact]
    public void PastItsLimitItSaysTheListIsFullOnce()
    {
        var unknowns = new UnknownValues(lines.Add, limit: 2);

        foreach (var value in new[] { "1", "2", "3", "4" })
        {
            unknowns.Report("map", value);
        }

        Assert.Equal(["map 1", "map 2", "list full at 2 values; later ones aren't listed"], lines);
    }

    [Fact]
    public void AFailingWriterNeverReachesTheCaller()
    {
        var unknowns = new UnknownValues(_ => throw new IOException("disk full"));

        unknowns.Report("icon", "PAD_ICON 80");
    }
}
