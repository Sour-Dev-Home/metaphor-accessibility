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
    private readonly SettingCatalogue features;
    private readonly Action<string> log;
    private readonly bool testingOn;
    private readonly Lock gate = new();
    private readonly HashSet<string> reportedClashes = new(StringComparer.Ordinal);
    private volatile IReadOnlyList<KeyEntry>? keys;
    private volatile int checkedKeysCount;
    private volatile ModSettings current = ModSettings.Defaults;

    /// <param name="announcer">Where a changed value is said.</param>
    /// <param name="features">The features' on/off settings, from the feature modules (#784).</param>
    /// <param name="log">Where a file that can't be used is reported (the Debug log).</param>
    /// <param name="testingOn">Whether this is a session that turns every testing feature on (#783).</param>
    /// <param name="keys">The mod's listed keys, for catching duplicate bindings (#755); can also be attached through
    /// <see cref="WatchKeys"/> as modules register.</param>
    public SettingsReloader(
        IAnnouncer announcer, SettingCatalogue features, Action<string> log, bool testingOn = false,
        IReadOnlyList<KeyEntry>? keys = null)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(log);
        this.announcer = announcer;
        this.features = features;
        this.log = log;
        this.testingOn = testingOn;
        this.keys = keys;
        current = current.WithTesting(testingOn);
    }

    /// <summary>The settings in force now.</summary>
    public ModSettings Current => current;

    /// <summary>The features' settings and sliders the file is read for, for the settings menu (#754).</summary>
    public SettingCatalogue Catalogue => features;

    /// <summary>Watches <paramref name="listed"/> for duplicate key or pad-chord bindings (#755), checked once every
    /// module has finished adding to it and on every reload.</summary>
    public void WatchKeys(IReadOnlyList<KeyEntry> listed)
    {
        ArgumentNullException.ThrowIfNull(listed);
        lock (gate)
        {
            keys = listed;
            checkedKeysCount = 0;
        }
    }

    /// <summary>Checks the watched keys for duplicate bindings (#755) if new keys were registered since the last check,
    /// leaving the later action unbound and speaking and logging each new clash once.</summary>
    public void CheckKeys()
    {
        if (keys is not { Count: > 0 } listed || checkedKeysCount == listed.Count)
        {
            return;
        }
        IReadOnlyList<KeyClash> fresh;
        lock (gate)
        {
            if (keys is not { Count: > 0 } now || checkedKeysCount == now.Count)
            {
                return;
            }
            var updated = current;
            fresh = ApplyClashes(ref updated, now);
            current = updated;
            checkedKeysCount = now.Count;
        }
        foreach (var clash in fresh)
        {
            announcer.Announce(new Announcement(clash.Spoken, Interrupt: false));
        }
    }

    /// <summary>Applies the file as read at start (null when there is none), saying nothing except any duplicate key
    /// binding (#755).</summary>
    public void Load(string? json)
    {
        IReadOnlyList<KeyClash> fresh = [];
        lock (gate)
        {
            var next = ModSettings.Parse(json, features, log).WithTesting(testingOn);
            if (keys is { Count: > 0 } listed)
            {
                fresh = ApplyClashes(ref next, listed);
                checkedKeysCount = listed.Count;
            }
            else
            {
                checkedKeysCount = 0;
            }
            current = next;
        }
        foreach (var clash in fresh)
        {
            announcer.Announce(new Announcement(clash.Spoken, Interrupt: false));
        }
    }

    /// <summary>Applies the file after a change (null when it is gone) and announces each value that differs from the
    /// one in force, in the file's documented order, each after the one before, plus any new key clash (#755).</summary>
    public void Reload(string? json)
    {
        lock (gate)
        {
            var next = ModSettings.Parse(json, features, log).WithTesting(testingOn);
            IReadOnlyList<KeyClash> fresh = [];
            if (keys is { Count: > 0 } listed)
            {
                fresh = ApplyClashes(ref next, listed);
                checkedKeysCount = listed.Count;
            }
            else
            {
                checkedKeysCount = 0;
            }
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
            foreach (var feature in features.Features.Where(feature => next.IsOn(feature) != current.IsOn(feature)))
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
            said.AddRange(fresh.Select(clash => clash.Spoken));
            current = next;
            for (var i = 0; i < said.Count; i++)
            {
                announcer.Announce(new Announcement(said[i], Interrupt: i == 0));
            }
        }
    }

    /// <summary>The one gate on a feature's speech (#661): what goes through it reaches <paramref name="inner"/> only
    /// while <paramref name="feature"/> is on, read at each announcement.</summary>
    public IAnnouncer Speech(Enum feature, IAnnouncer inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new SpeechGate(() => current.IsOn(feature), inner);
    }

    /// <summary>The one gate on a feature's keys (#661): while <paramref name="feature"/> is off, read at each input
    /// read, every read reaches the game untouched and <paramref name="inner"/> never sees it.</summary>
    public IInputControls Input(Enum feature, IInputControls inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return new InputGate(() => current.IsOn(feature), inner);
    }

    /// <summary>As <see cref="Input(Enum, IInputControls)"/>, for controls that also take each read of the
    /// dialogue window: those reach them whatever the setting, so they know the window's state when turned back on.
    /// </summary>
    public IWindowInputControls Input(Enum feature, IWindowInputControls inner)
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
            CheckKeys();
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

    // Called under gate: strips clashed later bindings from settings, logs new clashes and keeps track of active ones.
    private List<KeyClash> ApplyClashes(ref ModSettings settings, IReadOnlyList<KeyEntry> listed)
    {
        var clashes = KeyClashes.Find(settings, listed);
        if (clashes.Count > 0)
        {
            settings = settings.WithoutBindings(clashes.Select(clash => clash.UnboundPath));
        }
        reportedClashes.RemoveWhere(id => clashes.All(clash => clash.Id != id));
        List<KeyClash> fresh = [];
        foreach (var clash in clashes)
        {
            if (reportedClashes.Add(clash.Id))
            {
                log(clash.LogLine);
                fresh.Add(clash);
            }
        }
        return fresh;
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
