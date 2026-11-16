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

/// <summary>Where a development run's typed name comes from: a file in the mod's folder, outside the repository.</summary>
[Trait("Category", "Unit")]
public sealed class PlayerNameFileTests : IDisposable
{
    private readonly string modFolder = Directory.CreateTempSubdirectory("metaphor-name-").FullName;

    public void Dispose() => Directory.Delete(modFolder, recursive: true);

    private void WriteNameFile(string contents) =>
        File.WriteAllText(Path.Combine(modFolder, PlayerNameCheck.FileName), contents, new UTF8Encoding(true));

    [Fact]
    public void ReadsTheFilesFirstLineTrimmed()
    {
        WriteNameFile("  Ardent \r\nsecond line\r\n");

        var check = PlayerNameCheck.FromModFolder(modFolder, fallback: "Other");

        Assert.Equal(PlayerNameMatch.Exact, check.Compare("Ardent"u8));
    }

    [Fact]
    public void WithoutTheFileUsesTheFallback() =>
        Assert.Equal(PlayerNameMatch.Exact, PlayerNameCheck.FromModFolder(modFolder, fallback: "Other").Compare("Other"u8));

    [Fact]
    public void AnEmptyFileUsesTheFallback()
    {
        WriteNameFile(" \n");

        Assert.Equal(PlayerNameMatch.Unset, PlayerNameCheck.FromModFolder(modFolder, fallback: null).Compare("Ardent"u8));
    }

    [Fact]
    public void ReadsOnlyTheStartOfAHugeOneLineFile()
    {
        WriteNameFile("Ardent" + new string(' ', 300) + "tail");

        Assert.Equal(PlayerNameMatch.Exact, PlayerNameCheck.FromModFolder(modFolder, fallback: null).Compare("Ardent"u8));
    }

    [Fact]
    public void AMissingFolderIsUnset() =>
        Assert.Equal(
            PlayerNameMatch.Unset,
            PlayerNameCheck.FromModFolder(Path.Combine(modFolder, "missing"), fallback: null).Compare("Ardent"u8));
}
