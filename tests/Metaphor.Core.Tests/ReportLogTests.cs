using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Core.Tests;

/// <summary>The report log a player attaches to a bug report (#188): labels, events, hook counts and errors, never
/// message text. Made-up messages only.</summary>
[Trait("Category", "Unit")]
public sealed class ReportLogTests
{
    private const string Secret = "The bridge is out, Ada.";

    private static (ReportLog Log, StringWriter Text) NewLog(long now = 1500, params string[] redact)
    {
        var text = new StringWriter();
        return (new ReportLog(text, () => now, redact), text);
    }

    /// <summary>The text once the log's background writer has caught up.</summary>
    private static string Written(ReportLog log, StringWriter text)
    {
        Assert.True(log.WaitForWrites(TimeSpan.FromSeconds(5)));
        return text.ToString();
    }

    [Fact]
    public void RecordsACompiledMessageFileWithoutItsSource()
    {
        var (log, text) = NewLog();

        log.Observe(new MessageFileCompiled(Path: "event/e100.msg", Id: 7, Source: $"[msg line_1]\n{Secret}"));

        Assert.Equal($"1500 ms compile id=7 path=(not a label, 14 chars) chars=36{Environment.NewLine}", Written(log, text));
    }

    [Fact]
    public void RecordsARequestedLineByKindAndLabel()
    {
        var (log, text) = NewLog(now: 2750);

        log.Observe(new LineRequested(Kind: 2, Label: "line_1"));

        Assert.Equal($"2750 ms request kind=2 label=line_1{Environment.NewLine}", Written(log, text));
    }

    [Fact]
    public void RecordsACursorInputByItsNumbers()
    {
        var (log, text) = NewLog(now: 300);

        log.Observe(new CursorInput(Manager: 0x1a0, ClassRva: 0x2b0, Command: 0x15, Result: 1, Top: 2, Row: 1, Flags: 0x8));

        Assert.Equal(
            $"300 ms cursor mgr=0x1a0 class=0x2b0 cmd=0x15 result=1 index=3 top=2 row=1 flags=0x8{Environment.NewLine}",
            Written(log, text));
    }

    [Fact]
    public void RecordsAnOpenedChoiceByKindAndLabels()
    {
        var (log, text) = NewLog(now: 300);

        log.Observe(new ChoiceRequested(Kind: 1, PromptLabel: "sel_q", ChoiceLabel: null));

        Assert.Equal($"300 ms select kind=1 prompt=sel_q choice=(unreadable){Environment.NewLine}", Written(log, text));
    }

    /// <summary>A label read from a bad pointer could be message text, so anything not shaped like the game's labels
    /// is counted, not written.</summary>
    [Fact]
    public void WritesOnlyTheLengthOfALabelNotShapedLikeOne()
    {
        var (log, text) = NewLog(now: 10);

        log.Observe(new LineRequested(Kind: 0, Label: Secret));
        log.Observe(new ChoiceRequested(Kind: 0, PromptLabel: "Go_\nback", ChoiceLabel: "_Go"));
        log.Observe(new MessageFileCompiled(Path: Secret, Id: 1, Source: ""));

        Assert.Equal(
            """
            10 ms request kind=0 label=(not a label, 23 chars)
            10 ms select kind=0 prompt=(not a label, 8 chars) choice=(not a label, 3 chars)
            10 ms compile id=1 path=(not a label, 23 chars) chars=0

            """.ReplaceLineEndings(),
            Written(log, text));
    }

    /// <summary>A one-word line of game text has no underscore, so it isn't a label.</summary>
    [Fact]
    public void WritesOnlyTheLengthOfAOneWordMessage()
    {
        var (log, text) = NewLog(now: 10);

        log.Observe(new ChoiceRequested(Kind: 0, PromptLabel: "Yes", ChoiceLabel: "Okay"));

        Assert.Equal($"10 ms select kind=0 prompt=(not a label, 3 chars) choice=(not a label, 4 chars){Environment.NewLine}", Written(log, text));
    }

    /// <summary>Labels in the game's shape: letters and digits joined by underscores.</summary>
    [Fact]
    public void WritesALabelShapedLikeTheGames()
    {
        var (log, text) = NewLog(now: 10);

        log.Observe(new ChoiceRequested(Kind: 0, PromptLabel: "MES_e00_002_001_004", ChoiceLabel: "Title_System_08"));

        Assert.Equal($"10 ms select kind=0 prompt=MES_e00_002_001_004 choice=Title_System_08{Environment.NewLine}", Written(log, text));
    }

    [Fact]
    public void RecordsANoteWithEachRedactedStringReplaced()
    {
        var (log, text) = NewLog(500, @"D:\Profiles\someone", "someone");

        log.Note(@"hooks 3/4 (can't read D:\Profiles\someone\game.exe as someone)");

        Assert.Equal($"500 ms hooks 3/4 (can't read <redacted>\\game.exe as <redacted>){Environment.NewLine}", Written(log, text));
    }

    /// <summary>The same folder written with forward slashes, or with doubled backslashes as an escaped string shows
    /// it (#204).</summary>
    [Fact]
    public void RedactsAPathWrittenWithForwardSlashes()
    {
        var (log, text) = NewLog(500, @"D:\Profiles\someone");

        log.Note("can't read D:/Profiles/someone/game.exe or D:\\\\Profiles\\\\someone\\\\x.dll");

        Assert.Equal($"500 ms can't read <redacted>/game.exe or <redacted>\\\\x.dll{Environment.NewLine}", Written(log, text));
    }

    /// <summary>Windows can hand out a path with any folder in its 8.3 short form, such as <c>SOMEON~1</c> or the
    /// hashed <c>SO1A2B~1</c> (#204).</summary>
    [Fact]
    public void RedactsAPathWithFoldersInTheirShortForm()
    {
        var (log, text) = NewLog(500, @"D:\Profiles\someone.else\Mods\metaphor");

        log.Note(@"can't read D:\PROFIL~1\SOMEON~1\Mods\METAPH~2\a.dll or d:/Profiles/SO1A2B~1/MODS~1/metaphor/b.dll");

        Assert.Equal($"500 ms can't read <redacted>\\a.dll or <redacted>/b.dll{Environment.NewLine}", Written(log, text));
    }

    /// <summary>A startup reason can name the game's install folder (#204), so it's redacted with the mod's folders and
    /// the player's profile and name.</summary>
    [Fact]
    public void RedactsTheGameFolderAlongsideTheModsAndThePlayers()
    {
        var redact = ReportLog.PathsToRedact(
            modFolder: @"E:\Reloaded\Mods\metaphor", configFolder: @"E:\Reloaded\User\Mods\metaphor",
            gameExecutable: @"F:\Games\Metaphor ReFantazio\METAPHOR.exe", userProfile: @"D:\Profiles\someone", userName: "someone");
        var (log, text) = NewLog(500, [.. redact]);

        log.Note(@"unknown build (F:\Games\Metaphor ReFantazio\METAPHOR.exe, F:/Games/METAPH~1/x, E:\Reloaded\Mods\metaphor\y, D:\Profiles\someone, someone)");

        Assert.Equal(
            $"500 ms unknown build (<redacted>\\METAPHOR.exe, <redacted>/x, <redacted>\\y, <redacted>, <redacted>){Environment.NewLine}",
            Written(log, text));
    }

    /// <summary>A user name under three characters isn't redacted on its own: replacing it everywhere would garble
    /// the log. Nor is a bare drive (a game at <c>F:\</c>), which names no one.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(@"F:\METAPHOR.exe")]
    public void LeavesAShortUserNameAndAMissingOrDriveRootGameFolderOut(string? gameExecutable)
    {
        var redact = ReportLog.PathsToRedact(
            modFolder: @"E:\m", configFolder: @"E:\c", gameExecutable, userProfile: @"D:\Profiles\al", userName: "al");

        Assert.Equal([@"E:\m", @"E:\c", @"D:\Profiles\al"], redact);
    }

    /// <summary>Logging runs on the game's threads, so a slow disk mustn't stall them (#204): the lines wait in memory
    /// and a background writer writes them.</summary>
    [Fact]
    public async Task ABurstOfLinesNeverWaitsForTheFile()
    {
        using var stalled = new StalledWriter();
        var log = new ReportLog(stalled, () => 7, []);

        var burst = Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                log.Note($"line {i}");
                log.Observe(new LineRequested(0, "line_1"));
            }
        });

        var finished = await Task.WhenAny(burst, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(finished == burst, "the caller waited for the file");
        stalled.Release();
        Assert.True(log.WaitForWrites(TimeSpan.FromSeconds(5)));
        Assert.Equal(200, stalled.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.StartsWith($"7 ms line 0{Environment.NewLine}7 ms request kind=0 label=line_1", stalled.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Lines past what the log holds in memory are counted, not kept, so a stalled disk can't grow the game's
    /// memory without end.</summary>
    [Fact]
    public void CountsTheLinesItDropsWhileTheFileIsStalled()
    {
        using var stalled = new StalledWriter();
        var log = new ReportLog(stalled, () => 7, []);

        // The writer takes every queued line in one batch, so flood only once it's stuck on the first line: then the
        // count is exact instead of depending on when its thread started.
        log.Note("x");
        Assert.True(stalled.Stuck.Wait(TimeSpan.FromSeconds(5)), "the writer never started");
        for (var i = 1; i < 20_000; i++)
        {
            log.Note("x");
        }

        stalled.Release();
        Assert.True(log.WaitForWrites(TimeSpan.FromSeconds(5)));
        var lines = stalled.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(1 + 4096 + 1, lines.Length);
        Assert.Equal("7 ms (15903 lines dropped: the log fell behind)", lines[^1]);
    }

    /// <summary>The writer writes as lines come, not only when asked or at exit, so a native crash, which runs no exit
    /// handler, loses at most the last moment's lines (#204).</summary>
    [Fact]
    public void AQueuedLineReachesTheFileWithoutBeingWaitedFor()
    {
        using var file = new FlushSignallingWriter();
        var log = new ReportLog(file, () => 40, []);

        log.Observe(new LineRequested(0, "line_1"));

        Assert.True(file.Flushed.Wait(TimeSpan.FromSeconds(5)), "the line was never written");
        Assert.Equal($"40 ms request kind=0 label=line_1{Environment.NewLine}", file.ToString());
    }

    /// <summary>An error may come just before a crash, when testers need the log most, so it's on disk, after every
    /// line queued before it, by the time the call returns.</summary>
    [Fact]
    public void AnErrorIsWrittenBeforeTheCallReturns()
    {
        var (log, text) = NewLog(900, @"D:\Profiles\someone");

        log.Observe(new LineRequested(0, "line_1"));
        log.Error(@"hook failed reading D:/Profiles/someone/game.exe");

        Assert.Equal(
            $"900 ms request kind=0 label=line_1{Environment.NewLine}900 ms hook failed reading <redacted>/game.exe{Environment.NewLine}",
            text.ToString());
    }

    /// <summary>Writing an error through waits for the disk, but only briefly: a stalled disk mustn't hang the game.
    /// </summary>
    [Fact]
    public async Task AnErrorWaitsOnlyBrieflyForAStalledFile()
    {
        using var stalled = new StalledWriter();
        var log = new ReportLog(stalled, () => 7, []);

        var error = Task.Run(() => log.Error("hook failed"));

        var finished = await Task.WhenAny(error, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(finished == error, "the error waited for the stalled file");
        stalled.Release();
        Assert.True(log.WaitForWrites(TimeSpan.FromSeconds(5)));
        Assert.Equal($"7 ms hook failed{Environment.NewLine}", stalled.ToString());
    }

    [Fact]
    public void NeverThrowsWhenTheFileCantBeWritten()
    {
        var log = new ReportLog(new BrokenWriter(), () => 0, []);

        log.Note("hooks 0/1");
        log.Observe(new LineRequested(0, "line_1"));
    }

    /// <summary>The issue's done-when (#188): every way into the log, fed message text, writes none of it.</summary>
    [Fact]
    public void NoMessageTextReachesTheLog()
    {
        var (log, text) = NewLog();

        log.Observe(new MessageFileCompiled(Path: Secret, Id: 1, Source: Secret));
        log.Observe(new LineRequested(Kind: 1, Label: Secret));
        log.Observe(new ChoiceRequested(Kind: 1, PromptLabel: Secret, ChoiceLabel: Secret));
        foreach (var word in Secret.Split(' '))
        {
            log.Observe(new LineRequested(Kind: 1, Label: word));
        }

        Assert.All(Secret.Split(' '), word => Assert.DoesNotContain(word, Written(log, text), StringComparison.Ordinal));
    }

    private sealed class BrokenWriter : StringWriter
    {
        public override void Write(string? value) => throw new IOException("disk full");
    }

    /// <summary>A file that signals when the writer flushes it, which the writer does once a batch is written, so the
    /// test reads it only after the writer is done with it.</summary>
    private sealed class FlushSignallingWriter : StringWriter
    {
        public ManualResetEventSlim Flushed { get; } = new();

        public override void Flush()
        {
            base.Flush();
            Flushed.Set();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Flushed.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>A file whose every write waits until the test releases it, as a stalled disk would.</summary>
    private sealed class StalledWriter : StringWriter
    {
        private readonly ManualResetEventSlim released = new();

        /// <summary>Set once the log's writer is inside a write, stuck until <see cref="Release"/>.</summary>
        public ManualResetEventSlim Stuck { get; } = new();

        public void Release() => released.Set();

        public override void Write(string? value)
        {
            Stuck.Set();
            released.Wait(TimeSpan.FromSeconds(10));
            base.Write(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                released.Dispose();
                Stuck.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
