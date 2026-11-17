using System.Text.Json;
using Metaphor.Core.Announcements;

namespace Metaphor.Core.Tests;

[Trait("Category", "Unit")]
public sealed class ModStartupTests : IDisposable
{
    private const string ModId = "metaphor.test";

    // A stand-in game exe holding the 4 ASCII bytes "game", and their SHA-256 from `printf 'game' | sha256sum`.
    private const string GameBuild = "6ca5cab77e702c787b4c14b3d3bf26bad43da606be6eed04ab0b9720120ae081";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("metaphor-startup-");
    private readonly string _gameExe;

    public ModStartupTests()
    {
        _gameExe = Path.Combine(_folder.FullName, "METAPHOR.exe");
        File.WriteAllText(_gameExe, "game");
    }

    public void Dispose() => _folder.Delete(recursive: true);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private ModStatus Read() =>
        JsonSerializer.Deserialize<ModStatus>(File.ReadAllText(Path.Combine(_folder.FullName, ModStatusFile.FileName)), Web)!;

    private static GameBuild Build(string sha256) => new(sha256, SteamBuildId: 1, GameVersion: null, Evidence: "test");

    [Fact]
    public void OnAKnownBuildTheModIsReady()
    {
        ModStartup.CheckAndReport(ModId, _gameExe, _folder.FullName, () => [Build(GameBuild)], hooksExpected: 0);

        Assert.Equal(ModStatus.Ready(ModId, GameBuild, hooksLoaded: 0, hooksExpected: 0), Read());
    }

    [Fact]
    public void OnAKnownBuildTheModInstallsItsHooksAndReportsHowManyLoaded()
    {
        string? askedFor = null;

        var status = ModStartup.CheckAndReport(
            ModId, _gameExe, _folder.FullName, () => [Build(GameBuild)], hooksExpected: 3,
            installHooks: build => { askedFor = build; return 2; });

        Assert.Equal(GameBuild, askedFor);
        Assert.Equal(ModStatus.Ready(ModId, GameBuild, hooksLoaded: 2, hooksExpected: 3), status);
        Assert.Equal(status, Read());
    }

    // #92: the signature spike must see the game's own bytes, not the patches our hooks write.
    [Fact]
    public void WorkBeforeHooksRunsWithTheBuildBeforeAnyHookInstalls()
    {
        var calls = new List<string>();

        ModStartup.CheckAndReport(
            ModId, _gameExe, _folder.FullName, () => [Build(GameBuild)], hooksExpected: 1,
            installHooks: _ => { calls.Add("install"); return 1; },
            beforeHooks: build => calls.Add($"before {build}"));

        Assert.Equal([$"before {GameBuild}", "install"], calls);
    }

    [Fact]
    public void WhenWorkBeforeHooksFailsTheHooksStillInstall()
    {
        var status = ModStartup.CheckAndReport(
            ModId, _gameExe, _folder.FullName, () => [Build(GameBuild)], hooksExpected: 1,
            installHooks: _ => 1, beforeHooks: _ => throw new IOException("probe file locked"));

        Assert.Equal(ModStatus.Ready(ModId, GameBuild, hooksLoaded: 1, hooksExpected: 1), status);
    }

    [Fact]
    public void OnAnUnknownBuildNoHookIsInstalled()
    {
        var installed = false;

        ModStartup.CheckAndReport(
            ModId, _gameExe, _folder.FullName, () => [Build(new string('0', 64))], hooksExpected: 1,
            installHooks: _ => { installed = true; return 1; });

        Assert.False(installed);
    }

    [Fact]
    public void WhenTheHookLoaderFailsTheModRefusesWithoutThrowing()
    {
        var status = ModStartup.CheckAndReport(
            ModId, _gameExe, _folder.FullName, () => [Build(GameBuild)], hooksExpected: 1,
            installHooks: _ => throw new InvalidOperationException("no hooks controller"));

        Assert.Equal(ModState.Refused, status.State);
        Assert.Equal("hook loader failed: InvalidOperationException: no hooks controller", status.Reason);
    }

    [Fact]
    public void OnAnUnknownBuildTheModRefuses()
    {
        ModStartup.CheckAndReport(ModId, _gameExe, _folder.FullName, () => [Build(new string('0', 64))], hooksExpected: 2);

        Assert.Equal(ModStatus.Refused(ModId, GameBuild, hooksExpected: 2, "unknown game build"), Read());
    }

    // #187: a blind player can't read the status file, so the refusal is spoken.
    [Fact]
    public void OnAnUnknownBuildThePlayerHearsThatTheModIsOff()
    {
        var announcer = new RecordingAnnouncer();

        ModStartup.CheckAndReport(
            ModId, _gameExe, _folder.FullName, () => [Build(new string('0', 64))], hooksExpected: 1, announcer: announcer);

        Assert.Equal(
            [new Announcement("This game version isn't supported yet; the mod is off", Interrupt: true)], announcer.Said);
    }

    [Fact]
    public void OnAKnownBuildThePlayerHearsThatTheModLoaded()
    {
        var announcer = new RecordingAnnouncer();

        ModStartup.CheckAndReport(
            ModId, _gameExe, _folder.FullName, () => [Build(GameBuild)], hooksExpected: 1, installHooks: _ => 1,
            announcer: announcer);

        Assert.Equal([new Announcement("Metaphor accessibility loaded", Interrupt: true)], announcer.Said);
    }

    // A supported build whose hooks fail is not an unsupported version; saying so would send the player the wrong way.
    [Fact]
    public void WhenTheHookLoaderFailsThePlayerHearsTheModCouldNotStart()
    {
        var announcer = new RecordingAnnouncer();

        ModStartup.CheckAndReport(
            ModId, _gameExe, _folder.FullName, () => [Build(GameBuild)], hooksExpected: 1,
            installHooks: _ => throw new InvalidOperationException("no hooks controller"), announcer: announcer);

        Assert.Equal(
            [new Announcement("Metaphor accessibility couldn't start; the mod is off", Interrupt: true)], announcer.Said);
    }

    // The game must keep running whatever goes wrong: the loader rethrows a mod's start exception.
    [Fact]
    public void WhenTheKnownBuildsCannotLoadTheModRefusesWithoutThrowing()
    {
        var status = ModStartup.CheckAndReport(
            ModId, _gameExe, _folder.FullName, () => throw new InvalidDataException("known-builds file is malformed"), 1);

        Assert.Equal(ModStatus.Refused(ModId, GameBuild, hooksExpected: 1, "known-builds file is malformed"), Read());
        Assert.Equal(status, Read());
    }

    [Fact]
    public void WhenTheGameExeCannotBeReadTheModRefusesWithoutThrowing()
    {
        var missing = Path.Combine(_folder.FullName, "missing.exe");

        ModStartup.CheckAndReport(ModId, missing, _folder.FullName, () => [Build(GameBuild)], hooksExpected: 1);

        var status = Read();
        Assert.Equal(ModState.Refused, status.State);
        Assert.Equal("unknown", status.GameBuildSha256);
        // Players paste status files into bug reports, so the reason names the error, not the path.
        Assert.Equal("can't hash the game executable: FileNotFoundException", status.Reason);
    }

    [Fact]
    public void WhenTheStatusCannotBeWrittenTheModStillReturnsItsStatus()
    {
        var noFolder = Path.Combine(_folder.FullName, "not-there");

        var status = ModStartup.CheckAndReport(ModId, _gameExe, noFolder, () => [Build(GameBuild)], hooksExpected: 0);

        Assert.Equal(ModStatus.Ready(ModId, GameBuild, hooksLoaded: 0, hooksExpected: 0), status);
    }
}
