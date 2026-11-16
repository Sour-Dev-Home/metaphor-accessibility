using System.Text;

namespace Metaphor.Core.Tests;

/// <summary>#106's check of strings from the game against the hero name the maintainer typed. Made-up names only.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PlayerNameCheckTests
{
    [Theory]
    [InlineData("Ardent", PlayerNameMatch.Exact)]
    [InlineData("Hello, Ardent!", PlayerNameMatch.Contains)]
    [InlineData("Arden", PlayerNameMatch.No)]
    [InlineData("", PlayerNameMatch.No)]
    public void ComparesTheBytesWithTheTypedName(string text, PlayerNameMatch expected) =>
        Assert.Equal(expected, new PlayerNameCheck("Ardent").Compare(Encoding.UTF8.GetBytes(text)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithoutATypedNameEverythingIsUnset(string? typed) =>
        Assert.Equal(PlayerNameMatch.Unset, new PlayerNameCheck(typed).Compare("Ardent"u8));

    [Fact]
    public void ComparesANonAsciiNameAsUtf8ReadByteForByte()
    {
        var latin1 = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("Zoë"));

        var sighting = new PlayerNameCheck("Zoë").Sight(latin1, "heap");

        Assert.Equal(new PlayerNameSighting(4, PlayerNameMatch.Exact, "heap"), sighting);
    }

    [Fact]
    public void ASightingNeverHoldsTheText()
    {
        var sighting = new PlayerNameCheck("Ardent").Sight("Ardent", "module+0x10");

        Assert.Equal("bytes=6 match=exact at=module+0x10", sighting.ToString());
        Assert.DoesNotContain("Ardent", sighting.ToString(), StringComparison.Ordinal);
    }
}
