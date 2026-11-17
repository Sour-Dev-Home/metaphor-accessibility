using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Messages;

/// <summary>The silent buffer of the last dialogue lines (#126), for the repeat key (#58) and the backlog fallback
/// (#114). Made-up lines only.</summary>
[Trait("Category", "Unit")]
public sealed class DialogueHistoryTests
{
    [Fact]
    public void ItStartsEmpty()
    {
        var history = new DialogueHistory();

        Assert.Null(history.Latest);
        Assert.Empty(history.Lines());
    }

    [Fact]
    public void LinesComeBackOldestFirstAndLatestIsTheNewest()
    {
        var history = new DialogueHistory();
        history.Add("Ada: One.");
        history.Add("Two.");

        Assert.Equal(["Ada: One.", "Two."], history.Lines());
        Assert.Equal("Two.", history.Latest);
    }

    [Fact]
    public void ItKeepsTheLastTwentyLinesByDefault()
    {
        var history = new DialogueHistory();
        for (var i = 1; i <= 25; i++)
        {
            history.Add($"line {i}");
        }

        var lines = history.Lines();
        Assert.Equal(20, lines.Count);
        Assert.Equal("line 6", lines[0]);
        Assert.Equal("line 25", history.Latest);
    }

    [Fact]
    public void ACustomCapacityIsHonoured()
    {
        var history = new DialogueHistory(2);
        history.Add("a");
        history.Add("b");
        history.Add("c");

        Assert.Equal(["b", "c"], history.Lines());
    }

    [Fact]
    public void ACapacityBelowOneIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DialogueHistory(0));

    [Fact]
    public void ASnapshotIsNotChangedByLaterLines()
    {
        var history = new DialogueHistory(3);
        history.Add("a");
        var snapshot = history.Lines();
        history.Add("b");

        Assert.Equal(["a"], snapshot);
    }

    [Fact]
    public void LinesAddedFromManyThreadsAreKeptWithinCapacity()
    {
        var history = new DialogueHistory(20);
        Parallel.For(0, 1000, i =>
        {
            history.Add($"line {i}");
            _ = history.Lines();
            _ = history.Latest;
        });

        Assert.Equal(20, history.Lines().Count);
        Assert.Equal(20, history.Lines().Distinct().Count());
    }
}
