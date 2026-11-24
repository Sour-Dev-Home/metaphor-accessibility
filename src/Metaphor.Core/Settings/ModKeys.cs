namespace Metaphor.Core.Settings;

/// <summary>Where a mod key works (#868): the help key says the keys of every place the player is in now.</summary>
[Flags]
public enum KeyPlaces
{
    /// <summary>Nowhere yet.</summary>
    None = 0,

    /// <summary>On every screen.</summary>
    Anywhere = 1,

    /// <summary>While a dialogue line or choice is up.</summary>
    Dialogue = 2,

    /// <summary>In the game's menus.</summary>
    Menu = 4,

    /// <summary>In the field, free to walk.</summary>
    Field = 8,

    /// <summary>In a battle.</summary>
    Battle = 16,

    /// <summary>In the mod's settings menu (#754).</summary>
    SettingsMenu = 32,
}

/// <summary>One binding in the settings file's <c>keys</c> object (#661): <see cref="Name"/> in <see cref="Group"/>,
/// and its <see cref="Default"/> code, a virtual key or a pad mask by the name's ending
/// (<see cref="BindingNames.IsButtons"/>).</summary>
public sealed record BoundKey(string Group, string Name, int Default)
{
    /// <summary>The code <paramref name="settings"/> bind it to, 0 for none.</summary>
    public int From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Binding(Group, Name, Default);
    }

    /// <summary>Whether it holds pad buttons rather than a key.</summary>
    public bool Buttons => BindingNames.IsButtons(Name);
}

/// <summary>A mod key as the help key says it (#868): what it <see cref="Does"/>, its keyboard <see cref="Key"/>
/// (with Ctrl held when <see cref="WithCtrl"/>), and on the pad <see cref="Button"/> alone or with
/// <see cref="Hold"/> held. A fixed key, such as the settings menu's arrows, is <see cref="Fixed"/> instead.</summary>
public sealed record ModKey(string Does, BoundKey? Key = null, BoundKey? Button = null, BoundKey? Hold = null)
{
    /// <summary>A key the player can't move, said as written.</summary>
    public string? Fixed { get; init; }

    /// <summary>The key works with Ctrl held.</summary>
    public bool WithCtrl { get; init; }

    /// <summary>A key only Debug builds act on: listed so the bindings are all known, never said.</summary>
    public bool Hidden { get; init; }

    /// <summary>"Repeat the last line: R, or X on the pad"; null when nothing is bound to it.</summary>
    public string? Said(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var keys = Keys(settings);
        return keys is null ? null : $"{Does}: {keys}";
    }

    /// <summary>The bindings alone, "R, or X on the pad"; null when nothing is bound.</summary>
    public string? Keys(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (Fixed is not null)
        {
            return Fixed;
        }
        var key = Key is null ? 0 : Key.From(settings);
        var keyName = key == 0 ? null : (WithCtrl ? "Control " : "") + BindingNames.SpokenKey(key);
        var button = Button is null ? 0 : Button.From(settings);
        var hold = Hold is null ? 0 : Hold.From(settings);
        var buttonName = button == 0 || (Hold is not null && hold == 0)
            ? null
            : hold == 0
                ? BindingNames.SpokenButtons(button)
                : $"{BindingNames.SpokenButtons(hold)} with {BindingNames.SpokenButtons(button)}";
        return (keyName, buttonName) switch
        {
            (null, null) => null,
            ({ } k, null) => k,
            (null, { } b) => $"{b} on the pad",
            ({ } k, { } b) => $"{k}, or {b} on the pad",
        };
    }

    /// <summary>Every binding it reads.</summary>
    public IEnumerable<BoundKey> Bindings => new[] { Key, Button, Hold }.OfType<BoundKey>();
}

/// <summary>A mod key and where it works, behind its feature's setting (#868): the help key says it only while
/// <see cref="Feature"/> is on and the player is in one of its <see cref="Places"/>.</summary>
public sealed record KeyEntry(KeyPlaces Places, Enum Feature, ModKey Key);
