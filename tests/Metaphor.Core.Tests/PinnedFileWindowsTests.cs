using System.Security.Cryptography;

namespace Metaphor.Core.Tests;

/// <summary>
/// The part of the pinned-file check that relies on Windows file sharing: an open <c>FileShare.Read</c> stream blocks
/// writers. On Linux the write doesn't throw, so these run only where the mod runs; <c>scripts/check.sh</c> adds the
/// <c>WindowsOnly</c> category on Windows alone.
/// </summary>
[Trait("Category", "WindowsOnly")]
public sealed class PinnedFileWindowsTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("metaphor-pinned-win-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void ChecksAnOpenStreamSoTheCallerCanHoldTheFileUntilItLoadsIt()
    {
        var path = Path.Combine(folder, "lib.dll");
        File.WriteAllBytes(path, [1, 2, 3]);
        var sha = Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1, 2, 3 }));
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.Null(PinnedFile.WhyNot(held, "lib.dll", sha));
        Assert.Throws<IOException>(() => File.WriteAllBytes(path, [9]));
    }
}
// timing probe
