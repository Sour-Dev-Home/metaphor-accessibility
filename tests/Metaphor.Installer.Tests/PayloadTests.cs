using System.Text;

namespace Metaphor.Installer.Tests;

[Trait("Category", "Unit")]
public sealed class PayloadTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("metaphor-payload-").FullName;
    private readonly TestPayload payload = new();

    public void Dispose() => Directory.Delete(root, recursive: true);

    private void Unpack(Stream? zip = null) => Payload.Unpack(zip ?? payload.Zip(), payload.ParsedManifest(), root);

    private void Verify() => Payload.Verify(root, payload.ParsedManifest());

    [Fact]
    public void UnpacksEveryFileItsListNamesAndTheyCheckOut()
    {
        Unpack();

        foreach (var file in payload.Files)
        {
            Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(root, file.Key)));
        }
        Verify();
    }

    [Fact]
    public void AFileTheListDoesNotNameIsDamaged()
    {
        var entries = payload.Files.Append(KeyValuePair.Create("metaphor.accessibility/extra.ps1", new byte[] { 1 }));

        Assert.Throws<PayloadDamagedException>(() => Unpack(payload.Zip(entries)));
        Assert.False(File.Exists(Path.Combine(root, "metaphor.accessibility", "extra.ps1")));
    }

    [Fact]
    public void AFileMissingFromTheZipIsDamaged()
    {
        var entries = payload.Files.Where(file => file.Key != "metaphor.accessibility/prism.dll");

        Assert.Throws<PayloadDamagedException>(() => Unpack(payload.Zip(entries)));
    }

    [Fact]
    public void AnEntryTwiceIsDamaged()
    {
        var entries = payload.Files.Append(payload.Files.First());

        Assert.Throws<PayloadDamagedException>(() => Unpack(payload.Zip(entries)));
    }

    [Fact]
    public void AZipThatIsNotAZipIsDamaged() =>
        Assert.Throws<PayloadDamagedException>(() => Unpack(new MemoryStream(Encoding.UTF8.GetBytes("not a zip"))));

    [Fact]
    public void AnEntryWithOneByteChangedFailsTheCheck()
    {
        var manifest = payload.ParsedManifest();
        payload.Files["metaphor.accessibility/install.ps1"][0] ^= 1;

        Payload.Unpack(payload.Zip(), manifest, root);

        Assert.Throws<PayloadDamagedException>(() => Payload.Verify(root, manifest));
    }

    [Fact]
    public void AFileChangedAfterUnpackingFailsTheCheck()
    {
        Unpack();
        File.WriteAllText(Path.Combine(root, "metaphor.accessibility", "install.ps1"), "changed");

        Assert.Throws<PayloadDamagedException>(Verify);
    }

    [Fact]
    public void AFileRemovedAfterUnpackingFailsTheCheck()
    {
        Unpack();
        File.Delete(Path.Combine(root, "metaphor.accessibility", "prism.dll"));

        Assert.Throws<PayloadDamagedException>(Verify);
    }

    [Fact]
    public void AFileAddedAfterUnpackingFailsTheCheck()
    {
        Unpack();
        File.WriteAllText(Path.Combine(root, "metaphor.accessibility", "planted.ps1"), "planted");

        Assert.Throws<PayloadDamagedException>(Verify);
    }

    [Fact]
    public void AFolderAddedAfterUnpackingFailsTheCheck()
    {
        Unpack();
        Directory.CreateDirectory(Path.Combine(root, "metaphor.accessibility", "planted"));

        Assert.Throws<PayloadDamagedException>(Verify);
    }

    [Fact]
    public void NeverWritesOverAFileAlreadyThere()
    {
        Directory.CreateDirectory(Path.Combine(root, "metaphor.accessibility"));
        var planted = Path.Combine(root, "metaphor.accessibility", "install.ps1");
        File.WriteAllText(planted, "planted");

        Assert.ThrowsAny<IOException>(() => Unpack());
        Assert.Equal("planted", File.ReadAllText(planted));
    }
}
