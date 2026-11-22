namespace Metaphor.Installer.Tests;

[Trait("Category", "Unit")]
public sealed class PayloadManifestTests
{
    private static readonly string Hash = new('a', 64);

    [Fact]
    public void ReadsEachFileAndItsHash()
    {
        var payload = new TestPayload();

        var manifest = payload.ParsedManifest();

        Assert.Equal(TestPayload.Names.Order(StringComparer.Ordinal), manifest.Paths);
        Assert.Equal(TestPayload.Sha256(payload.Files["metaphor.accessibility/install.ps1"]),
            manifest.HashOf("metaphor.accessibility/install.ps1"));
    }

    [Fact]
    public void FindsTheModFolderByTheInstallerInIt() =>
        Assert.Equal("metaphor.accessibility", new TestPayload().ParsedManifest().ModFolder);

    [Fact]
    public void TakesWindowsLineEndings() =>
        Assert.Equal("m", PayloadManifest.Parse($"{Hash}  m/install.ps1\r\n").ModFolder);

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    public void AnEmptyListIsDamaged(string text) => Assert.Throws<PayloadDamagedException>(() => PayloadManifest.Parse(text));

    [Theory]
    [InlineData("aaaa  m/install.ps1")] // too short
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA  m/install.ps1")] // not lower case
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg  m/install.ps1")] // not hex
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa m/install.ps1")] // one space
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  ")] // no path
    public void AMalformedLineIsDamaged(string line) =>
        Assert.Throws<PayloadDamagedException>(() => PayloadManifest.Parse(line + "\n"));

    [Theory]
    [InlineData("../install.ps1")]
    [InlineData("m/../../x")]
    [InlineData("m/./x")]
    [InlineData("/m/x")]
    [InlineData("m//x")]
    [InlineData("m/x/")]
    [InlineData(@"m\x")]
    [InlineData("C:/x")]
    [InlineData("m/x:stream")]
    [InlineData("m/CON")]
    [InlineData("m/nul.txt")]
    [InlineData("m/com1")]
    [InlineData("m/x.")]
    [InlineData("m/x ")]
    [InlineData("m/ x")]
    [InlineData("m/x*")]
    [InlineData("m/x\ty")]
    public void APathThatCouldLeaveTheFolderOrMeanSomethingElseIsDamaged(string path) =>
        Assert.Throws<PayloadDamagedException>(() => PayloadManifest.Parse($"{Hash}  m/install.ps1\n{Hash}  {path}\n"));

    [Theory]
    [InlineData("m/install.ps1")]
    [InlineData("M/INSTALL.PS1")]
    public void TheSameFileTwiceIsDamaged(string again) =>
        Assert.Throws<PayloadDamagedException>(() => PayloadManifest.Parse($"{Hash}  m/install.ps1\n{Hash}  {again}\n"));

    [Theory]
    [InlineData("install.ps1")] // not in a folder
    [InlineData("m/sub/install.ps1")] // too deep
    [InlineData("m/check-install.ps1")]
    public void AListWithoutTheInstallerInAModFolderIsDamaged(string path) =>
        Assert.Throws<PayloadDamagedException>(() => PayloadManifest.Parse($"{Hash}  {path}\n"));

    [Fact]
    public void TwoModFoldersAreDamaged() =>
        Assert.Throws<PayloadDamagedException>(() => PayloadManifest.Parse($"{Hash}  a/install.ps1\n{Hash}  b/install.ps1\n"));

    [Fact]
    public void AFileItDoesNotListHasNoHash() => Assert.Null(new TestPayload().ParsedManifest().HashOf("metaphor.accessibility/other"));
}
