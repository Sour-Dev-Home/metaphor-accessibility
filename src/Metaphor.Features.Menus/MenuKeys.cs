using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The settings menu's and the help key's bindings (#754, #868), each 0 when unbound, under
/// <c>keys.menus</c>: F2 opens the menu and F1 says the help; on the pad <see cref="ChordButtons"/> held, then
/// <see cref="SettingsButton"/> or <see cref="HelpButton"/>.</summary>
public sealed record SettingsMenuBindings(int SettingsKey, int ChordButtons, int SettingsButton, int HelpKey, int HelpButton)
{
    /// <summary>F2 and F1, and LT and RT held then View or Y: the maintainer's decisions of 2026-11-23 on #754 (F2,
    /// LT+RT then View) and #868 (F1, the same chord with a face button). The idea of an F1 help key, an F2 settings
    /// menu and a trigger chord comes from Arcana (https://github.com/ChipsAhoiMcCoy/arcana, ChipsAhoiMcCoy, Zlib,
    /// v0.6.0 release notes, tag commit 73db802); the idea only, no code (CREDITS.md). Codes: F1 <c>0x70</c> and F2
    /// <c>0x71</c> (VK_F1, VK_F2 in WinUser.h, Windows SDK 10.0.26100.0), which the game's keyboard read lists when held;
    /// LT <c>0x0100</c>, RT <c>0x0200</c>, View (XInput's Back) <c>0x0001</c> and Y <c>0x1000</c> in the game's pad
    /// mask on game build 548ddc95… (both: docs/raw-sources/pad-and-keyboard-state-2026-11-16.md). Y with the chord
    /// never reaches the game: the shoulder taps keep a chord's other button from it (#811). Whether the game binds
    /// F1 or F2 is <c>[NEEDS VERIFICATION]</c>; while the features are on, the mod keeps them.</summary>
    public static SettingsMenuBindings Default { get; } = From(ModSettings.Defaults);

    /// <summary>The bindings the settings file's <c>keys.menus</c> object names, each missing or unusable one the
    /// default.</summary>
    public static SettingsMenuBindings From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            MenuKeys.SettingsKey.From(settings), MenuKeys.ChordButtons.From(settings),
            MenuKeys.SettingsButton.From(settings), MenuKeys.HelpKey.From(settings), MenuKeys.HelpButton.From(settings));
    }
}

/// <summary>The menus project's keys, for the help key and the settings menu's key list (#868, #754). Every binding
/// the project's controls read is here; a test fails when one isn't.</summary>
public static class MenuKeys
{
    /// <summary><c>keys.menus.settingsMenuKey</c>.</summary>
    public static BoundKey SettingsKey { get; } = new("menus", "settingsMenuKey", 0x71);

    /// <summary><c>keys.menus.chordButtons</c>, held for the settings menu and the help on the pad.</summary>
    public static BoundKey ChordButtons { get; } = new("menus", "chordButtons", 0x0100 | 0x0200);

    /// <summary><c>keys.menus.settingsMenuButton</c>.</summary>
    public static BoundKey SettingsButton { get; } = new("menus", "settingsMenuButton", 0x0001);

    /// <summary><c>keys.menus.helpKey</c>.</summary>
    public static BoundKey HelpKey { get; } = new("menus", "helpKey", 0x70);

    /// <summary><c>keys.menus.helpButton</c>.</summary>
    public static BoundKey HelpButton { get; } = new("menus", "helpButton", 0x1000);

    /// <summary><c>keys.menus.detailsKey</c>.</summary>
    public static BoundKey DetailsKey { get; } = new("menus", "detailsKey", MenuBindings.Default.DetailsKey);

    /// <summary><c>keys.menus.detailsButtons</c>.</summary>
    public static BoundKey DetailsButtons { get; } = new("menus", "detailsButtons", MenuBindings.Default.DetailsButtons);

    /// <summary><c>keys.menus.nameReadBackKey</c>.</summary>
    public static BoundKey NameReadBackKey { get; } = new("menus", "nameReadBackKey", 0x52);

    /// <summary><c>keys.report.markKey</c>.</summary>
    public static BoundKey MarkKey { get; } = new("report", "markKey", Menus.MarkKey.DefaultKey);

    /// <summary><c>keys.dialogue.readerKey</c>, the dialogue's reader binding, which the menus toggle with too.</summary>
    public static BoundKey ReaderKey { get; } = new("dialogue", "readerKey", ReaderBinding.Default.Key);

    /// <summary><c>keys.dialogue.readerButtons</c>.</summary>
    public static BoundKey ReaderButtons { get; } = new("dialogue", "readerButtons", ReaderBinding.Default.Buttons);

    /// <summary>The settings menu's own keys, fixed (#754).</summary>
    public static IReadOnlyList<ModKey> SettingsMenuKeys { get; } =
    [
        new("Move") { Fixed = "Up and Down, or the D-pad" },
        new("Change the setting and save it") { Fixed = "Left and Right, with Shift for 5 steps, or the D-pad" },
        new("Next or previous section") { Fixed = "Page Down and Page Up, or RB and LB" },
        new("Say what the setting does", DetailsKey, DetailsButtons),
        new("Close the settings menu") { Fixed = "Escape, or B on the pad" },
    ];

    /// <summary>Every key the project lists, by where it works.</summary>
    public static IReadOnlyList<KeyEntry> All { get; } =
    [
        new(KeyPlaces.Anywhere, MenusFeature.SettingsMenu,
            new("Open the mod's settings", SettingsKey, SettingsButton, ChordButtons)),
        new(KeyPlaces.Anywhere | KeyPlaces.SettingsMenu, MenusFeature.KeyHelp,
            new("Say the keys for where you are", HelpKey, HelpButton, ChordButtons)),
        new(KeyPlaces.Anywhere | KeyPlaces.SettingsMenu, MenusFeature.MarkKey,
            new("Write a marker to the report log", MarkKey)),
        new(KeyPlaces.Menu, MenusFeature.Menus, new("Read the focused item's details", DetailsKey, DetailsButtons)),
        new(KeyPlaces.Menu, MenusFeature.Menus, new("Turn the reader on or off", ReaderKey, ReaderButtons)),
        new(KeyPlaces.Menu, MenusFeature.NameReadBack,
            new("On the name entry screen, read back the name typed", NameReadBackKey, DetailsButtons)
            {
                WithCtrl = true,
            }),
        .. SettingsMenuKeys.Select(key => new KeyEntry(KeyPlaces.SettingsMenu, MenusFeature.SettingsMenu, key)),
    ];
}
