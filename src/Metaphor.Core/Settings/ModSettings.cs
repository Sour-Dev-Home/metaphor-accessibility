using System.Text.Json;
using Metaphor.Core.Cues;

namespace Metaphor.Core.Settings;

/// <summary>What the player says about lines the game's voice actors speak (#57, #104).</summary>
public enum VoicedLinesMode
{
    /// <summary>The default: the speaker's name for English voice, the full line for Japanese voice, and the full
    /// line while the voice language is unknown.</summary>
    Auto,

    /// <summary>Only the speaker's name; a voiced line with no speaker is silent.</summary>
    NameOnly,

    /// <summary>The speaker and the line, as for an unvoiced line.</summary>
    FullLine,

    /// <summary>Nothing.</summary>
    Nothing,
}

/// <summary>The settings file's contents (#104), each setting defaulting when missing or unusable.</summary>
public sealed record ModSettings
{
    /// <summary>The file's name in the mod's user-config folder.</summary>
    public const string FileName = "settings.json";

    private const string VoicedLinesKey = "voicedLines";
    private const string CheckForUpdatesKey = "checkForUpdates";
    private const string SoundCuesKey = "soundCues";
    private const string SoundCueVolumeKey = "soundCueVolume";
    private const string SoundsKey = "sounds";
    private const string KeysKey = "keys";

    /// <summary>What every setting is without a usable file.</summary>
    public static ModSettings Defaults { get; } = new();

    /// <summary>How voiced dialogue lines are spoken.</summary>
    public VoicedLinesMode VoicedLines { get; init; } = VoicedLinesMode.Auto;

    /// <summary>Whether the mod asks GitHub once per launch for a newer release (#576, ADR 0018). On by default; false
    /// turns the request off.</summary>
    public bool CheckForUpdates { get; init; } = true;

    /// <summary>Whether the mod plays its sound cues (#636, ADR 0019). On by default.</summary>
    public bool SoundCues { get; init; } = true;

    /// <summary>How loud the sound cues are, 0 (silent) to 100 (#636). 50 by default.</summary>
    public int SoundCueVolume { get; init; } = 50;

    // Only what the file sets: a feature or cue it doesn't name is on at full volume, a binding it doesn't name is the
    // feature's default. Compared by value, so the same file read twice gives equal settings.
    private SettingMap<Feature, bool> Toggles { get; init; } = SettingMap<Feature, bool>.Empty;

    private SettingMap<Cue, CueSetting> Sounds { get; init; } = SettingMap<Cue, CueSetting>.Empty;

    private SettingMap<string, int> Bindings { get; init; } = SettingMap<string, int>.Empty;

    /// <summary>Whether <paramref name="feature"/> speaks, plays and takes its keys (#661). On unless the file turns
    /// it off.</summary>
    public bool IsOn(Feature feature) => Toggles.Get(feature, true);

    /// <summary>Whether <paramref name="cue"/> plays, and its volume as a share of <see cref="SoundCueVolume"/>
    /// (#661).</summary>
    public CueSetting Sound(Cue cue) => Sounds.Get(cue, CueSetting.Default);

    /// <summary>The code the file binds to <paramref name="name"/> in <paramref name="group"/> of its <c>keys</c>
    /// object: a virtual-key code for a name ending in <c>Key</c>, a pad mask for one ending in <c>Button</c> or
    /// <c>Buttons</c>, 0 for none. <paramref name="fallback"/>, the feature's default, when the file has no usable
    /// value (#661).</summary>
    public int Binding(string group, string name, int fallback) => Bindings.Get($"{group}.{name}", fallback);

    /// <summary>Whether <paramref name="other"/> binds the same keys, for saying when they changed.</summary>
    public bool SameBindings(ModSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Bindings.Equals(other.Bindings);
    }

    /// <summary>Reads the file's text; null means no file. Anything unusable, whether the whole file or one setting,
    /// gives the default and a line to <paramref name="log"/>. Keys it doesn't know are ignored, so later settings can
    /// share the file.</summary>
    public static ModSettings Parse(string? json, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (json is null)
        {
            return Defaults;
        }
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                log("settings file is not a JSON object; using defaults");
                return Defaults;
            }
            return new ModSettings
            {
                VoicedLines = ReadMode(document.RootElement, log),
                CheckForUpdates = ReadCheckForUpdates(document.RootElement, log),
                SoundCues = ReadSoundCues(document.RootElement, log),
                SoundCueVolume = ReadSoundCueVolume(document.RootElement, log),
                Toggles = ReadToggles(document.RootElement, log),
                Sounds = ReadSounds(document.RootElement, log),
                Bindings = ReadBindings(document.RootElement, log),
            };
        }
        // A lone surrogate, raw or escaped, is ArgumentException or InvalidOperationException rather than JsonException
        // (found by the property tests, #443).
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            log($"settings file is not valid JSON ({e.Message}); using defaults");
            return Defaults;
        }
    }

    private static VoicedLinesMode ReadMode(JsonElement root, Action<string> log)
    {
        if (!root.TryGetProperty(VoicedLinesKey, out var value))
        {
            return Defaults.VoicedLines;
        }
        // A mode's name in any case, and nothing else: Enum.TryParse would also take "3", " auto" and "auto, nameOnly"
        // (found by the property tests, #443).
        if (value.ValueKind == JsonValueKind.String && Named(value.GetString()) is { } mode)
        {
            return mode;
        }
        log($"settings file: \"{VoicedLinesKey}\" is not one of auto, nameOnly, fullLine, nothing; using auto");
        return Defaults.VoicedLines;
    }

    // JSON true or false only: a string or a number is a typo, so the check stays on and the log says why.
    private static bool ReadCheckForUpdates(JsonElement root, Action<string> log)
    {
        if (!root.TryGetProperty(CheckForUpdatesKey, out var value))
        {
            return Defaults.CheckForUpdates;
        }
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }
        log($"settings file: \"{CheckForUpdatesKey}\" is not true or false; using true");
        return Defaults.CheckForUpdates;
    }

    // JSON true or false only: a string or a number is a typo, so cues stay on and the log says why.
    private static bool ReadSoundCues(JsonElement root, Action<string> log)
    {
        if (!root.TryGetProperty(SoundCuesKey, out var value))
        {
            return Defaults.SoundCues;
        }
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }
        log($"settings file: \"{SoundCuesKey}\" is not true or false; using true");
        return Defaults.SoundCues;
    }

    // A JSON whole number from 0 to 100; a fraction, a string or anything outside the range gives the default.
    private static int ReadSoundCueVolume(JsonElement root, Action<string> log)
    {
        if (!root.TryGetProperty(SoundCueVolumeKey, out var value))
        {
            return Defaults.SoundCueVolume;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var volume) && volume is >= 0 and <= 100)
        {
            return volume;
        }
        log($"settings file: \"{SoundCueVolumeKey}\" is not a whole number from 0 to 100; using {Defaults.SoundCueVolume}");
        return Defaults.SoundCueVolume;
    }

    // Each feature's true or false in its group's object; anything else leaves it on and says why.
    private static SettingMap<Feature, bool> ReadToggles(JsonElement root, Action<string> log)
    {
        Dictionary<Feature, bool> read = [];
        foreach (var group in Enum.GetValues<Feature>().GroupBy(feature => SettingKey.Of(feature).Group))
        {
            if (Group(root, group.Key, log) is not { } values)
            {
                continue;
            }
            foreach (var feature in group)
            {
                var key = SettingKey.Of(feature);
                if (!values.TryGetProperty(key.Name, out var value))
                {
                    continue;
                }
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    read[feature] = value.GetBoolean();
                }
                else
                {
                    log($"settings file: \"{key.Path}\" is not true or false; using true");
                }
            }
        }
        return new(read);
    }

    // Each cue's { "on": true or false, "volume": 0 to 100 }; a value that can't be used keeps that value's default.
    private static SettingMap<Cue, CueSetting> ReadSounds(JsonElement root, Action<string> log)
    {
        Dictionary<Cue, CueSetting> read = [];
        if (Group(root, SoundsKey, log) is not { } values)
        {
            return new(read);
        }
        foreach (var cue in Enum.GetValues<Cue>())
        {
            var key = SettingKey.Of(cue);
            if (!values.TryGetProperty(key.Name, out var value))
            {
                continue;
            }
            if (value.ValueKind != JsonValueKind.Object)
            {
                log($"settings file: \"{key.Path}\" is not an object like {{ \"on\": true, \"volume\": 100 }}; using the default");
                continue;
            }
            var setting = CueSetting.Default;
            if (value.TryGetProperty("on", out var on))
            {
                if (on.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    setting = setting with { On = on.GetBoolean() };
                }
                else
                {
                    log($"settings file: \"{key.Path}.on\" is not true or false; using true");
                }
            }
            if (value.TryGetProperty("volume", out var volume))
            {
                if (volume.ValueKind == JsonValueKind.Number && volume.TryGetInt32(out var number) && number is >= 0 and <= 100)
                {
                    setting = setting with { Volume = number };
                }
                else
                {
                    log($"settings file: \"{key.Path}.volume\" is not a whole number from 0 to 100; using 100");
                }
            }
            read[cue] = setting;
        }
        return new(read);
    }

    // Every group in "keys", whatever feature it belongs to, so a feature added later reads its own; a name the
    // feature doesn't use is never asked for.
    private static SettingMap<string, int> ReadBindings(JsonElement root, Action<string> log)
    {
        Dictionary<string, int> read = new(StringComparer.Ordinal);
        if (Group(root, KeysKey, log) is not { } groups)
        {
            return new(read);
        }
        foreach (var group in groups.EnumerateObject())
        {
            if (group.Value.ValueKind != JsonValueKind.Object)
            {
                log($"settings file: \"{KeysKey}.{group.Name}\" is not an object; using the default keys");
                continue;
            }
            foreach (var binding in group.Value.EnumerateObject())
            {
                var buttons = BindingNames.IsButtons(binding.Name);
                if (!buttons && !BindingNames.IsKey(binding.Name))
                {
                    continue;
                }
                var text = binding.Value.ValueKind == JsonValueKind.String ? binding.Value.GetString() : null;
                if ((buttons ? BindingNames.Buttons(text) : BindingNames.Key(text)) is { } code)
                {
                    read[$"{group.Name}.{binding.Name}"] = code;
                }
                else
                {
                    log(buttons
                        ? $"settings file: \"{KeysKey}.{group.Name}.{binding.Name}\" is not pad buttons like \"LB+RB\"; using the default"
                        : $"settings file: \"{KeysKey}.{group.Name}.{binding.Name}\" is not a key like \"R\" or \"F5\"; using the default");
                }
            }
        }
        return new(read);
    }

    // A group's object, or null when the file has none or it isn't an object (said in the log).
    private static JsonElement? Group(JsonElement root, string name, Action<string> log)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }
        log($"settings file: \"{name}\" is not an object; using its defaults");
        return null;
    }

    private static VoicedLinesMode? Named(string? text)
    {
        foreach (var mode in Enum.GetValues<VoicedLinesMode>())
        {
            if (string.Equals(mode.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                return mode;
            }
        }
        return null;
    }
}
