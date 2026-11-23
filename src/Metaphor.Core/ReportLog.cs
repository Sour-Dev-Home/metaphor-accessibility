using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Metaphor.Core.Logging;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Core;

/// <summary>The log a player can attach to a bug report (#188), written in every build: labels, message events, hook
/// counts and errors, and never message text (ground rule 4). It hears the same message events as
/// <see cref="MessageLog"/> but keeps only their numbers and labels. Safe to call from any game thread, and never
/// throws. Only <see cref="Error"/> waits for the file, and only briefly (#204): every other caller just queues its
/// line, and a background thread writes the queue.</summary>
public sealed class ReportLog(TextWriter writer, Func<long> millisecondsSinceStart, LogScrubber scrubber)
    : IMessageFileObserver, ILineRequestObserver, IChoiceRequestObserver, ICursorObserver, ISessionReport
{
    /// <summary>The longest screen class name written; anything longer is counted instead.</summary>
    private const int MaxClassNameLength = 96;

    /// <summary>The log's file name in the mod's folder.</summary>
    public const string FileName = "report-log.txt";

    /// <summary>The longest label written; anything longer is counted instead.</summary>
    private const int MaxLabelLength = 64;

    /// <summary>The most lines waiting for the file; past it, lines are counted as dropped so a stalled disk can't
    /// grow the game's memory.</summary>
    private const int MaxQueuedLines = 4096;

    // Guards the queue and the writer's state; never held while writing the file.
    private readonly object gate = new();
    private readonly Queue<string> queue = new();
    private long dropped;
    private bool writing;
    private Thread? writerThread;

    /// <summary>A log redacting the strings in <paramref name="redact"/>, and memory dumps (#824).</summary>
    public ReportLog(TextWriter writer, Func<long> millisecondsSinceStart, IEnumerable<string> redact)
        : this(writer, millisecondsSinceStart, new LogScrubber(redact))
    {
    }

    /// <summary>What every note and error is cleaned with: shared with the loader's console (<see cref="ModLog"/>), so
    /// a name the player typed is cleaned from both once the mod has read it.</summary>
    public LogScrubber Scrubber { get; } = scrubber;

    /// <summary>A message file the game compiled: its number, its path (as a label, so usually only its length) and
    /// its length, not its source.</summary>
    public void Observe(MessageFileCompiled file) => Write(() =>
        Line($"compile id={file.Id} path={Label(file.Path)} chars={(file.Source ?? "").Length}"));

    /// <summary>A line the game asked for: its kind and its label, not its text.</summary>
    public void Observe(LineRequested line) => Write(() => Line($"request kind={line.Kind} label={Label(line.Label)}"));

    /// <summary>A choice the game opened: its kind and the labels of its prompt and options, not their text.</summary>
    public void Observe(ChoiceRequested choice) => Write(() =>
        Line($"select kind={choice.Kind} prompt={Label(choice.PromptLabel)} choice={Label(choice.ChoiceLabel)}"));

    /// <summary>An input a menu's cursor manager handled (#94), as <see cref="MessageLog"/> records it: numbers only,
    /// no game text.</summary>
    public void Observe(CursorInput input) => Write(() => Line(
        $"cursor mgr=0x{input.Manager:x} class={(input.ClassRva is { } rva ? $"0x{rva:x}" : "?")} cmd=0x{input.Command:x} result={input.Result} index={input.Index} top={input.Top} row={input.Row} flags=0x{input.Flags:x} screen={input.ScreenClass ?? "?"} count={input.Count?.ToString(CultureInfo.InvariantCulture) ?? "?"} mode={input.Mode?.ToString(CultureInfo.InvariantCulture) ?? "?"}"));

    /// <summary>The player pressed the mark key (#597): the marker's number and how far into the session it came, as
    /// hours, minutes and seconds, so a player's "about ten minutes in" finds it.</summary>
    public void Mark(int number) => Write(() =>
    {
        var session = TimeSpan.FromMilliseconds(millisecondsSinceStart());
        return Line($"mark number={number} session={(int)session.TotalHours}:{session.Minutes:00}:{session.Seconds:00}");
    });

    /// <summary>A screen entered or a focus moved and the mod said nothing (#597): the screen's class name, the index
    /// and the list's count, never text.</summary>
    public void Silent(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Write(() => Line(
            $"silent change={change.Change} screen={ClassName(change.ScreenClass)} index={change.Index} count={change.Count?.ToString(CultureInfo.InvariantCulture) ?? "?"}"));
    }

    /// <summary>The silent list reached its <paramref name="limit"/>; nothing more is listed this session.</summary>
    public void SilentListFull(int limit) =>
        Write(() => Line($"silent list full at {limit} entries; later silent screens aren't listed"));

    /// <summary>One line from the mod itself, such as the startup status with its hook counts, or an error, cleaned by
    /// <see cref="Scrubber"/> first: the folders and user name it was made with, typed names and memory dumps.</summary>
    public void Note(string line) => Write(() => Line($"{Scrubber.Scrub(line)}"));

    /// <summary>An error from the mod, redacted as <see cref="Note"/> is. An error may come just before a crash, so
    /// unlike every other line it's on disk, after everything queued before it, when this returns; a stalled disk
    /// holds the caller up for <see cref="ErrorWriteTimeout"/> at most.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A report log must never reach the game.")]
    public void Error(string line)
    {
        Note(line);
        try
        {
            WaitForWrites(ErrorWriteTimeout);
        }
        catch (Exception)
        {
            // An interrupted wait only means the line may land a moment later.
        }
    }

    /// <summary>The longest <see cref="Error"/> waits for the disk.</summary>
    public static readonly TimeSpan ErrorWriteTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>What a report log redacts: <see cref="LogScrubber.PathsToRedact"/>.</summary>
    public static IReadOnlyList<string> PathsToRedact(
        string modFolder, string configFolder, string? gameExecutable, string userProfile, string userName) =>
        LogScrubber.PathsToRedact(modFolder, configFolder, gameExecutable, userProfile, userName);

    /// <summary>Waits until every line logged so far is written; false if that takes longer than
    /// <paramref name="timeout"/>. It blocks, so it's only for bounded waits off the per-message path: <see cref="Error"/>,
    /// draining at exit, and tests. Timed by the monotonic clock, so a system clock change can't stretch it.</summary>
    public bool WaitForWrites(TimeSpan timeout)
    {
        var started = Stopwatch.GetTimestamp();
        lock (gate)
        {
            while (queue.Count > 0 || dropped > 0 || writing)
            {
                var remaining = timeout - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero || !Monitor.Wait(gate, remaining))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>A label in the shape the game's labels take: letters and digits joined by underscores, with at least
    /// one underscore (<c>MES_e00_002_001_004</c>, <c>Title_System_08</c>). Anything else, a one-word line or a path
    /// included, could be message text, so only its length is written.</summary>
    private static string Label(string? label) => label switch
    {
        null => "(unreadable)",
        { Length: <= MaxLabelLength } when IsLabelShaped(label) => label,
        _ => string.Create(CultureInfo.InvariantCulture, $"(not a label, {label.Length} chars)"),
    };

    /// <summary>A class name as the game's RTTI gives it (<c>RootMenuChoice@ope@ui@app</c>): ASCII letters, digits,
    /// <c>@</c>, <c>_</c>, <c>:</c>, <c>&lt;</c>, <c>&gt;</c> and <c>,</c>. Anything else, a space or a slash
    /// included, could be text or a path, so only its length is written.</summary>
    private static string ClassName(string? name) =>
        name is { Length: > 0 and <= MaxClassNameLength } && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '@' or '_' or ':' or '<' or '>' or ',')
            ? name
            : string.Create(CultureInfo.InvariantCulture, $"(not a class name, {name?.Length ?? 0} chars)");

    private static bool IsLabelShaped(string text)
    {
        var parts = text.Split('_');
        return parts.Length >= 2 && parts.All(part => part.Length > 0 && part.All(char.IsAsciiLetterOrDigit));
    }

    private string Line(FormattableString text) =>
        $"{millisecondsSinceStart().ToString(CultureInfo.InvariantCulture)} ms {text.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}";

    /// <summary>Builds one record on the caller's thread, so its time is the caller's, and queues it for the writer.
    /// Building it is inside the try too, so nothing a caller passes can make this throw.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A report log must never reach the game.")]
    private void Write(Func<string> record)
    {
        try
        {
            var line = record();
            lock (gate)
            {
                if (queue.Count >= MaxQueuedLines)
                {
                    dropped++;
                    return;
                }

                queue.Enqueue(line);
                writerThread ??= StartWriter();
                Monitor.PulseAll(gate);
            }
        }
        catch (Exception)
        {
            // A lost line in a report is better than a crashed game.
        }
    }

    /// <summary>The background writer, started with the first line. It lives as long as the process, as the log
    /// does, and being a background thread it never keeps the game from exiting.</summary>
    private Thread StartWriter()
    {
        var thread = new Thread(WriteQueued) { IsBackground = true, Name = "Metaphor report log", Priority = ThreadPriority.BelowNormal };
        thread.Start();
        return thread;
    }

    /// <summary>Takes everything queued, writes it with no lock held, and repeats. All of it is inside the try, as an
    /// exception escaping a thread would end the game.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A report log must never reach the game.")]
    private void WriteQueued()
    {
        while (true)
        {
            try
            {
                string[] lines;
                long lost;
                lock (gate)
                {
                    while (queue.Count == 0 && dropped == 0)
                    {
                        Monitor.Wait(gate);
                    }

                    lines = [.. queue];
                    queue.Clear();
                    lost = dropped;
                    dropped = 0;
                    writing = true;
                }

                foreach (var line in lines)
                {
                    writer.Write(line);
                }

                if (lost > 0)
                {
                    writer.Write(Line($"({lost} lines dropped: the log fell behind)"));
                }

                writer.Flush();
            }
            catch (Exception)
            {
                // A lost line in a report is better than a crashed game.
            }
            finally
            {
                lock (gate)
                {
                    writing = false;
                    Monitor.PulseAll(gate);
                }
            }
        }
    }
}
