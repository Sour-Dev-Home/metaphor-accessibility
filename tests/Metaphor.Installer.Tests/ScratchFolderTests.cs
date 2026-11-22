namespace Metaphor.Installer.Tests;

[Trait("Category", "Unit")]
public sealed class ScratchFolderTests : IDisposable
{
    private readonly string temp = Directory.CreateTempSubdirectory("metaphor-temp-").FullName;

    public void Dispose() => Directory.Delete(temp, recursive: true);

    [Fact]
    public void MakesANewEmptyFolderInTheTempFolder()
    {
        var folder = ScratchFolder.Create(temp);

        Assert.Equal(temp, Path.GetDirectoryName(folder));
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
    }

    [Fact]
    public void NamesItWithARandomPart()
    {
        var name = Path.GetFileName(ScratchFolder.Create(temp));

        Assert.Matches("^metaphor-installer-[0-9a-f]{32}$", name);
        Assert.NotEqual(name, Path.GetFileName(ScratchFolder.Create(temp)));
    }

    [Fact]
    public void NeverUsesAFolderOrFileAlreadyThere()
    {
        Directory.CreateDirectory(Path.Combine(temp, "taken-folder"));
        File.WriteAllText(Path.Combine(temp, "taken-file"), "");
        var names = new Queue<string>(["taken-folder", "taken-file", "free"]);

        var folder = ScratchFolder.Create(temp, names.Dequeue);

        Assert.Equal(Path.Combine(temp, "free"), folder);
    }

    [Fact]
    public void GivesUpWhenEveryNameIsTaken()
    {
        Directory.CreateDirectory(Path.Combine(temp, "taken"));

        Assert.Throws<IOException>(() => ScratchFolder.Create(temp, () => "taken"));
    }

    [Fact]
    public void TheCleanUpRemovesTheFolderAndEverythingInIt()
    {
        var folder = ScratchFolder.Create(temp);
        Directory.CreateDirectory(Path.Combine(folder, "a", "b"));
        File.WriteAllText(Path.Combine(folder, "a", "b", "c.txt"), "c");
        var readOnly = Path.Combine(folder, "a", "read-only.txt");
        File.WriteAllText(readOnly, "r");
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);

        Assert.True(ScratchFolder.Delete(folder));

        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void TheCleanUpOfAFolderAlreadyGoneSucceeds() => Assert.True(ScratchFolder.Delete(Path.Combine(temp, "gone")));
}
