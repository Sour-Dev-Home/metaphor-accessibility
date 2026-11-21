using Metaphor.Core.Announcements;
using Metaphor.Core.Cues;
using Metaphor.Core.Input;

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
    /// one in force, in the file's documented order, each after the one before.</summary>
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
            if (next.SoundCues != current.SoundCues)
            {
                said.Add($"Sound cues: {(next.SoundCues ? "on" : "off")}.");
            }
            if (next.SoundCueVolume != current.SoundCueVolume)
            {
                said.Add($"Sound cue volume: {next.SoundCueVolume}.");
            }
            foreach (var feature in Enum.GetValues<Feature>().Where(feature => next.IsOn(feature) != current.IsOn(feature)))
            {
                said.Add($"{SettingKey.Of(feature).Spoken}: {OnOff(next.IsOn(feature))}.");
            }
            foreach (var cue in Enum.GetValues<Cue>())
            {
                var (was, now) = (current.Sound(cue), next.Sound(cue));
                if (now.On != was.On)
                {
                    said.Add($"{SettingKey.Of(cue).Spoken}: {OnOff(now.On)}.");
                }
                if (now.Volume != was.Volume)
                {
                    said.Add($"{SettingKey.Of(cue).Spoken} volume: {now.Volume}.");
                }
            }
            if (!next.SameBindings(current))
            {
                said.Add("Keys changed.");
            }
            current = next;
            for (var i = 0; i < said.Count; i++)
            {
                announcer.Announce(new Announcement(said[i], Interrupt: i == 0));
            }
        }
    }

    /// <summary>The one gate on a feature's speech (#661): what goes through it reaches <paramref name="inner"/> only
    /// while <paramref name="feature"/> is on, read at each announcement.</summary>
    public IAnnouncer Speech(Feature feature, IAnnouncer inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new SpeechGate(() => current.IsOn(feature), inner);
    }

    /// <summary>The one gate on a feature's keys (#661): while <paramref name="feature"/> is off, read at each input
    /// read, every read reaches the game untouched and <paramref name="inner"/> never sees it.</summary>
    public IInputControls Input(Feature feature, IInputControls inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new InputGate(() => current.IsOn(feature), inner);
    }

    /// <summary>As <see cref="Input(Feature, IInputControls)"/>, for controls that also take each read of the
    /// dialogue window: those reach them whatever the setting, so they know the window's state when turned back on.
    /// </summary>
    public IWindowInputControls Input(Feature feature, IWindowInputControls inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new InputGate(() => current.IsOn(feature), inner);
    }

    /// <summary>A value worked out from the settings in force, such as a feature's key bindings (#661): worked out
    /// again only after the file changes, so a read on every input frame costs a reference compare.</summary>
    public Func<T> Live<T>(Func<ModSettings, T> make)
    {
        ArgumentNullException.ThrowIfNull(make);
        // A reference, so a thread reading it while another replaces it sees the old pair or the new one, never half.
        Tuple<ModSettings, T>? made = null;
        return () =>
        {
            var now = current;
            if (Volatile.Read(ref made) is { } known && ReferenceEquals(known.Item1, now))
            {
                return known.Item2;
            }
            var value = make(now);
            Volatile.Write(ref made, Tuple.Create(now, value));
            return value;
        };
    }

    private static string OnOff(bool on) => on ? "on" : "off";

    private sealed class SpeechGate(Func<bool> on, IAnnouncer inner) : IAnnouncer
    {
        public void Announce(Announcement announcement)
        {
            if (on())
            {
                inner.Announce(announcement);
            }
        }
    }

    private sealed class InputGate(Func<bool> on, IInputControls inner) : IWindowInputControls
    {
        public int Pad(int pad, int buttons, bool made) => on() ? inner.Pad(pad, buttons, made) : buttons;

        public IReadOnlyList<int> Keys(IReadOnlyList<int> keys) => on() ? inner.Keys(keys) : keys;

        public void WindowRead(long? currentMessage) => (inner as IWindowInputControls)?.WindowRead(currentMessage);
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
