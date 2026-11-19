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

    /// <summary>What every setting is without a usable file.</summary>
    public static ModSettings Defaults { get; } = new();

    /// <summary>How voiced dialogue lines are spoken.</summary>
    public VoicedLinesMode VoicedLines { get; init; } = VoicedLinesMode.Auto;

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
            return new ModSettings { VoicedLines = ReadMode(document.RootElement, log) };
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
