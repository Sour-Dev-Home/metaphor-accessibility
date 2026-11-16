namespace Metaphor.Core.Tests;

/// <summary>Game text kept on one log line. Made-up text only.</summary>
[Trait("Category", "Unit")]
public sealed class LogTextTests
{
    [Fact]
    public void EscapesControlCharactersQuotesAndBackslashes() =>
        Assert.Equal("a\\x22b\\x5cc\\x0ad\\x09e", LogText.Escaped("a\"b\\c\nd\te"));

    [Fact]
    public void LeavesOrdinaryTextAlone() => Assert.Equal("The bridge is out.", LogText.Escaped("The bridge is out."));

    [Fact]
    public void TreatsNullAsEmpty() => Assert.Equal("", LogText.Escaped(null!));
}
