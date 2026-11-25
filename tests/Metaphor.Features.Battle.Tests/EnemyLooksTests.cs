namespace Metaphor.Features.Battle.Tests;

/// <summary>The enemy look table (#699): descriptions keyed by enemy name, and optionally by enemy id, read from JSON;
/// made-up names, ids and text.</summary>
[Trait("Category", "Unit")]
public sealed class EnemyLooksTests
{
    [Fact]
    public void ANameFindsItsDescriptionIgnoringCaseAndBlanksCountAsNone()
    {
        var looks = EnemyLooks.Parse("""
            {"about": "a note", "looks": {"Test Beast": " A round grey shape. ", "Blank One": "  "}}
            """);

        Assert.NotNull(looks);
        Assert.Equal(1, looks.Count);
        Assert.Equal("A round grey shape.", looks.Of("test beast"));
        Assert.Null(looks.Of("Blank One"));
    }

    [Fact]
    public void TheLineSaysTheNameThenItsLookOrThatNoneIsWrittenYet()
    {
        var looks = EnemyLooks.Parse("""{"looks": {"Test Beast": "A round grey shape."}}""")!;

        Assert.Equal("Test Beast. A round grey shape.", looks.Line("First enemy", "Test Beast"));
        Assert.Equal("Other Beast, no description yet", looks.Line("First enemy", "Other Beast"));
        Assert.Equal("Second enemy, name not known yet", looks.Line("Second enemy", null));
    }

    [Fact]
    public void AnIdEntryWinsOverTheNameEntryForThatIdOnly()
    {
        // The maintainer's decision of 2026-11-24: an optional entry by enemy id, for a variant that looks different.
        var looks = EnemyLooks.Parse("""
            {"looks": {"Test Beast": "A round grey shape."}, "ids": {"412": " A round red shape. ", "9": " "}}
            """)!;

        Assert.Equal(1, looks.IdCount);
        Assert.Equal("Test Beast. A round red shape.", looks.Line("First enemy", "Test Beast", 412));
        Assert.Equal("Test Beast. A round grey shape.", looks.Line("First enemy", "Test Beast", 413));
        Assert.Equal("Test Beast. A round grey shape.", looks.Line("First enemy", "Test Beast", 9));
        Assert.Equal("Second enemy. A round red shape.", looks.Line("Second enemy", null, 412));
        Assert.Equal("Second enemy, name not known yet", looks.Line("Second enemy", null, 413));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("""{"other": {}}""")]
    [InlineData("""{"looks": []}""")]
    [InlineData("""{"looks": {"Test Beast": 3}}""")]
    [InlineData("""{"looks": {""")]
    [InlineData("""{"looks": {}, "ids": []}""")]
    [InlineData("""{"looks": {}, "ids": {"412": 3}}""")]
    [InlineData("""{"looks": {}, "ids": {"Test Beast": "A shape."}}""")]
    [InlineData("""{"looks": {}, "ids": {"-1": "A shape."}}""")]
    public void AFileOfAnotherShapeIsRefused(string json) => Assert.Null(EnemyLooks.Parse(json));

    [Fact]
    public void TheShippedTableIsReadable() =>
        // A typo in the shipped file would fall back to the empty table and say nothing about it.
        Assert.NotSame(EnemyLooks.Empty, EnemyLooks.Embedded());
}
