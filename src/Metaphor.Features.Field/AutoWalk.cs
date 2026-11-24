using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>Auto-walk (#184): while the guidance beep guides (#183), the walk key or chord pushes the game's own left
/// stick toward where the beep points (the next corner of the route, or the target), on the screen's frame (#686), so
/// the player walks there as if holding the stick. It never writes the player's position and never presses a button:
/// all it hands the game is the left stick of a pad read the game made (<see cref="Sticks"/>). It stops, saying
/// "Auto-walk off", on any button, key or stick the player presses, and silently when the beep ends (it says "Reached")
/// or free roam ends (a line, a menu, a battle, a scripted move), or the map changes. That the field's movement reads
/// the left stick from the pad read the mod changes is [NEEDS VERIFICATION] (docs/findings/pad-sticks-in-pad-state-static.md);
/// so is the game's movement following the camera as the beep does. It needs a pad: with none connected the pad read
/// isn't made and the game gets nothing from it.</summary>
[FeatureSetting<FieldFeature>(FieldFeature.AutoWalk)]
public sealed class AutoWalk : IInputControls, IStickControls
{
    /// <summary>What is said when the player starts it without guidance on.</summary>
    public const string NoTargetText = "Start the guidance beep first";

    private readonly Lock gate = new();
    private readonly IFieldReads field;
    private readonly Func<double?> aim;
    private readonly IAnnouncer announcer;
    private readonly Func<bool> on;
    private readonly Func<WalkBindings> bindings;
    private readonly FreeRoamGate roam;
    private bool walking;
    private int? map;
    private int heldButtons;
    private HashSet<int> heldKeys = [];
    private int previousButtons;
    private bool keyWasDown;

    /// <summary>Auto-walk over <paramref name="field"/>, steering by <paramref name="aim"/> (the beep's bearing from the
    /// screen's facing, left positive, or null when it isn't guiding: <see cref="Beacon.Bearing"/>), speaking through
    /// <paramref name="announcer"/>, on while <paramref name="on"/> says so, on <paramref name="bindings"/>.</summary>
    public AutoWalk(
        IFieldReads field, Func<double?> aim, IAnnouncer announcer, Func<bool> on, Func<WalkBindings> bindings)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(aim);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(on);
        ArgumentNullException.ThrowIfNull(bindings);
        this.field = field;
        this.aim = aim;
        this.announcer = announcer;
        this.on = on;
        this.bindings = bindings;
        roam = new FreeRoamGate(field);
    }

    /// <summary>Whether it is walking.</summary>
    public bool Walking
    {
        get
        {
            lock (gate)
            {
                return walking;
            }
        }
    }

    /// <summary>Takes one read of the buttons: the walk chord in free roam starts or stops it, and its button is
    /// hidden; while walking, any other button pressed stops it.</summary>
    public int Pad(int pad, int buttons, bool made)
    {
        if (!made)
        {
            return buttons;
        }
        var b = bindings();
        var chord = b.HoldButtons != 0 && b.WalkButton != 0 &&
            (buttons & (b.HoldButtons | b.WalkButton)) == (b.HoldButtons | b.WalkButton);
        bool pressed;
        string? say = null;
        lock (gate)
        {
            pressed = chord && (previousButtons & b.WalkButton) == 0;
            previousButtons = buttons;
            if (walking && !pressed && (buttons & ~heldButtons) != 0)
            {
                walking = false;
                say = "Auto-walk off";
            }
            heldButtons &= buttons;
        }
        if (pressed && on() && roam.Open())
        {
            say = Toggle(buttons, null);
        }
        Say(say);
        return chord && on() && roam.Open() ? buttons & ~b.WalkButton : buttons;
    }

    /// <summary>Takes one read of the keys: the walk key in free roam starts or stops it, and is hidden; while walking,
    /// any other key pressed stops it.</summary>
    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var key = bindings().WalkKey;
        var down = key != 0 && keys.Contains(key);
        bool pressed;
        string? say = null;
        lock (gate)
        {
            pressed = down && !keyWasDown;
            keyWasDown = down;
            if (walking && keys.Any(k => k != key && !heldKeys.Contains(k)))
            {
                walking = false;
                say = "Auto-walk off";
            }
            heldKeys.IntersectWith(keys);
        }
        if (!down || !on() || !roam.Open())
        {
            Say(say);
            return keys;
        }
        if (pressed)
        {
            say = Toggle(0, keys);
        }
        Say(say);
        return [.. keys.Where(k => k != key)];
    }

    /// <summary>Takes one made read of the sticks: while walking, hands the game the left stick pushed fully toward the
    /// beep's aim; stops on a stick the player pushes, the beep ending, free roam ending or a new map.</summary>
    public PadSticks Sticks(int pad, int buttons, PadSticks sticks)
    {
        string? say = null;
        double? toward = null;
        lock (gate)
        {
            if (!walking)
            {
                return sticks;
            }
            if (sticks.Moved)
            {
                walking = false;
                say = "Auto-walk off";
            }
            else if (!on() || !roam.Open() || field.MapId() != map || aim() is not { } bearing)
            {
                walking = false;
            }
            else
            {
                toward = bearing;
            }
        }
        Say(say);
        // Bearings are left positive and up on the stick is ahead (docs/findings/pad-sticks-in-pad-state-static.md).
        return toward is { } way ? sticks.WithLeft(-Math.Sin(way), Math.Cos(way)) : sticks;
    }

    private string Toggle(int buttons, IReadOnlyList<int>? keys)
    {
        lock (gate)
        {
            if (walking)
            {
                walking = false;
                return "Auto-walk off";
            }
            if (aim() is null)
            {
                return NoTargetText;
            }
            walking = true;
            map = field.MapId();
            heldButtons = buttons;
            heldKeys = keys is null ? [] : [.. keys];
            return "Auto-walk on";
        }
    }

    private void Say(string? text)
    {
        if (text is not null)
        {
            announcer.Announce(new Announcement(text, Interrupt: true));
        }
    }
}

/// <summary>Auto-walk's keys (#184): the key, and the pad's chord of the field's hold button with the walk button.
/// </summary>
/// <param name="WalkKey">The key that starts or stops it.</param>
/// <param name="HoldButtons">The button held for the chord: the field's (<see cref="FieldBindings.HoldButtons"/>).</param>
/// <param name="WalkButton">With <paramref name="HoldButtons"/>: starts or stops it, or 0 for none.</param>
public sealed record WalkBindings(int WalkKey, int HoldButtons, int WalkButton);

/// <summary>The keys of the stick features (#701, #184), in the settings file's <c>keys.field</c>. Buttons are bits of
/// the game's 16-bit pad mask; keys are Windows virtual-key codes.</summary>
/// <param name="ScanKey">The key that says the nearest thing ahead, left, right and behind (#701).</param>
/// <param name="WalkKey">The key that starts or stops auto-walk (#184).</param>
/// <param name="WalkButton">With the field's hold button (<see cref="FieldBindings.HoldButtons"/>): as
/// <paramref name="WalkKey"/>, or 0 for none.</param>
public sealed record StickBindings(int ScanKey, int WalkKey, int WalkButton)
{
    /// <summary>U (<c>0x55</c>) to scan, H (<c>0x48</c>) to walk, and Y on the pad (<c>0x1000</c>) with the field's LB:
    /// proposed, the mod's own choices, free among the mod's field keys and within the game's keyboard read of 8 to
    /// <c>0xEF</c> (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md, which also maps Y to <c>0x1000</c>, game build
    /// 548ddc95…, Steam build 18330018). Whether the game's own keyboard map uses U or H is [NEEDS VERIFICATION], as
    /// for J and K; Y under the held LB never reaches the game (#811's chord rule).</summary>
    public static StickBindings Default { get; } = new(0x55, 0x48, 0x1000);

    /// <summary>The bindings the settings file's <c>keys.field</c> object names, each missing or unusable one the
    /// default.</summary>
    public static StickBindings From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            settings.Binding("field", "scanKey", Default.ScanKey),
            settings.Binding("field", "walkKey", Default.WalkKey),
            settings.Binding("field", "walkButton", Default.WalkButton));
    }
}
