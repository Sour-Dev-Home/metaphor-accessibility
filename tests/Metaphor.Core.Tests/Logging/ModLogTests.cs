using System.Text;
using Metaphor.Core.Logging;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests.Logging;

/// <summary>The mod's own log lines (#824): one prefix per area, one startup result per subsystem, and nothing private in
/// either log a player shares, the report log or the loader's console. Made-up folders and names only.</summary>
[Trait("Category", "Unit")]
public sealed class ModLogTests : IDisposable
{
    public void Dispose() => text.Dispose();

    private const string UserName = "rowanfan";
    private const string Profile = @"C:\Users\" + UserName;

    private readonly List<string> console = [];
    private readonly StringWriter text = new();

    private (ModLog Log, ReportLog Report) NewLog(LogScrubber? scrubber = null)
    {
        scrubber ??= LogScrubber.None();
        var report = new ReportLog(text, () => 0, scrubber);
        return (new ModLog(console.Add, report, scrubber), report);
    }

    private string[] Written(ReportLog report)
    {
        Assert.True(report.WaitForWrites(TimeSpan.FromSeconds(5)));
        return text.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public void EachAreasLinesStartWithItsPrefix()
    {
        var (log, report) = NewLog();

        log.For(LogArea.Hooks)("hook Dialogue installed");
        log.ErrorsFor(LogArea.Settings)("settings file unreadable");
        log.For(LogArea.Data)("tutorial table: 3 tutorials");

        string[] expected = ["hooks: hook Dialogue installed", "settings: settings file unreadable", "data: tutorial table: 3 tutorials"];
        Assert.Equal(expected, console);
        Assert.Equal(expected.Select(line => "0 ms " + line), Written(report));
    }

    [Fact]
    public void EveryAreaHasItsOwnLowerCasePrefix()
    {
        var prefixes = Enum.GetValues<LogArea>().Select(ModLog.Prefix).ToList();

        Assert.Equal(prefixes.Count, prefixes.Distinct().Count());
        Assert.All(prefixes, prefix => Assert.Matches("^[a-z]+: $", prefix));
    }

    [Fact]
    public void EachSubsystemHasOneStartupResultLine()
    {
        var (log, report) = NewLog();

        log.Result("speech: NVDA");
        log.Result("hooks: 51 of 51 installed in 120 ms");
        log.Result("hooks: 50 of 51 installed in 90 ms");
        log.Result("tutorial table: 3 tutorials in 4 ms");

        Assert.Equal(
            ["startup: speech: NVDA", "startup: hooks: 51 of 51 installed in 120 ms", "startup: tutorial table: 3 tutorials in 4 ms"],
            console);
        Assert.Equal(3, Written(report).Length);
    }

    [Fact]
    public void UnknownValuesGoToTheUnknownArea()
    {
        var (log, _) = NewLog();

        log.Unknowns.Report("icon", "PAD_ICON 80");
        log.Unknowns.Report("icon", "PAD_ICON 80");

        Assert.Equal(["unknown: icon PAD_ICON 80"], console);
    }

    [Fact]
    public void AConsoleThatThrowsStillLeavesTheReportLogItsLine()
    {
        var report = new ReportLog(text, () => 0, LogScrubber.None());
        var log = new ModLog(_ => throw new IOException("console gone"), report, LogScrubber.None());

        log.For(LogArea.Speech)("prism started");

        Assert.Equal(["0 ms speech: prism started"], Written(report));
    }

    /// <summary>The scrub test (#824): a Release log, the report log or the loader's console Reloaded-II saves, never
    /// holds the user's profile path or user name, a name the player typed, or a raw memory dump. Each is planted where
    /// a real one could come from: an exception's message, a typed name the mod read, a value it couldn't map, a dump of
    /// bytes.</summary>
    [Fact]
    public void ReleaseLogsHoldNoUserPathUserNameTypedNameOrMemoryDump()
    {
        var scrubber = new LogScrubber(LogScrubber.PathsToRedact(
            @"G:\Mods\metaphor.accessibility", Profile + @"\AppData\Roaming\Reloaded-Mod-Loader-II\User\Mods\metaphor",
            @"G:\Games\Metaphor\METAPHOR.exe", Profile, UserName));
        var (log, report) = NewLog(scrubber);
        // The mod reads the names the player typed when it speaks them; from then on both logs clean them.
        var memory = new TypedNames { [TypedName.Hero] = "Wilhelmina", [TypedName.Player] = "Kestrel" };
        var names = new GameNames(memory, new MessageLibrary()) { TypedNameRead = scrubber.AddName };
        names.Typed(TypedName.Hero);
        names.Typed(TypedName.Player);
        const string dump = "48 8B 05 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF";

        log.ErrorsFor(LogArea.Settings)($@"settings file unreadable: Access to the path '{Profile}\Documents\settings.json' is denied.");
        log.For(LogArea.Speech)($@"prism.dll not found under C:/Users/{UserName}/Desktop");
        log.ErrorsFor(LogArea.Hooks)($"hook Dialogue failed for {UserName}: saw Wilhelmina's save, guide KESTREL");
        log.Unknowns.Report("tag", "Wilhelmina");
        log.For(LogArea.Inspector)($"read {dump}");
        report.Note($"note from {UserName} with {dump}");

        var logs = string.Join(Environment.NewLine, console) + Environment.NewLine + string.Join(Environment.NewLine, Written(report));
        foreach (var secret in new[] { UserName, Profile, "Users/", "Wilhelmina", "Kestrel", "48 8B 05" })
        {
            Assert.DoesNotContain(secret, logs, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("<memory dump, 18 bytes>", logs, StringComparison.Ordinal);
        Assert.Contains("unknown: tag <name>", logs, StringComparison.Ordinal);
    }

    private sealed class TypedNames : Dictionary<TypedName, string>, IGameNameMemory
    {
        public byte[]? CharacterEntry(int character) => null;

        public int? FollowerCharacter(int follower) => null;

        public bool? StoryFlag(int flag) => null;

        public byte[]? TypedName(TypedName name)
        {
            if (!TryGetValue(name, out var typed))
            {
                return null;
            }
            var field = new byte[TypedNameText.Size];
            Encoding.UTF32.GetBytes(typed).CopyTo(field, 0);
            return field;
        }
    }
}
