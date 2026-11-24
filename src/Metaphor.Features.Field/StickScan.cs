using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>Scan by direction (#701): in free roam, with RB held, pushing the right stick says the nearest thing that
/// way, within <see cref="HalfCone"/> of the push, on the screen's frame (#686): up is ahead, right is right. Each new
/// eighth of the circle the stick enters is said once; letting the stick back near the centre lets the same eighth be
/// said again. While RB is held in free roam the game gets the right stick at rest, so the camera doesn't turn, and the
/// push makes RB a chord, so the map doesn't open on release (the frame's rule, <see cref="InputFrame.Sticks"/>). The
/// keyboard's scan key, which the idea's source lacks, says the nearest thing ahead, left, right and behind in turn.
/// The idea comes from Arcana, the Persona 3 Reload accessibility mod (<c>ChipsAhoiMcCoy/arcana</c>, Zlib, <c>main</c>
/// at <c>ed3b005f86b1cde27e30ab299ae91af3d974c988</c>, <c>KNOWN_ISSUES.md</c> and the README); no code is copied.
/// </summary>
[FeatureSetting<FieldFeature>(FieldFeature.StickScan)]
public sealed class StickScan : IInputControls, IStickControls
{
    /// <summary>How far the stick goes, of a full push, before a direction is said.</summary>
    public const double PushThreshold = 0.6;

    /// <summary>How near the centre the stick comes back, of a full push, before the same direction can be said again.
    /// </summary>
    public const double RestThreshold = 0.3;

    /// <summary>How far either side of the push a thing still counts as that way: 45°, so the eight directions overlap
    /// and nothing between two of them is missed.</summary>
    public const double HalfCone = Math.PI / 4;

    /// <summary>RB in the game's pad mask (<see cref="ShoulderTaps.RightShoulder"/>), the scan's layer button.</summary>
    public const int LayerButton = ShoulderTaps.RightShoulder;

    private readonly Lock gate = new();
    private readonly IFieldReads field;
    private readonly IAnnouncer announcer;
    private readonly Func<FieldFeature, bool> isOn;
    private readonly Func<int> scanKey;
    private readonly Func<double> radius;
    private readonly NamesTable? names;
    private readonly FreeRoamGate roam;
    private readonly Dictionary<int, int?> said = [];
    private bool keyHeld;

    /// <summary>A scan that reads <paramref name="field"/>, names things by <paramref name="names"/>, speaks through
    /// <paramref name="announcer"/>, looks within <paramref name="radius"/> game units, and takes
    /// <paramref name="scanKey"/> as the keyboard's scan key. <paramref name="isOn"/> says whether the scan is on and
    /// whether the list's height and compass words are (#650, #700).</summary>
    public StickScan(
        IFieldReads field, IAnnouncer announcer, Func<FieldFeature, bool> isOn, Func<int> scanKey, Func<double> radius,
        NamesTable? names = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(isOn);
        ArgumentNullException.ThrowIfNull(scanKey);
        ArgumentNullException.ThrowIfNull(radius);
        this.field = field;
        this.announcer = announcer;
        this.isOn = isOn;
        this.scanKey = scanKey;
        this.radius = radius;
        this.names = names;
        roam = new FreeRoamGate(field);
    }

    /// <summary>Hands the buttons on unchanged: RB is held back by the field's shoulder hold (#811).</summary>
    public int Pad(int pad, int buttons, bool made) => buttons;

    /// <summary>Takes one read of the keys: a press of the scan key in free roam says the four ways and is hidden.
    /// </summary>
    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var key = scanKey();
        var held = key != 0 && keys.Contains(key);
        bool pressed;
        lock (gate)
        {
            pressed = held && !keyHeld;
            keyHeld = held;
        }
        if (!held || !isOn(FieldFeature.StickScan) || !roam.Open())
        {
            return keys;
        }
        if (pressed)
        {
            announcer.Announce(new Announcement(Four(), Interrupt: true));
        }
        return [.. keys.Where(k => k != key)];
    }

    /// <summary>Takes one read of the sticks: with RB held in free roam, says a new direction the right stick points and
    /// hands the game the right stick at rest; otherwise hands them on unchanged.</summary>
    public PadSticks Sticks(int pad, int buttons, PadSticks sticks)
    {
        if ((buttons & LayerButton) == 0 || !isOn(FieldFeature.StickScan) || !roam.Open())
        {
            lock (gate)
            {
                said.Remove(pad);
            }
            return sticks;
        }
        var (x, y) = sticks.Right;
        var length = Math.Sqrt((x * x) + (y * y));
        int? sector = null;
        lock (gate)
        {
            said.TryGetValue(pad, out var before);
            if (length >= PushThreshold)
            {
                // Up is ahead and right is right; bearings are left positive.
                var at = (int)Math.Round(CameraFrame.Normalised(Math.Atan2(-x, y)) / (Math.PI / 4));
                at = at == -4 ? 4 : at;
                if (at != before)
                {
                    sector = at;
                    said[pad] = at;
                }
            }
            else if (length < RestThreshold)
            {
                said.Remove(pad);
            }
        }
        if (sector is { } way)
        {
            announcer.Announce(new Announcement(Way(way * Math.PI / 4), Interrupt: true));
        }
        return sticks.RightAtRest();
    }

    /// <summary>"Ahead-left: Door, 5 steps ahead-left", or "Nothing ahead-left": the nearest thing within
    /// <see cref="HalfCone"/> of <paramref name="bearing"/> (left positive, from the screen's facing), as the list says
    /// it.</summary>
    public string Way(double bearing) => View() is { } view ? Way(view, bearing) : "Can't read the field";

    private string Four()
    {
        if (View() is not { } view)
        {
            return "Can't read the field";
        }
        double[] ways = [0, Math.PI / 2, -Math.PI / 2, Math.PI];
        return string.Join(". ", ways.Select(way => Way(view, way)));
    }

    private string Way(FieldView view, double bearing)
    {
        var word = Nearby.Direction(bearing);
        return Nearby.Toward(view, bearing, HalfCone, radius()) is { } thing
            ? string.Create(CultureInfo.InvariantCulture, $"{Capitalised(word)}: {Text(thing)}")
            : $"Nothing {word}";
    }

    private FieldView? View() => field.Read() is { } view ? names?.Apply(view) ?? view : null;

    private string Text(NearbyThing thing) =>
        thing.Text(isOn(FieldFeature.NearbyHeight), isOn(FieldFeature.Compass));

    private static string Capitalised(string word) => char.ToUpperInvariant(word[0]) + word[1..];
}
