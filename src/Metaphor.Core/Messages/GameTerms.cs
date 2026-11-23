using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Logging;

namespace Metaphor.Core.Messages;

/// <summary>The game-term and icon tags (#108, #109), filled under #83's rule: a value the mod can find replaces the
/// tag, and any other says a stand-in for its kind ("item", "button") and is reported. A term tag's arguments are a
/// number and a form: form 2 shows the name; an item's or a skill's form 1 is an icon the text names after it, so it says
/// nothing. Which
/// numbers are pinned is docs/findings/placeholder-tags-name-file-positions.md: a skill's and a condition's number is
/// its position in its file on game build 548ddc95… (Steam build 18330018); items, classes, attributes, values and
/// icons are <c>[NEEDS VERIFICATION]</c>, so they say their stand-in, but for the button icons heard on a controller
/// (<see cref="XboxButtons"/>, #109). The names are behind the dialogue's
/// <c>speech.termNames</c> setting, which the text cleaner reads before asking this (#784).</summary>
public sealed class GameTerms
{
    /// <summary>The files a pinned tag's number is a position in, as game build 548ddc95… (Steam build 18330018)
    /// compiles them: the counts are the messages each file held in play and match the exe's message-file table for
    /// the skill file (docs/findings/placeholder-tags-name-file-positions.md).</summary>
    public static IReadOnlyList<MessageFileShape> Steam18330018 { get; } =
    [
        new(SkillFile, 1086),
        new(ConditionFile, 14),
    ];

    private const string SkillFile = "message/system/Skill_Message_1.msg";
    private const string ConditionFile = "message/system/BadStatus_Message_1.msg";
    private const int NameForm = 2;
    private const int IconForm = 1;

    // Tag name to its stand-in, which is also its placeholder key. Tag counts and forms:
    // docs/raw-sources/message-listening-run-2026-11-16.md.
    private static readonly Dictionary<string, string> StandIns = new(StringComparer.Ordinal)
    {
        ["ITEM"] = "item",
        ["SKILL"] = "skill",
        ["CONDITION"] = "condition",
        ["CLASS"] = "class",
        ["ATTRIBUTE"] = "attribute",
        ["V"] = "value",
        ["PAD_ICON"] = "button",
        ["SELECT_ICON"] = "icon",
        ["SHOP_ICON"] = "shop",
        ["FOLLOWER_ICON"] = "follower",
        ["MONEY_ICON"] = "money",
        ["WEATHER_ICON"] = "weather",
    };

    private static readonly Dictionary<string, string> TagsByKey =
        StandIns.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    // The terms whose form 1 is seen followed by the word it stands for ("[<SKILL 173 1> Strike]").
    private static readonly HashSet<string> IconFormTerms = new(StringComparer.Ordinal) { "ITEM", "SKILL" };

    // Icons sit against the text beside them ("<PAD_ICON 10>Licenses"), so their stand-in is spaced off.
    private static readonly HashSet<string> Icons = new(StringComparer.Ordinal)
    {
        "PAD_ICON", "SELECT_ICON", "SHOP_ICON", "FOLLOWER_ICON", "MONEY_ICON", "WEATHER_ICON",
    };

    private static readonly TextCleaner Plain = new(TextCleanerRules.None);

    // The button a <PAD_ICON n> draws on a controller with Xbox glyphs, by ear on game build 548ddc95… (Steam build
    // 18330018): docs/raw-sources/pad-icon-buttons-by-ear-2026-11-22.md. Other numbers, and the PlayStation and Switch
    // glyphs, are [NEEDS VERIFICATION].
    private static readonly Dictionary<string, string> XboxButtons = new(StringComparer.Ordinal)
    {
        ["6"] = "LT",
        ["7"] = "RT",
        ["12"] = "B",
    };

    // On the keyboard a button icon draws the key bound to its action, which the mod can't read yet
    // (docs/findings/pad-icon-glyph-tables-static.md), so it says this kind instead of "button".
    private const string KeyStandIn = "key";

    private readonly MessageFilePositions files;
    private readonly Action<string, string>? missing;
    private readonly UnknownValues unknowns;
    private readonly Func<InputDevice?> buttons;

    /// <param name="files">The compiled files a pinned tag's number is a position in.</param>
    /// <param name="missing">Told the tag and its arguments each time one says its stand-in (Debug builds' log).</param>
    /// <param name="unknowns">Told each tag and arguments said as a stand-in, as an <c>icon</c> or a <c>tag</c>, which
    /// it writes once a session (#824).</param>
    /// <param name="buttons">The device the game draws button icons for, asked at each button icon (#109): on a
    /// controller a pinned icon says its button, on the keyboard an icon says "key"; null, or none given, says
    /// "button" as before.</param>
    public GameTerms(
        MessageFilePositions files, Action<string, string>? missing = null, UnknownValues? unknowns = null,
        Func<InputDevice?>? buttons = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        this.files = files;
        this.missing = missing;
        this.unknowns = unknowns ?? UnknownValues.None;
        this.buttons = buttons ?? (() => null);
    }

    /// <summary>The tags, as placeholders keyed by their stand-in.</summary>
    public static TextCleanerRules Rules { get; } = new() { Placeholders = StandIns };

    /// <summary>A placeholder's value for <see cref="Rules"/>: the name found, nothing for an icon form, else the
    /// stand-in, reported. Null for a key that isn't one of these tags.</summary>
    public string? Placeholder(string key, string arguments)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(arguments);
        if (!TagsByKey.TryGetValue(key, out var tag))
        {
            return null;
        }
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (IconFormTerms.Contains(tag) && parts.Length == 2 && Number(parts[1]) == IconForm)
        {
            return "";
        }
        if (Found(tag, parts) is { } name)
        {
            return name;
        }
        var device = tag == PadIcon ? buttons() : null;
        if (device == InputDevice.Controller && XboxButtons.TryGetValue(arguments, out var button))
        {
            return $" {button} ";
        }
        missing?.Invoke(tag, arguments);
        var icon = Icons.Contains(tag);
        unknowns.Report(icon ? "icon" : "tag", $"{tag} {arguments}".TrimEnd());
        var standIn = device == InputDevice.Keyboard ? KeyStandIn : key;
        return icon ? $" {standIn} " : standIn;
    }

    private const string PadIcon = "PAD_ICON";

    private string? Found(string tag, string[] parts)
    {
        var file = tag switch
        {
            "SKILL" => SkillFile,
            "CONDITION" => ConditionFile,
            _ => null,
        };
        if (file is null || parts.Length != 2 || Number(parts[1]) != NameForm || Number(parts[0]) is not { } position ||
            files.Find(file, position) is not { } message)
        {
            return null;
        }
        var name = Plain.Clean(message.Text);
        return name.Length == 0 ? null : name;
    }

    private static int? Number(string text) =>
        text.Length is > 0 and <= 5 && text.All(char.IsAsciiDigit) &&
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
}
