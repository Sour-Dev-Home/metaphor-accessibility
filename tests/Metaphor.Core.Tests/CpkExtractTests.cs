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

    // #899, the security review's hardening of #895.

    [Theory]
    [InlineData("NUL")]
    [InlineData("a/CON.txt")]
    [InlineData("com1")]
    [InlineData("data/LPT9.bin")]
    [InlineData("aux .bin")]
    [InlineData("a/b?.bin")]
    [InlineData("a/b*.bin")]
    [InlineData("x<y.bin")]
    [InlineData("x>y.bin")]
    [InlineData("x|y.bin")]
    [InlineData("x\"y.bin")]
    [InlineData("x\ty.bin")]
    public void ExtractRefusesADeviceNameOrACharacterWindowsCannotName(string path)
    {
        var (code, output, error) = Run("extract", "base.cpk", path, "--out", Path.Combine(repo, "extracted"));
        Assert.Equal(2, code);
        Assert.StartsWith("refused:", error);
        Assert.Equal("", output);
    }

    [Theory]
    [InlineData("CONSOLE.bin")]
    [InlineData("a/null.txt")]
    [InlineData("COM10.bin")]
    public void ANameThatOnlyStartsLikeADeviceIsAllowed(string path) =>
        Assert.Equal(path, OutputGuard.ArchivePath(path));

    [Fact]
    public void AnUnexpectedErrorSaysItsKindOnlyNeverAStackTraceOrPath()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = CpkExtractCommand.Run(["list", "base.cpk"], _ => "bad\0path", repo, output, error);
        Assert.Equal(1, code);
        Assert.Equal("could not read or write a file (ArgumentException)", error.ToString().Trim());
        Assert.DoesNotContain(" at ", error.ToString());
    }

    [Fact]
    public void ExtractRefusesToOverwriteAFileUnlessForced()
    {
        var target = Path.Combine(repo, "extracted", "top.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "mine");

        var (code, output, error) = Run("extract", "base.cpk", "top.bin", "--out", Path.Combine(repo, "extracted"));
        Assert.Equal(2, code);
        Assert.StartsWith("refused:", error);
        Assert.Contains("--force", error);
        Assert.Equal("", output);
        Assert.Equal("mine", File.ReadAllText(target));

        (code, _, error) = Run("extract", "base.cpk", "top.bin", "--out", Path.Combine(repo, "extracted"), "--force");
        Assert.True(code == 0, error);
        Assert.Equal("root", File.ReadAllText(target));
    }

    [Fact]
    public void AFileThatAppearsAfterTheChecksIsNotWrittenOver()
    {
        // The write opens the file only if it isn't there, so nothing that appeared after the checks (a file, or a link
        // to one elsewhere) is written through.
        var folder = Path.Combine(repo, "extracted");
        var target = Path.Combine(folder, "late.bin");
        Directory.CreateDirectory(folder);
        File.WriteAllText(target, "planted");

        Assert.ThrowsAny<IOException>(() => OutputGuard.WriteNew(folder, target, [1, 2, 3], force: false));
        Assert.Equal("planted", File.ReadAllText(target));

        OutputGuard.WriteNew(folder, target, [1, 2, 3], force: true);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(target));
    }

    [Fact]
    public void AForcedWriteNeverFollowsALinkAtTheFile()
    {
        var folder = Path.Combine(repo, "extracted");
        Directory.CreateDirectory(folder);
        var victim = Path.Combine(temp, "victim.bin");
        File.WriteAllText(victim, "safe");
        var link = Path.Combine(folder, "link.bin");
        try
        {
            File.CreateSymbolicLink(link, victim);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return; // No permission to make a file symlink on this machine; nothing to test.
        }

        Assert.Throws<RefusedPathException>(() => OutputGuard.WriteNew(folder, link, [1], force: true));
        Assert.Equal("safe", File.ReadAllText(victim));
    }

    [Fact]
    public void ListStripsControlCharactersFromNames()
    {
        using var archive = CpkArchiveTests.Archive(("dir\u001b[2J", "a\rb\u0007.bin", Encoding.ASCII.GetBytes("x"), 1));
        File.WriteAllBytes(Path.Combine(game, "odd.cpk"), archive.ToArray());

        var (code, output, _) = Run("list", "odd.cpk");

        Assert.Equal(0, code);
        Assert.Equal("1\tdir?[2J/a?b?.bin\n", Slash(output));
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
