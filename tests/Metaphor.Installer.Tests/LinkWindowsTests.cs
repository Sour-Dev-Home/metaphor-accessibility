using System.Diagnostics;

namespace Metaphor.Installer.Tests;

/// <summary>Links in the unpacked folder: refused by the check, never followed by the clean-up. Junctions, which any
/// user may make, by cmd's mklink /J, so these run on Windows alone (the <c>WindowsOnly</c> category).</summary>
[Trait("Category", "WindowsOnly")]
public sealed class LinkWindowsTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("metaphor-links-").FullName;
    private readonly string outside = Directory.CreateTempSubdirectory("metaphor-outside-").FullName;
    private readonly TestPayload payload = new();

    public void Dispose()
    {
        // .NET's recursive delete stops at a junction (access denied), so the launcher's own clean-up goes first.
        ScratchFolder.Delete(root);
        Directory.Delete(outside, recursive: true);
    }

    private static void Junction(string link, string target)
    {
        using var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        mklink.StandardOutput.ReadToEnd();
        mklink.WaitForExit();
        Assert.Equal(0, mklink.ExitCode);
    }

    [Fact]
    public void ALinkInTheFolderIsRefused()
    {
        Payload.Unpack(payload.Zip(), payload.ParsedManifest(), root);
        Junction(Path.Combine(root, "metaphor.accessibility", "linked"), outside);

        Assert.Throws<PayloadLinkException>(() => Payload.Verify(root, payload.ParsedManifest()));
    }

    [Fact]
    public void AFolderThatIsALinkIsNeverUnpackedInto()
    {
        Junction(Path.Combine(root, "metaphor.accessibility"), outside);

        Assert.Throws<PayloadLinkException>(() => Payload.Unpack(payload.Zip(), payload.ParsedManifest(), root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    [Fact]
    public void TheCleanUpRemovesALinkButNotWhatItPointsAt()
    {
        var kept = Path.Combine(outside, "kept.txt");
        File.WriteAllText(kept, "kept");
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        Junction(Path.Combine(root, "sub", "linked"), outside);

        Assert.True(ScratchFolder.Delete(root));

        Assert.False(Directory.Exists(root));
        Assert.Equal("kept", File.ReadAllText(kept));
    }

    [Fact]
    public void TheCleanUpOfAFolderSwappedForALinkRemovesOnlyTheLink()
    {
        var kept = Path.Combine(outside, "kept.txt");
        File.WriteAllText(kept, "kept");
        Directory.Delete(root);
        Junction(root, outside);

        Assert.True(ScratchFolder.Delete(root));

        Assert.False(Directory.Exists(root));
        Assert.Equal("kept", File.ReadAllText(kept));
    }
}
