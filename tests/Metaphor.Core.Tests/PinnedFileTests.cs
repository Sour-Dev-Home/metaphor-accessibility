using System.Security.Cryptography;

namespace Metaphor.Core.Tests;

/// <summary>The check that a native library on disk is the exact file we pinned, before the mod loads it (ADR 0005).</summary>
[Trait("Category", "Unit")]
public sealed class PinnedFileTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("metaphor-pinned-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string Write(params byte[] bytes)
    {
        var path = Path.Combine(folder, "lib.dll");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void AcceptsTheFileWithThePinnedHash()
    {
        var path = Write(1, 2, 3);
        var sha = Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1, 2, 3 }));

        Assert.Null(PinnedFile.WhyNot(path, sha));
    }

    [Fact]
    public void RefusesAFileWithAnyOtherHash()
    {
        var path = Write(1, 2, 4);
        var sha = Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1, 2, 3 }));

        Assert.Equal("lib.dll isn't the pinned file", PinnedFile.WhyNot(path, sha));
    }

    [Fact]
    public void RefusesAMissingFileWithoutThrowing() =>
        Assert.Equal("no lib.dll", PinnedFile.WhyNot(Path.Combine(folder, "lib.dll"), new string('0', 64)));
}
