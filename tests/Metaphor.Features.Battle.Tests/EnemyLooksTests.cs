namespace Metaphor.Features.Battle.Tests;

/// <summary>The enemy look table (#699): descriptions keyed by enemy name, read from JSON; made-up names and text.
/// </summary>
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

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("""{"other": {}}""")]
    [InlineData("""{"looks": []}""")]
    [InlineData("""{"looks": {"Test Beast": 3}}""")]
    [InlineData("""{"looks": {""")]
    public void AFileOfAnotherShapeIsRefused(string json) => Assert.Null(EnemyLooks.Parse(json));

    [Fact]
    public void TheShippedTableIsReadable() =>
        // A typo in the shipped file would fall back to the empty table and say nothing about it.
        Assert.NotSame(EnemyLooks.Empty, EnemyLooks.Embedded());
}
