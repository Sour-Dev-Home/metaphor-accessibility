namespace Metaphor.Installer.Tests;

[Trait("Category", "Unit")]
public sealed class LauncherTests : IDisposable
{
    private readonly FakeHost host = new();

    public void Dispose() => Directory.Delete(host.TempFolder, recursive: true);

    private int Run(params string[] args) => Launcher.Run(host, args);

    [Fact]
    public void RunsTheInstallerAndReturnsItsExitCode()
    {
        host.InstallerExitCode = 7;

        Assert.Equal(7, Run());
        Assert.Single(host.Commands);
    }

    [Fact]
    public void RunsTheUnpackedInstallerWithTheExesArguments()
    {
        Run("-NoSpeech");

        var command = host.Commands.Single();
        Assert.Equal(Path.Combine(host.SystemFolder, "WindowsPowerShell", "v1.0", "powershell.exe"), command.FileName);
        Assert.EndsWith(" -FromLauncher -NoSpeech", command.Arguments);
        Assert.Equal(TestPayload.Names.Order(StringComparer.Ordinal), host.FilesAtStart);
    }

    [Fact]
    public void UnpacksIntoANewFolderInTheTempFolderAndDeletesItAfterwards()
    {
        Run();

        var folder = Path.GetDirectoryName(host.Commands.Single().WorkingDirectory)!;
        Assert.Equal(host.TempFolder, Path.GetDirectoryName(folder));
        Assert.False(Directory.Exists(folder));
        Assert.Empty(Directory.EnumerateFileSystemEntries(host.TempFolder));
    }

    [Fact]
    public void SpeaksTheWelcomeLineThroughTheReaderWithTheCheckedSpeechLibraryBeforeTheInstallerStarts()
    {
        host.ReaderRunning = true;

        Run();

        var welcome = host.Said.First();
        Assert.Equal(Launcher.Welcome, welcome.Line);
        Assert.Equal(SpeechMode.Reader, welcome.Mode);
        Assert.EndsWith(Path.Combine("metaphor.accessibility", "prism.dll"), welcome.Library!.Path);
        Assert.Equal(TestPayload.Sha256(host.Payload.Files["metaphor.accessibility/prism.dll"]), welcome.Library.Sha256);
        Assert.Equal(["say", "end-speech", "run", "wait"], host.Events);
    }

    [Fact]
    public void WaitsForAKeyOnlyAfterTheFolderIsGone()
    {
        Run();

        Assert.False(host.FolderExistedAtWait);
    }

    [Fact]
    public void WithoutAScreenReaderItSpeaksWithWindowsVoice()
    {
        Run();

        Assert.Equal(SpeechMode.Voice, host.Said.First().Mode);
    }

    [Fact]
    public void AsAdministratorItStopsBeforeUnpackingAnything()
    {
        host.Elevated = true;

        Assert.Equal(1, Run());

        Assert.Empty(host.Commands);
        Assert.Empty(Directory.EnumerateFileSystemEntries(host.TempFolder));
        Assert.Equal(Launcher.RunsAsAdministrator, host.Said.First().Line);
        Assert.Null(host.Said.First().Library);
        Assert.Equal("wait", host.Events.Last());
    }

    [Fact]
    public void AsAdministratorWithUserAccountControlOffItLeavesTheQuestionToTheInstaller()
    {
        host.Elevated = true;
        host.UacEnabled = false;

        Assert.Equal(0, Run());

        Assert.Single(host.Commands);
    }

    [Fact]
    public void AZipWithOneByteChangedIsDamagedAndInstallsNothing()
    {
        host.Manifest = host.Payload.Manifest();
        host.Payload.Files["metaphor.accessibility/install.ps1"][0] ^= 1;

        Assert.Equal(1, Run());

        AssertDamaged();
    }

    [Fact]
    public void AZipWithAFileMissingIsDamagedAndInstallsNothing()
    {
        host.Manifest = host.Payload.Manifest();
        host.Payload.Files.Remove("metaphor.accessibility/ModConfig.json");

        Assert.Equal(1, Run());

        AssertDamaged();
    }

    [Theory]
    [InlineData(FakeHost.ZipName)]
    [InlineData(FakeHost.ManifestName)]
    public void AnExeWithoutItsPayloadIsDamaged(string missing)
    {
        host.Missing = missing;

        Assert.Equal(1, Run());

        AssertDamaged();
    }

    [Fact]
    public void AFolderItCannotUnpackIntoStopsIt()
    {
        host.TempFolderOverride = Path.Combine(host.TempFolder, "missing", "deeper");
        File.WriteAllText(Path.Combine(host.TempFolder, "missing"), "a file, not a folder");

        Assert.Equal(1, Run());

        Assert.Empty(host.Commands);
        Assert.Equal(Launcher.CouldNotUnpack, host.Said.First().Line);
    }

    [Fact]
    public void APowerShellThatDoesNotStartStopsIt()
    {
        host.InstallerStarts = false;

        Assert.Equal(1, Run());

        Assert.Contains(host.Said, said => said.Line == Launcher.PowerShellDidNotStart);
        Assert.Empty(Directory.EnumerateFileSystemEntries(host.TempFolder));
    }

    [Fact]
    public void SaysSoWhenTheFolderCannotBeDeleted()
    {
        host.CleanUpFails = true;
        host.InstallerExitCode = 0;

        Assert.Equal(0, Run());

        Assert.Equal(Launcher.CouldNotCleanUp, host.Said.Last().Line);
    }

    private void AssertDamaged()
    {
        Assert.Empty(host.Commands);
        Assert.Equal(Launcher.Damaged, host.Said.First().Line);
        Assert.Null(host.Said.First().Library);
        Assert.Empty(Directory.EnumerateFileSystemEntries(host.TempFolder));
        Assert.Equal("wait", host.Events.Last());
    }
}
