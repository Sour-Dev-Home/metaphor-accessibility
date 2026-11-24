using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The help key (#868): F1, or the settings menu's chord with Y, says the mod's keys that work where the player
/// is now, with their current bindings: in the settings menu its own keys; otherwise those of a dialogue line, a menu,
/// a battle or, with none of those, the field, then those that work anywhere. It says them from the keys the modules
/// listed (<see cref="Core.Features.FeatureContext.AddKeys"/>), read at the press, so it follows a rebinding and a
/// feature turned off. The idea of a context help key comes from Arcana (https://github.com/ChipsAhoiMcCoy/arcana,
/// ChipsAhoiMcCoy, Zlib, v0.6.0 release notes, tag commit 73db802); the idea only, no code (CREDITS.md). While the
/// feature is on, its key and button never reach the game. Safe to call from any of the game's threads.</summary>
[FeatureSetting<MenusFeature>(MenusFeature.KeyHelp)]
public sealed class KeyHelp : IWindowInputControls
{
    private readonly Lock gate = new();
    private readonly IReadOnlyList<KeyEntry> keys;
    private readonly Func<ModSettings> settings;
    private readonly IAnnouncer speech;
    private readonly Func<SettingsMenuBindings> bindings;
    private readonly Func<KeyPlaces> places;
    private readonly Dictionary<int, int> padsBefore = [];
    private readonly HashSet<int> keysBefore = [];
    private long? window;

    /// <param name="keys">Every listed key; a list the modules are still adding to is read at each press.</param>
    /// <param name="settings">The settings in force: each key's binding and its feature's toggle.</param>
    /// <param name="speech">Where the help speaks.</param>
    /// <param name="bindings">The help key and chord, read on each read.</param>
    /// <param name="places">Where the player is, but for a dialogue line, which the window reads tell.</param>
    public KeyHelp(
        IReadOnlyList<KeyEntry> keys, Func<ModSettings> settings, IAnnouncer speech, Func<SettingsMenuBindings> bindings,
        Func<KeyPlaces> places)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(places);
        this.keys = keys;
        this.settings = settings;
        this.speech = speech;
        this.bindings = bindings;
        this.places = places;
    }

    public void WindowRead(long? currentMessage)
    {
        lock (gate)
        {
            window = currentMessage;
        }
    }

    public int Pad(int pad, int buttons, bool made)
    {
        if (!made)
        {
            return buttons;
        }
        var now = bindings();
        lock (gate)
        {
            var down = buttons & ~padsBefore.GetValueOrDefault(pad);
            padsBefore[pad] = buttons;
            if (now.ChordButtons == 0 || now.HelpButton == 0 || (buttons & now.ChordButtons) != now.ChordButtons)
            {
                return buttons;
            }
            if ((down & now.HelpButton) != 0)
            {
                Speak();
            }
            return buttons & ~now.HelpButton;
        }
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var key = bindings().HelpKey;
        lock (gate)
        {
            var pressed = key != 0 && keys.Contains(key) && !keysBefore.Contains(key);
            keysBefore.Clear();
            keysBefore.UnionWith(keys);
            if (key == 0 || !keys.Contains(key))
            {
                return keys;
            }
            if (pressed)
            {
                Speak();
            }
            return [.. keys.Where(each => each != key)];
        }
    }

    /// <summary>The help for <paramref name="where"/>: its keys, then those that work anywhere, each said once.
    /// </summary>
    public string Line(KeyPlaces where)
    {
        var now = settings();
        var here = where & ~KeyPlaces.Anywhere;
        IEnumerable<KeyEntry> Listed(Func<KeyEntry, bool> wanted) =>
            keys.Where(entry => wanted(entry) && !entry.Key.Hidden && now.IsOn(entry.Feature));
        var said = Listed(entry => (entry.Places & here) != 0)
            .Concat(where.HasFlag(KeyPlaces.Anywhere)
                ? Listed(entry => entry.Places.HasFlag(KeyPlaces.Anywhere) && (entry.Places & here) == 0)
                : [])
            .Select(entry => entry.Key.Said(now))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return said.Count == 0
            ? $"No mod keys {Spoken(here)}."
            : $"Keys {Spoken(here)}. {string.Join(". ", said)}.";
    }

    /// <summary>Where the player is, as <see cref="Line"/> takes it: the places <c>places</c> gave, with a dialogue
    /// line when the window holds one, and the field when none of a menu, a battle or a line is up.</summary>
    public KeyPlaces Where()
    {
        long? message;
        lock (gate)
        {
            message = window;
        }
        var where = places();
        if (where.HasFlag(KeyPlaces.SettingsMenu))
        {
            return KeyPlaces.SettingsMenu;
        }
        if (message is { } held && held != 0)
        {
            where |= KeyPlaces.Dialogue;
        }
        if ((where & (KeyPlaces.Dialogue | KeyPlaces.Menu | KeyPlaces.Battle)) == 0)
        {
            where |= KeyPlaces.Field;
        }
        return where | KeyPlaces.Anywhere;
    }

    private void Speak() => speech.Announce(new Announcement(Line(Where()), Interrupt: true));

    private static readonly (KeyPlaces Place, string Said)[] Places =
    [
        (KeyPlaces.SettingsMenu, "the settings menu"), (KeyPlaces.Dialogue, "dialogue"), (KeyPlaces.Menu, "menus"),
        (KeyPlaces.Battle, "battle"), (KeyPlaces.Field, "the field"),
    ];

    // "in dialogue and menus"; "anywhere" for no place of its own.
    private static string Spoken(KeyPlaces here)
    {
        var named = string.Join(" and ", Places.Where(place => here.HasFlag(place.Place)).Select(place => place.Said));
        return named.Length == 0 ? "anywhere" : $"in {named}";
    }
}
