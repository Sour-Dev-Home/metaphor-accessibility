using System.Diagnostics.CodeAnalysis;

namespace Metaphor.Core.Logging;

/// <summary>The part of the mod a log line is from; its prefix is <see cref="ModLog.Prefix"/>.</summary>
public enum LogArea
{
    /// <summary>The mod's start: the build line and each subsystem's result.</summary>
    Startup,

    /// <summary>The screen reader backend.</summary>
    Speech,

    /// <summary>The settings file.</summary>
    Settings,

    /// <summary>The sound cues.</summary>
    Sounds,

    /// <summary>The hook loader: each hook and game global, and the signature scan it runs.</summary>
    Hooks,

    /// <summary>The update check.</summary>
    Updates,

    /// <summary>The game's tables the mod reads from its archive.</summary>
    Data,

    /// <summary>The dev signature scan (#21).</summary>
    Scan,

    /// <summary>Values the mod has no mapping for yet (<see cref="UnknownValues"/>).</summary>
    Unknown,

    /// <summary>The Debug-only live inspector (#291).</summary>
    Inspector,
}

/// <summary>The mod's own log lines (#824), as the loader's console and the report log get them. Each line starts with
/// its area's prefix (<c>hooks: </c>), so one area's lines are found with one search; each is cleaned by the
/// <see cref="LogScrubber"/> before either log sees it, as Reloaded-II keeps its console in a log file of its own
/// (docs/raw-sources/reloaded-ii-launch-and-logs-2026-11-13.md); and each subsystem's startup result is one
/// <c>startup: </c> line. Safe to use from several threads; never throws.</summary>
/// <remarks>A result line per subsystem, a prefix per area and timings for slow steps are ideas from Arcana's log, seen
/// in a log a player shared on 2026-11-23 (ChipsAhoiMcCoy, https://github.com/ChipsAhoiMcCoy/arcana, Zlib); no code was
/// copied (CREDITS.md).</remarks>
public sealed class ModLog
{
    private readonly Action<string> console;
    private readonly ReportLog? report;
    private readonly HashSet<string> results = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <param name="console">The loader's console, which gets each cleaned line.</param>
    /// <param name="report">The report log, or null when it couldn't be opened.</param>
    /// <param name="scrubber">What cleans every line: the report log's, so both logs clean the same names.</param>
    public ModLog(Action<string> console, ReportLog? report, LogScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(scrubber);
        this.console = console;
        this.report = report;
        Scrubber = scrubber;
        Unknowns = new UnknownValues(For(LogArea.Unknown));
    }

    /// <summary>What cleans every line; a feature that reads a name the player typed adds it here.</summary>
    public LogScrubber Scrubber { get; }

    /// <summary>Each value the mod has no mapping for, once per session, in the <c>unknown</c> area.</summary>
    public UnknownValues Unknowns { get; }

    /// <summary>The prefix every line of <paramref name="area"/> starts with: its name in lower case and a colon.</summary>
    public static string Prefix(LogArea area) => area switch
    {
        LogArea.Startup => "startup: ",
        LogArea.Speech => "speech: ",
        LogArea.Settings => "settings: ",
        LogArea.Sounds => "sounds: ",
        LogArea.Hooks => "hooks: ",
        LogArea.Updates => "updates: ",
        LogArea.Data => "data: ",
        LogArea.Scan => "scan: ",
        LogArea.Unknown => "unknown: ",
        LogArea.Inspector => "inspector: ",
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, null),
    };

    /// <summary>Where <paramref name="area"/>'s ordinary lines go.</summary>
    public Action<string> For(LogArea area)
    {
        var prefix = Prefix(area);
        return line => Write(prefix + line, error: false);
    }

    /// <summary>Where <paramref name="area"/>'s errors go: on disk before the call returns, in case a crash follows
    /// (<see cref="ReportLog.Error"/>).</summary>
    public Action<string> ErrorsFor(LogArea area)
    {
        var prefix = Prefix(area);
        return line => Write(prefix + line, error: true);
    }

    /// <summary>One subsystem's startup result, written as "subsystem: result", such as <c>hooks: 51 of 51 installed in
    /// 120 ms</c>: a <c>startup: </c> line for the first result of each subsystem (the text before the first colon)
    /// only, so the summary has one line per subsystem. A <paramref name="failed"/> result is written as an error.
    /// </summary>
    public void Result(string line, bool failed = false)
    {
        line ??= "";
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        var subsystem = colon < 0 ? line : line[..colon];
        lock (gate)
        {
            if (!results.Add(subsystem))
            {
                return;
            }
        }
        Write(Prefix(LogArea.Startup) + line, failed);
    }

    [SuppressMessage("Design", "CA1031", Justification = "A log must never reach the game.")]
    private void Write(string line, bool error)
    {
        var clean = Scrubber.Scrub(line);
        try
        {
            console(clean);
        }
        catch (Exception)
        {
            // The report log still gets it.
        }
        if (error)
        {
            report?.Error(clean);
        }
        else
        {
            report?.Note(clean);
        }
    }
}
