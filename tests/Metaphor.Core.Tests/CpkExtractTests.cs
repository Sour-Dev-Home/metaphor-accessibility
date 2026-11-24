using System.Diagnostics;
using System.Text;
using Metaphor.CpkExtract;

namespace Metaphor.Core.Tests;

/// <summary>The dev-only CPK extract tool (#895): listing, extracting, and refusing any output outside the repository's
/// <c>extracted/</c> folder. A made-up repository, game folder and archive in a temp folder; no game data.</summary>
[Trait("Category", "Unit")]
public sealed class CpkExtractTests : IDisposable
{
    private readonly string temp = Path.Combine(Path.GetTempPath(), "cpk-extract-" + Guid.NewGuid().ToString("N"));
    private readonly string repo;
    private readonly string game;
    private readonly byte[] archiveBytes;

    public CpkExtractTests()
    {
        repo = Path.Combine(temp, "repo");
        game = Path.Combine(temp, "game");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        File.WriteAllText(Path.Combine(repo, "Metaphor.sln"), "");
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        Directory.CreateDirectory(game);
        using var archive = CpkArchiveTests.Archive(
            ("COMMON/data", "one.bin", Encoding.ASCII.GetBytes("first"), 5),
            ("COMMON/data", "two.txt", Encoding.ASCII.GetBytes("second"), 6),
            ("", "top.bin", Encoding.ASCII.GetBytes("root"), 4),
            ("COMMON/packed", "zip.bin", Encoding.ASCII.GetBytes("xx"), 99));
        archiveBytes = archive.ToArray();
        File.WriteAllBytes(Path.Combine(game, "base.cpk"), archiveBytes);
    }

    public void Dispose()
    {
        try
        {
            // The link first and alone, so deleting the folder never follows it.
            var link = Path.Combine(repo, "extracted", "link");
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }
            Directory.Delete(temp, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A temp folder left behind is harmless.
        }
    }

    private string? Environment(string name) => name == "METAPHOR_GAME_EXE" ? Path.Combine(game, "METAPHOR.exe") : null;

    private (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = CpkExtractCommand.Run(args, Environment, Path.Combine(repo, "docs"), output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static string Slash(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void ListsEveryFileWithItsSize()
    {
        var (code, output, _) = Run("list", "base.cpk");
        Assert.Equal(0, code);
        Assert.Contains("5\tCOMMON/data/one.bin\n", Slash(output));
        Assert.Contains("6\tCOMMON/data/two.txt\n", Slash(output));
        Assert.Contains("4\ttop.bin\n", Slash(output));
        Assert.Contains("COMMON/packed/zip.bin\t(compressed", Slash(output));
    }

    [Fact]
    public void ListFilterKeepsPathsContainingItIgnoringCase()
    {
        var (code, output, _) = Run("list", "base.cpk", "DATA/ONE");
        Assert.Equal(0, code);
        Assert.Equal("5\tCOMMON/data/one.bin\n", Slash(output));
    }

    [Fact]
    public void ExtractWritesTheFilesUnderTheOutputFolderKeepingTheirPaths()
    {
        var (code, output, error) = Run("extract", "base.cpk", "COMMON/data/one.bin", "top.bin", "--out", Path.Combine("..", "extracted", "t"));
        Assert.Equal(2, code);
        Assert.Contains("'..'", error);
        Assert.Equal("", output);

        (code, output, error) = Run("extract", "base.cpk", "COMMON/data/one.bin", "top.bin", "--out", Path.Combine(repo, "extracted", "t"));
        Assert.True(code == 0, error);
        Assert.Equal("first", File.ReadAllText(Path.Combine(repo, "extracted", "t", "COMMON", "data", "one.bin")));
        Assert.Equal("root", File.ReadAllText(Path.Combine(repo, "extracted", "t", "top.bin")));
        Assert.Contains("5\tCOMMON/data/one.bin", output);
    }

    [Fact]
    public void ExtractLeavesTheArchiveUntouched()
    {
        Run("extract", "base.cpk", "top.bin", "--out", Path.Combine(repo, "extracted"));
        Assert.Equal(archiveBytes, File.ReadAllBytes(Path.Combine(game, "base.cpk")));
    }

    [Theory]
    [InlineData("docs")]
    [InlineData("out")]
    [InlineData("extracted-not")]
    [InlineData("../extracted")]
    [InlineData("extracted/../docs")]
    [InlineData("extracted\\..\\docs")]
    public void ExtractRefusesAnOutputOutsideTheExtractedFolder(string relativeToRepo)
    {
        var requested = Path.Combine(repo, relativeToRepo);
        var (code, _, error) = Run("extract", "base.cpk", "top.bin", "--out", requested);
        Assert.Equal(2, code);
        Assert.StartsWith("refused:", error);
        Assert.False(File.Exists(Path.Combine(repo, "docs", "top.bin")));
        Assert.False(Directory.Exists(Path.Combine(repo, "out")));
        Assert.False(Directory.Exists(Path.Combine(repo, "extracted-not")));
    }

    [Fact]
    public void ExtractRefusesTheRepositoryRootAnotherDriveFolderAndARelativeEscape()
    {
        foreach (var requested in new[] { repo, Path.Combine(temp, "elsewhere"), Path.GetTempPath(), "..", "../..", "." })
        {
            var (code, _, error) = Run("extract", "base.cpk", "top.bin", "--out", requested);
            Assert.True(code == 2, requested);
            Assert.StartsWith("refused:", error);
        }
        Assert.False(File.Exists(Path.Combine(repo, "top.bin")));
        Assert.False(File.Exists(Path.Combine(temp, "elsewhere", "top.bin")));
    }

    [Fact]
    public void ExtractRefusesALinkInsideExtractedThatLeadsOut()
    {
        var elsewhere = Path.Combine(temp, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        Directory.CreateDirectory(Path.Combine(repo, "extracted"));
        var link = Path.Combine(repo, "extracted", "link");
        if (!MakeLink(link, elsewhere))
        {
            return; // No permission to make a symlink or junction on this machine; nothing to test.
        }
        var (code, _, error) = Run("extract", "base.cpk", "top.bin", "--out", Path.Combine(link, "deeper"));
        Assert.Equal(2, code);
        Assert.Contains("link", error);
        Assert.False(File.Exists(Path.Combine(elsewhere, "deeper", "top.bin")));
        Assert.False(File.Exists(Path.Combine(elsewhere, "top.bin")));
    }

    [Theory]
    [InlineData("../evil.bin")]
    [InlineData("a/../../evil.bin")]
    [InlineData("..\\evil.bin")]
    [InlineData("/evil.bin")]
    [InlineData("C:/evil.bin")]
    [InlineData("a//b.bin")]
    [InlineData("evil.bin:stream")]
    public void ExtractRefusesAnArchivePathThatIsNotPlainRelative(string path)
    {
        var (code, _, error) = Run("extract", "base.cpk", path, "--out", Path.Combine(repo, "extracted"));
        Assert.Equal(2, code);
        Assert.StartsWith("refused:", error);
        Assert.False(File.Exists(Path.Combine(repo, "evil.bin")));
    }

    [Fact]
    public void ExtractReportsAMissingAndACompressedFileAndExtractsTheRest()
    {
        var (code, output, error) = Run("extract", "base.cpk", "nope/none.bin", "COMMON/packed/zip.bin", "top.bin", "--out", Path.Combine(repo, "extracted"));
        Assert.Equal(1, code);
        Assert.Contains("nope/none.bin: not in the archive", error);
        Assert.Contains("COMMON/packed/zip.bin: compressed", error);
        Assert.Contains("top.bin", output);
        Assert.False(File.Exists(Path.Combine(repo, "extracted", "nope", "none.bin")));
    }

    [Theory]
    [InlineData("C:/Games/base.cpk")]
    [InlineData("../base.cpk")]
    [InlineData("base.bin")]
    public void TheArchiveIsNamedByFileNameOnly(string name)
    {
        var (code, _, error) = Run("list", name);
        Assert.Equal(2, code);
        Assert.StartsWith("refused:", error);
    }

    [Fact]
    public void NeedsTheGameExeEnvironmentVariable()
    {
        var error = new StringWriter();
        var code = CpkExtractCommand.Run(["list", "base.cpk"], _ => null, repo, new StringWriter(), error);
        Assert.Equal(2, code);
        Assert.Contains("METAPHOR_GAME_EXE", error.ToString());
    }

    [Fact]
    public void FailsOutsideTheRepository()
    {
        var error = new StringWriter();
        var code = CpkExtractCommand.Run(["extract", "base.cpk", "top.bin", "--out", "extracted"], Environment, temp, new StringWriter(), error);
        Assert.Equal(2, code);
        Assert.Contains("inside the repository", error.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("list")]
    [InlineData("extract base.cpk top.bin")]
    [InlineData("extract base.cpk --out extracted")]
    [InlineData("frobnicate base.cpk")]
    public void UsageErrorsExitTwo(string line)
    {
        var (code, _, error) = Run(line.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(2, code);
        Assert.Contains("usage:", error);
    }

    // A symlink, or on Windows without the privilege a junction; false when neither can be made here.
    private static bool MakeLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }
        }
        using var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        mklink?.WaitForExit();
        return Directory.Exists(link);
    }
}
