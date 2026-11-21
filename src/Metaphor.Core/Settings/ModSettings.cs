using System.Text.Json;

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
