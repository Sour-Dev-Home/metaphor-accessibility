using System.Collections.Frozen;
using System.Globalization;

namespace Metaphor.Core.Settings;

/// <summary>The names a player writes for keys and pad buttons in the settings file's <c>keys</c> object (#661), and
/// the codes the controls compare with the game's reads. <c>none</c> leaves an action unbound.</summary>
public static class BindingNames
{
    /// <summary>What a player writes to leave an action unbound.</summary>
    public const string None = "none";

    // Windows virtual-key codes (VK_* in WinUser.h, Windows SDK 10.0.26100.0): letters are their capital's ASCII code,
    // digits their ASCII code, F1 to F12 0x70 to 0x7B. The game's keyboard read lists codes like these:
    // docs/raw-sources/pad-and-keyboard-state-2026-11-16.md.
    private static readonly FrozenDictionary<string, int> KeyCodes = Enumerable.Range('A', 26)
        .Select(code => (Name: ((char)code).ToString(), Code: code))
        .Concat(Enumerable.Range('0', 10).Select(code => (Name: ((char)code).ToString(), Code: code)))
        .Concat(Enumerable.Range(1, 12).Select(n => (Name: string.Create(CultureInfo.InvariantCulture, $"F{n}"), Code: 0x6F + n)))
        .ToFrozenDictionary(key => key.Name, key => key.Code, StringComparer.OrdinalIgnoreCase);

    // Bits of the game's 16-bit pad mask by XInput button, on game build 548ddc95 (Steam build 18330018):
    // docs/raw-sources/pad-and-keyboard-state-2026-11-16.md. The triggers count as held past 30.
    private static readonly FrozenDictionary<string, int> ButtonBits = new Dictionary<string, int>
    {
        ["A"] = 0x4000,
        ["B"] = 0x2000,
        ["X"] = 0x8000,
        ["Y"] = 0x1000,
        ["LB"] = 0x0400,
        ["RB"] = 0x0800,
        ["LT"] = 0x0100,
        ["RT"] = 0x0200,
        ["Start"] = 0x0008,
        ["Back"] = 0x0001,
        ["LS"] = 0x0002,
        ["RS"] = 0x0004,
        ["DpadUp"] = 0x0010,
        ["DpadDown"] = 0x0040,
        ["DpadLeft"] = 0x0080,
        ["DpadRight"] = 0x0020,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a binding named <paramref name="name"/> in the file holds pad buttons rather than a key: its
    /// name ends in <c>Button</c> or <c>Buttons</c>.</summary>
    public static bool IsButtons(string name) =>
        name.EndsWith("Button", StringComparison.Ordinal) || name.EndsWith("Buttons", StringComparison.Ordinal);

    /// <summary>Whether a binding named <paramref name="name"/> holds a key: its name ends in <c>Key</c>.</summary>
    public static bool IsKey(string name) => name.EndsWith("Key", StringComparison.Ordinal);

    /// <summary>The virtual-key code of a key's name in any case, 0 for <see cref="None"/>, null for anything else.
    /// </summary>
    public static int? Key(string? text)
    {
        var name = text?.Trim();
        if (string.Equals(name, None, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
        return name is not null && KeyCodes.TryGetValue(name, out var code) ? code : null;
    }

    /// <summary>The pad mask of button names joined by <c>+</c>, held together, in any case; 0 for
    /// <see cref="None"/>, null when any name is unknown.</summary>
    public static int? Buttons(string? text)
    {
        if (string.Equals(text?.Trim(), None, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var mask = 0;
        foreach (var part in text.Split('+'))
        {
            if (!ButtonBits.TryGetValue(part.Trim(), out var bit))
            {
                return null;
            }
            mask |= bit;
        }
        return mask;
    }
}
