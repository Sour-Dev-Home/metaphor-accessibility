using System.Globalization;
using Metaphor.Core.Cues;

namespace Metaphor.Core.Settings;

/// <summary>A change to the settings file: <see cref="Path"/>, object names from the top, set to <see cref="Json"/>.
/// </summary>
public sealed record SettingChange(IReadOnlyList<string> Path, string Json);

/// <summary>One setting a player can change, as the settings menu lists it (#754): its section, the name it is said
/// by, what it does, its value said aloud, and the change a press makes. The rows come from the same declarations the
/// settings reference is generated from (ADR 0022), so the menu and the page can't drift apart.</summary>
public abstract record PlayerSetting(string Section, string Spoken, string Description)
{
    /// <summary>A feature still waiting for its by-ear check (#783): the menu says "testing".</summary>
    public bool Testing { get; init; }

    /// <summary>Its value now, as said: "on", "auto", "50".</summary>
    public abstract string Value(ModSettings settings);

    /// <summary>The change moving it <paramref name="steps"/> along makes, right positive; null when it can't change
    /// here (a key binding, or a number already at its end).</summary>
    public abstract SettingChange? Change(ModSettings settings, int steps);
}

/// <summary>A setting that is <c>true</c> or <c>false</c>: any move turns it over.</summary>
public sealed record ToggleSetting(
    string Section, string Spoken, string Description, IReadOnlyList<string> Path, Func<ModSettings, bool> Read)
    : PlayerSetting(Section, Spoken, Description)
{
    public override string Value(ModSettings settings) => Read(settings) ? "on" : "off";

    public override SettingChange? Change(ModSettings settings, int steps) =>
        steps == 0 ? null : new(Path, Read(settings) ? "false" : "true");
}

/// <summary>A setting that is one of a few words (<paramref name="Names"/>, as the file writes them, said as
/// <paramref name="SaidAs"/>): a move steps through them and wraps.</summary>
public sealed record ChoiceSetting(
    string Section, string Spoken, string Description, IReadOnlyList<string> Path, IReadOnlyList<string> Names,
    IReadOnlyList<string> SaidAs, Func<ModSettings, int> Read)
    : PlayerSetting(Section, Spoken, Description)
{
    public override string Value(ModSettings settings) => SaidAs[Read(settings)];

    public override SettingChange? Change(ModSettings settings, int steps)
    {
        if (steps == 0)
        {
            return null;
        }
        var next = (((Read(settings) + Math.Sign(steps)) % Names.Count) + Names.Count) % Names.Count;
        return new(Path, $"\"{Names[next]}\"");
    }
}

/// <summary>A whole number from <paramref name="Min"/> to <paramref name="Max"/>, moved by <paramref name="Step"/>.
/// </summary>
public sealed record NumberSetting(
    string Section, string Spoken, string Description, IReadOnlyList<string> Path, int Min, int Max, int Step,
    Func<ModSettings, int> Read)
    : PlayerSetting(Section, Spoken, Description)
{
    public override string Value(ModSettings settings) => Read(settings).ToString(CultureInfo.InvariantCulture);

    public override SettingChange? Change(ModSettings settings, int steps)
    {
        var now = Read(settings);
        var next = Math.Clamp(now + (steps * Step), Min, Max);
        return next == now ? null : new(Path, next.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>A key binding, said but not changed here (#754's decision: "edit settings.json to change" until #564).
/// </summary>
public sealed record KeySetting(string Section, string Spoken, Func<ModSettings, string> Read)
    : PlayerSetting(Section, Spoken, "Keys are changed in settings.json, in its keys object.")
{
    public override string Value(ModSettings settings) => Read(settings);

    public override SettingChange? Change(ModSettings settings, int steps) => null;
}

/// <summary>The settings a player can change, grouped and ordered as the settings reference
/// (<c>docs/players/reference/settings.md</c>) lists them (#754).</summary>
public static class PlayerSettings
{
    /// <summary>The general settings' section.</summary>
    public const string General = "General";

    /// <summary>The cues' section.</summary>
    public const string Sounds = "Sounds";

    /// <summary>The sliders' section.</summary>
    public const string Limits = "Limits";

    /// <summary>The key bindings' section.</summary>
    public const string Keys = "Keys";

    private static readonly string[] VoicedNames = ["auto", "nameOnly", "fullLine", "nothing"];

    /// <summary>The settings at the top of the file. <c>welcomed</c> is the mod's own flag, so it isn't one.</summary>
    public static IReadOnlyList<PlayerSetting> GeneralSettings { get; } =
    [
        new ChoiceSetting(General, "Voiced lines", "What is said for lines the actors speak: auto, name only, full " +
            "line or nothing.", ["voicedLines"], VoicedNames, ["auto", "name only", "full line", "nothing"],
            settings => (int)settings.VoicedLines),
        new ToggleSetting(General, "Update check", "Whether the mod asks GitHub once per launch for a newer release.",
            ["checkForUpdates"], settings => settings.CheckForUpdates),
        new ToggleSetting(General, "Sound cues", "Whether the mod plays its sound cues at all.", ["soundCues"],
            settings => settings.SoundCues),
        new NumberSetting(General, "Sound cue volume", "How loud the sound cues are, a whole number from 0, silent, " +
            "to 100.", ["soundCueVolume"], 0, 100, 5, settings => settings.SoundCueVolume),
    ];

    /// <summary>Every row, in the reference's order: the general settings, each feature group's toggles, the sounds,
    /// the limits, then <paramref name="keys"/>, read-only. A feature in <paramref name="hidden"/> isn't listed.
    /// </summary>
    public static IReadOnlyList<PlayerSetting> Rows(
        SettingCatalogue catalogue, IEnumerable<PlayerSetting> keys, Func<Enum, bool>? hidden = null)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(keys);
        List<PlayerSetting> rows = [.. GeneralSettings];
        var features = catalogue.Features.Where(feature => hidden?.Invoke(feature) != true)
            .Select(feature => (Feature: feature, Key: SettingKey.Of(feature)));
        // Grouped in the order the groups first appear, as the reference's sections are.
        foreach (var group in features.GroupBy(pair => pair.Key.Group))
        {
            var section = SectionOf(group.Key);
            rows.AddRange(group.Select(pair => new ToggleSetting(
                section, pair.Key.Spoken, pair.Key.Description, [pair.Key.Group, pair.Key.Name],
                settings => settings.IsOn(pair.Feature))
            {
                Testing = pair.Key.Testing,
            }));
        }
        foreach (var cue in Enum.GetValues<Cue>())
        {
            var key = SettingKey.Of(cue);
            rows.Add(new ToggleSetting(Sounds, key.Spoken, key.Description, ["sounds", key.Name, "on"],
                settings => settings.Sound(cue).On));
            rows.Add(new NumberSetting(Sounds, $"{key.Spoken} volume", $"How loud it is, as a share of the sound cue " +
                "volume.", ["sounds", key.Name, "volume"], 0, 100, 5, settings => settings.Sound(cue).Volume));
        }
        rows.AddRange(catalogue.Limits.Select(limit => new NumberSetting(
            Limits, limit.Spoken, limit.Description, ["limits", limit.Group, limit.Name], limit.Min, limit.Max,
            limit.Step, limit.From)));
        rows.AddRange(keys);
        return rows;
    }

    /// <summary>The section a feature group's toggles sit in: its name with a capital, as the reference's headings.
    /// </summary>
    public static string SectionOf(string group)
    {
        ArgumentException.ThrowIfNullOrEmpty(group);
        return string.Concat(char.ToUpperInvariant(group[0]).ToString(), group[1..]);
    }
}
