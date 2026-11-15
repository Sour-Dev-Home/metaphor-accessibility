using System.Security.Cryptography;

namespace Metaphor.Core.Tests;

/// <summary>The offline signature checker, run against a stand-in "game exe": a file we write with known byte
/// patterns (seam 3 in issue #1).</summary>
[Trait("Category", "Unit")]
public sealed class SignatureCheckTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("metaphor-sigcheck-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string StandInExe(params byte[] bytes)
    {
        var path = Path.Combine(folder, "game.exe");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static HookSignature Signature(string name, string pattern) => new(name, pattern, [], "test");

    private static (int ExitCode, string Output) Run(string exe, IReadOnlyList<GameBuild> knownBuilds, params HookSignature[] signatures)
    {
        using var output = new StringWriter();
        var exitCode = SignatureCheck.Run(exe, signatures, knownBuilds, output);
        return (exitCode, output.ToString());
    }

    private static (int ExitCode, string Output) Run(string exe, params HookSignature[] signatures) => Run(exe, [], signatures);

    [Fact]
    public void FailsWhenASignatureMatchesNowhere()
    {
        var exe = StandInExe(0x10, 0x20, 0x30, 0x40);

        var (exitCode, output) = Run(exe, Signature("draw-text", "20 40"));

        Assert.Equal(1, exitCode);
        Assert.Contains("draw-text: no match", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PassesWhenEachSignatureMatchesExactlyOnce()
    {
        var exe = StandInExe(0x10, 0x20, 0x30, 0x40, 0x50);

        var (exitCode, output) = Run(exe, Signature("draw-text", "30 40"), Signature("play-sound", "10 20"));

        Assert.Equal(0, exitCode);
        Assert.Contains("ok   draw-text: 1 match at 0x2", output, StringComparison.Ordinal);
        Assert.Contains("ok   play-sound: 1 match at 0x0", output, StringComparison.Ordinal);
    }

    [Fact]
    public void FailsWhenASignatureMatchesMoreThanOnce()
    {
        var exe = StandInExe(0xAA, 0xBB, 0x00, 0xAA, 0xBB, 0x00, 0xAA, 0xBB);

        var (exitCode, output) = Run(exe, Signature("draw-text", "AA BB"));

        Assert.Equal(1, exitCode);
        Assert.Contains("FAIL draw-text: 3 matches, at 0x0, 0x3, 0x6", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AWildcardMatchesAnyByte()
    {
        var exe = StandInExe(0x48, 0x8B, 0x05, 0x10, 0x00, 0x48, 0x8B, 0x0D, 0x20);

        var (exitCode, output) = Run(exe, Signature("draw-text", "48 8B ?? 20"));

        Assert.Equal(0, exitCode);
        Assert.Contains("ok   draw-text: 1 match at 0x5", output, StringComparison.Ordinal);
    }

    [Fact]
    public void APatternMayStartWithAWildcard()
    {
        var exe = StandInExe(0x20, 0x10, 0x20);

        var (exitCode, output) = Run(exe, Signature("draw-text", "?? 20"));

        Assert.Equal(0, exitCode);
        Assert.Contains("ok   draw-text: 1 match at 0x1", output, StringComparison.Ordinal);
    }

    [Fact]
    public void NamesTheKnownGameBuildItChecked()
    {
        var exe = StandInExe(0x10, 0x20);
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(exe)));

        var (_, output) = Run(exe, [new GameBuild(sha, 18330018, null, "test")], Signature("draw-text", "10"));

        Assert.StartsWith($"game build {sha} (Steam build 18330018)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SaysWhenTheGameBuildIsNotKnown()
    {
        var exe = StandInExe(0x10, 0x20);

        var (exitCode, output) = Run(exe, Signature("draw-text", "10"));

        Assert.Equal(0, exitCode);
        Assert.Matches("^game build [0-9a-f]{64} \\(not a known build\\)", output);
    }

    [Fact]
    public void SaysWhenASignatureMatchesOnABuildItDoesNotName()
    {
        var exe = StandInExe(0x10, 0x20);
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(exe)));
        var listed = new HookSignature("listed", "10", [sha], "test");
        var unlisted = new HookSignature("unlisted", "20", [new string('1', 64)], "test");

        var (exitCode, output) = Run(exe, listed, unlisted);

        Assert.Equal(0, exitCode);
        Assert.Contains($"ok   listed: 1 match at 0x0{Environment.NewLine}", output, StringComparison.Ordinal);
        Assert.Contains("ok   unlisted: 1 match at 0x1, on a build it doesn't name yet", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ChecksProbesLikeSignaturesWithoutABuildNote()
    {
        var exe = StandInExe(0x10, 0x20, 0x20);
        using var output = new StringWriter();

        var exitCode = SignatureCheck.RunProbes(
            exe, [new SignatureProbe("once", "10", "s"), new SignatureProbe("twice", "20", "s")], [], output);

        Assert.Equal(1, exitCode);
        Assert.Contains($"ok   once: 1 match at 0x0{Environment.NewLine}", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("FAIL twice: 2 matches, at 0x1, 0x2", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void GivesEachMatchsAddressInMemoryWhenTheFileIsAPortableExecutable()
    {
        // A real PE file: this build's Metaphor.Core.dll. Its CLI header records where the metadata, which starts with
        // "BSJB", lands in memory, independently of the section table the check reads.
        var dll = typeof(SignatureCheck).Assembly.Location;
        using (var stream = File.OpenRead(dll))
        {
            var metadataRva = new System.Reflection.PortableExecutable.PEHeaders(stream).CorHeader!.MetadataDirectory.RelativeVirtualAddress;
            using var output = new StringWriter();

            SignatureCheck.RunProbes(dll, [new SignatureProbe("metadata", "42 53 4A 42 01 00 01 00", "s")], [], output);

            Assert.Contains($"(rva 0x{metadataRva:x})", output.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PassesAndSaysSoWhenThereAreNoSignatures()
    {
        var exe = StandInExe(0x10);

        var (exitCode, output) = Run(exe);

        Assert.Equal(0, exitCode);
        Assert.Contains("no signatures to check", output, StringComparison.Ordinal);
    }

    [Fact]
    public void EndsWithHowManySignaturesMatchedExactlyOnce()
    {
        var exe = StandInExe(0x10, 0x20, 0x10);

        var (_, output) = Run(exe, Signature("a", "20"), Signature("b", "10"), Signature("c", "30"));

        Assert.EndsWith($"1 of 3 signatures matched exactly once{Environment.NewLine}", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ListsOnlyTheFirstFewOffsetsOfManyMatches()
    {
        var exe = StandInExe([.. Enumerable.Repeat<byte>(0xAA, 12)]);

        var (_, output) = Run(exe, Signature("draw-text", "AA"));

        Assert.Contains("FAIL draw-text: 12 matches, at 0x0, 0x1, 0x2, 0x3, 0x4 and 7 more", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CountsMatchesThatOverlap()
    {
        var exe = StandInExe(0xAA, 0xAA, 0xAA);

        var (exitCode, output) = Run(exe, Signature("draw-text", "AA AA"));

        Assert.Equal(1, exitCode);
        Assert.Contains("FAIL draw-text: 2 matches, at 0x0, 0x1", output, StringComparison.Ordinal);
    }
}
