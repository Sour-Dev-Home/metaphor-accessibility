using Metaphor.Core.Announcements;

namespace Metaphor.Core.Settings;

/// <summary>Holds the current settings and applies the settings file's text as it is read, at start and again when
/// the file changes (#104). A changed value is announced. Safe to use from several threads: the file
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

    /// <summary>Applies the file after a change (null when it is gone) and announces each value that differs from the
    /// one in force: voiced lines, then the update check, the second queued after the first.</summary>
    public void Reload(string? json)
    {
        lock (gate)
        {
            var next = ModSettings.Parse(json, log);
            List<string> said = [];
            if (next.VoicedLines != current.VoicedLines)
            {
                said.Add($"Voiced lines: {Describe(next.VoicedLines)}.");
            }
            if (next.CheckForUpdates != current.CheckForUpdates)
            {
                said.Add($"Update check: {(next.CheckForUpdates ? "on" : "off")}.");
            }
            current = next;
            for (var i = 0; i < said.Count; i++)
            {
                announcer.Announce(new Announcement(said[i], Interrupt: i == 0));
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
