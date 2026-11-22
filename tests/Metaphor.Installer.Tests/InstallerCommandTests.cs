namespace Metaphor.Installer.Tests;

[Trait("Category", "Unit")]
public sealed class InstallerCommandTests
{
    private static readonly string System32 = Path.Combine("C:", "Windows", "System32");
    private static readonly string Folder = Path.Combine("C:", "Temp", "metaphor-installer-1");

    private static InstallerCommand Command(params string[] args) => InstallerCommand.For(System32, Folder, "metaphor.accessibility", args);

    [Fact]
    public void StartsWindowsPowerShellByItsFullPathInSystem32() =>
        Assert.Equal(Path.Combine(System32, "WindowsPowerShell", "v1.0", "powershell.exe"), Command().FileName);

    [Fact]
    public void RunsTheUnpackedInstallerAsInstallCmdDoes()
    {
        var script = Path.Combine(Folder, "metaphor.accessibility", "install.ps1");

        Assert.Equal($"-NoLogo -NoProfile -ExecutionPolicy Bypass -File {script} -FromLauncher", Command().Arguments);
    }

    [Fact]
    public void RunsInTheModFolder() => Assert.Equal(Path.Combine(Folder, "metaphor.accessibility"), Command().WorkingDirectory);

    [Fact]
    public void PassesTheExesArgumentsOnAfterItsOwn() =>
        Assert.EndsWith(" -FromLauncher -NoSpeech -Answers yes", Command("-NoSpeech", "-Answers", "yes").Arguments);

    [Fact]
    public void QuotesAFolderWithSpaces()
    {
        var command = InstallerCommand.For(System32, Path.Combine("C:", "Users", "A B", "Temp", "x"), "m", []);

        Assert.Contains($"-File \"{Path.Combine("C:", "Users", "A B", "Temp", "x", "m", "install.ps1")}\" -FromLauncher", command.Arguments);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("", "\"\"")]
    [InlineData("two words", "\"two words\"")]
    [InlineData("tab\there", "\"tab\there\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData(@"C:\a b\", "\"C:\\a b\\\\\"")]
    [InlineData(@"C:\ab\", @"C:\ab\")]
    [InlineData("a\\\"b", "\"a\\\\\\\"b\"")]
    public void QuotesEachArgumentSoWindowsReadsItBackWhole(string argument, string quoted) =>
        Assert.Equal(quoted, CommandLine.Quote(argument));
}
