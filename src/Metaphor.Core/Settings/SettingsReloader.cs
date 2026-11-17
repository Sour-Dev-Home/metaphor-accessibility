using Metaphor.Core.Announcements;

namespace Metaphor.Core.Settings;

/// <summary>Holds the current settings and applies the settings file's text as it is read, at start and again when
/// the file changes (#104). A changed voiced-lines value is announced. Safe to use from several threads: the file
/// watcher calls it from another one.</summary>
public sealed class SettingsReloader
{
    private readonly IAnnouncer announcer;
    private readonly Action<string> log;
    private readonly Lock gate = new();
    private volatile ModSettings current = ModSettings.Defaults;

    /// <param name="announcer">Where a changed value is said.</param>
    /// <param name="log">Where a file that can't be used is reported (the Debug log).</param>
    public SettingsReloader(IAnnouncer announcer, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(log);
        this.announcer = announcer;
        this.log = log;
    }

    /// <summary>The settings in force now.</summary>
    public ModSettings Current => current;

    /// <summary>Applies the file as read at start (null when there is none), saying nothing.</summary>
    public void Load(string? json)
    {
        lock (gate)
        {
            current = ModSettings.Parse(json, log);
        }
    }

    /// <summary>Applies the file after a change (null when it is gone) and announces the new voiced-lines value when
    /// it differs from the one in force.</summary>
    public void Reload(string? json)
    {
        lock (gate)
        {
            var next = ModSettings.Parse(json, log);
            var changed = next.VoicedLines != current.VoicedLines;
            current = next;
            if (changed)
            {
                announcer.Announce(new Announcement($"Voiced lines: {Describe(next.VoicedLines)}.", Interrupt: true));
            }
        }
    }

    private static string Describe(VoicedLinesMode mode) => mode switch
    {
        VoicedLinesMode.Auto => "auto",
        VoicedLinesMode.NameOnly => "name only",
        VoicedLinesMode.FullLine => "full line",
        VoicedLinesMode.Nothing => "nothing",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
