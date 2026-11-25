using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Field;

/// <summary>The map cursor's own keys (#877): the key held to move it (V), the bookmark key (B) and, with the field's
/// hold buttons (LB), the bookmark button (R3). The settings file can rebind each under <c>keys.field</c>.</summary>
/// <remarks>V (<c>0x56</c>) and B (<c>0x42</c>) are free of the mod's field and dialogue keys and inside the game's
/// keyboard read of 8 to <c>0xEF</c> (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md); whether the game's own
/// field keyboard map uses them is <c>[NEEDS VERIFICATION]</c>, to check in a session as the decision asks. R3 is
/// <c>0x0004</c> in the game's pad mask (same source).</remarks>
public sealed record CursorBindings(int CursorKey, int BookmarkKey, int BookmarkButton)
{
    /// <summary>V, B and R3.</summary>
    public static CursorBindings Default { get; } = new(0x56, 0x42, 0x0004);

    /// <summary>The bindings the settings file's <c>keys.field</c> object names, each missing or unusable one the
    /// default.</summary>
    public static CursorBindings From(ModSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new CursorBindings(
            settings.Binding("field", "cursorKey", Default.CursorKey),
            settings.Binding("field", "bookmarkKey", Default.BookmarkKey),
            settings.Binding("field", "bookmarkButton", Default.BookmarkButton));
    }
}

/// <summary>The map cursor with bookmarks (#877, option 2 of its spike, the maintainer's decision of 2026-11-24). In free
/// roam, holding <see cref="CursorBindings.CursorKey"/> (V) puts a cursor on the player ("Map cursor"); each arrow key
/// moves it <see cref="MoveSteps"/> steps on the screen's frame (up is ahead, <see cref="CameraFrame"/>) and says
/// what's there: a bookmark, an icon point of the map, the nearest thing of the field, "open" or "wall" from the
/// walking map, and its distance and direction from the player ("Door, open, 5 steps ahead"). Letting go returns it to
/// the player. While it is held, <see cref="CursorBindings.BookmarkKey"/> (B) bookmarks its spot on this map, the list's
/// next and previous keys (K, J) jump it to this map's bookmarks, and the guide key (G) starts the beacon toward it, or
/// toward the bookmark it is on. On the pad, the field's hold buttons (LB) with the right stick move it a step per push
/// (the game gets the right stick at rest, so the camera stays still and LB becomes a chord), and while it is out LB
/// with the bookmark button (R3), D-pad Down and Up and D-pad Right do the same; letting go of LB ends it. It only reads
/// the game: the player is never moved. The idea of a map cursor with bookmarks comes from Unseen Desire, a Persona 5
/// Royal accessibility mod (heavenay.com/unseenDesire, 0.1.7, GPL-3.0 per its docs), from its docs only; no code was
/// read (CREDITS.md).</summary>
[FeatureSetting<FieldFeature>(FieldFeature.MapCursor)]
public sealed class MapCursor : IInputControls, IStickControls
{
    /// <summary>How far one move goes, in the nearby list's steps: the maintainer's 5.</summary>
    public const int MoveSteps = 5;

    /// <summary>How near the cursor a bookmark is said as there, in steps.</summary>
    public const double BookmarkSteps = 2.5;

    /// <summary>How near the cursor an icon point or a thing is said as there, in steps.</summary>
    public const double ThereSteps = 3;

    private const int KeyLeft = 0x25;
    private const int KeyUp = 0x26;
    private const int KeyRight = 0x27;
    private const int KeyDown = 0x28;
    private const int KeyboardSource = -1;

    // The arrows' moves, left positive (VK_LEFT to VK_DOWN, WinUser.h, Windows SDK 10.0.26100.0).
    private static readonly (int Key, double Bearing)[] Arrows =
        [(KeyUp, 0), (KeyLeft, Math.PI / 2), (KeyRight, -Math.PI / 2), (KeyDown, Math.PI)];

    private readonly Lock gate = new();
    private readonly IFieldReads field;
    private readonly IAnnouncer announcer;
    private readonly Func<bool> on;
    private readonly Func<CursorBindings> keys;
    private readonly Func<FieldBindings> fieldKeys;
    private readonly MapBookmarks bookmarks;
    private readonly Func<int, NavEdges?> edges;
    private readonly Func<int?, float, IReadOnlyList<(float X, float Z, bool Objective)>> marks;
    private readonly Func<BeaconTarget, bool> guide;
    private readonly NamesTable? names;
    private readonly FreeRoamGate roam;
    private readonly HashSet<int> keysDown = [];
    private readonly Dictionary<int, int> padDown = [];
    private readonly Dictionary<int, int?> stickSaid = [];
    private Spot? spot;
    private int source;
    private int jump = -1;

    /// <summary>A cursor over what <paramref name="field"/> reads, speaking through <paramref name="announcer"/> while
    /// <paramref name="on"/>, on <paramref name="keys"/> and the field's <paramref name="fieldKeys"/>, keeping
    /// <paramref name="bookmarks"/>. <paramref name="edges"/> gives a map's walking map once loaded (null before),
    /// <paramref name="marks"/> a map's icon points in world from a height, and <paramref name="guide"/> starts the beacon
    /// (false when guidance is off).</summary>
    public MapCursor(
        IFieldReads field, IAnnouncer announcer, Func<bool> on, Func<CursorBindings> keys, Func<FieldBindings> fieldKeys,
        MapBookmarks bookmarks, Func<int, NavEdges?> edges,
        Func<int?, float, IReadOnlyList<(float X, float Z, bool Objective)>> marks, Func<BeaconTarget, bool> guide,
        NamesTable? names = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(on);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(fieldKeys);
        ArgumentNullException.ThrowIfNull(bookmarks);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(guide);
        this.field = field;
        this.announcer = announcer;
        this.on = on;
        this.keys = keys;
        this.fieldKeys = fieldKeys;
        this.bookmarks = bookmarks;
        this.edges = edges;
        this.marks = marks;
        this.guide = guide;
        this.names = names;
        roam = new FreeRoamGate(field);
    }

    private enum Act
    {
        Start,
        Bookmark,
        Next,
        Previous,
        Guide,
    }

    /// <summary>Takes one read of the keys: with the cursor key held in free roam, acts on the arrows and the cursor's
    /// keys and hides them from the game; otherwise hands them on.</summary>
    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var k = this.keys();
        var f = fieldKeys();
        var held = k.CursorKey != 0 && keys.Contains(k.CursorKey);
        var pressed = new List<int>();
        bool mine;
        bool starting;
        lock (gate)
        {
            pressed.AddRange(keys.Where(key => !keysDown.Contains(key)));
            keysDown.Clear();
            keysDown.UnionWith(keys);
            var active = spot is not null && source == KeyboardSource;
            if (!held && active)
            {
                spot = null;
            }
            starting = held && !active && pressed.Contains(k.CursorKey);
            mine = held && (active || starting);
        }
        if (starting && !Begin(KeyboardSource))
        {
            return keys;
        }
        if (!mine)
        {
            return keys;
        }
        foreach (var key in pressed)
        {
            if (Arrows.FirstOrDefault(arrow => arrow.Key == key) is { Key: not 0 } arrow)
            {
                Move(arrow.Bearing);
            }
            else if (Of(key, k.BookmarkKey, f.NextKey, f.PreviousKey, f.GuideKey) is { } act)
            {
                Do(act);
            }
        }
        int[] hidden = [k.CursorKey, k.BookmarkKey, f.NextKey, f.PreviousKey, f.GuideKey, .. Arrows.Select(a => a.Key)];
        return [.. keys.Where(key => !hidden.Contains(key))];
    }

    /// <summary>Takes one read of the pad's buttons: while the pad's cursor is out, acts on the bookmark button and the
    /// next, previous and guide buttons and hides them; letting go of the hold buttons ends it.</summary>
    public int Pad(int pad, int buttons, bool made)
    {
        var f = fieldKeys();
        var k = keys();
        int pressed;
        bool active;
        lock (gate)
        {
            padDown.TryGetValue(pad, out var before);
            padDown[pad] = buttons;
            pressed = buttons & ~before;
            active = spot is not null && source == pad;
            if (active && (f.HoldButtons == 0 || (buttons & f.HoldButtons) != f.HoldButtons))
            {
                spot = null;
                stickSaid.Remove(pad);
                return buttons;
            }
        }
        if (!active)
        {
            return buttons;
        }
        (int Button, Act Act)[] chords =
            [(k.BookmarkButton, Act.Bookmark), (f.NextButton, Act.Next), (f.PreviousButton, Act.Previous), (f.GuideButton, Act.Guide)];
        var hidden = 0;
        foreach (var (button, act) in chords)
        {
            if (button == 0)
            {
                continue;
            }
            hidden |= button;
            if ((pressed & button) == button)
            {
                Do(act);
            }
        }
        return buttons & ~hidden;
    }

    /// <summary>Takes one read of the sticks: with the hold buttons held in free roam, each new push of the right stick
    /// past <see cref="StickScan.PushThreshold"/> moves the cursor a step that way, putting it out first; while it is
    /// out the game gets the right stick at rest.</summary>
    public PadSticks Sticks(int pad, int buttons, PadSticks sticks)
    {
        var hold = fieldKeys().HoldButtons;
        if (hold == 0 || (buttons & hold) != hold || !on())
        {
            lock (gate)
            {
                stickSaid.Remove(pad);
            }
            return sticks;
        }
        var (x, y) = sticks.Right;
        var length = Math.Sqrt((x * x) + (y * y));
        int? move = null;
        bool active;
        lock (gate)
        {
            active = spot is not null && source == pad;
            stickSaid.TryGetValue(pad, out var before);
            if (length >= StickScan.PushThreshold)
            {
                // Up is ahead and right is right, as the scan's; four ways, a quarter each.
                var at = (int)Math.Round(CameraFrame.Normalised(Math.Atan2(-x, y)) / (Math.PI / 2));
                at = at == -2 ? 2 : at;
                if (at != before)
                {
                    move = at;
                    stickSaid[pad] = at;
                }
            }
            else if (length < StickScan.RestThreshold)
            {
                stickSaid.Remove(pad);
            }
        }
        if (move is { } way && (active || Begin(pad)))
        {
            Move(way * Math.PI / 2);
            active = true;
        }
        return active ? sticks.RightAtRest() : sticks;
    }

    private static Act? Of(int key, int bookmark, int next, int previous, int guideKey) =>
        key == 0 ? null
        : key == bookmark ? Act.Bookmark
        : key == next ? Act.Next
        : key == previous ? Act.Previous
        : key == guideKey ? Act.Guide
        : null;

    // Puts the cursor on the player for source, saying so; false, saying nothing, when it can't be out.
    private bool Begin(int from)
    {
        if (!on() || !roam.Open())
        {
            return false;
        }
        if (field.ReadPose() is not { } pose || field.MapId() is not { } map)
        {
            Say("Can't read the field");
            return false;
        }
        lock (gate)
        {
            spot = new Spot(map, pose.X, pose.Y, pose.Z);
            source = from;
            jump = -1;
        }
        Say("Map cursor");
        return true;
    }

    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it says nothing.")]
    private void Move(double bearing)
    {
        try
        {
            if (field.ReadPose() is not { } pose)
            {
                return;
            }
            var heading = CameraFrame.Ahead(pose) + bearing;
            var distance = MoveSteps * Nearby.UnitsPerStep;
            Spot moved;
            lock (gate)
            {
                if (spot is not { } at)
                {
                    return;
                }
                // Kept to a hundredth of a unit, so a move and its way back land where they started.
                moved = at with
                {
                    X = (float)Math.Round(at.X + (Math.Sin(heading) * distance), 2),
                    Z = (float)Math.Round(at.Z + (Math.Cos(heading) * distance), 2),
                };
                spot = moved;
            }
            Say(Describe(moved, pose));
        }
        catch (Exception)
        {
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "A failed read must never reach the game; it says nothing.")]
    private void Do(Act act)
    {
        Spot? at;
        lock (gate)
        {
            at = spot;
        }
        if (at is not { } here)
        {
            return;
        }
        try
        {
            switch (act)
            {
                case Act.Bookmark:
                    Say(bookmarks.Add(here.Map, here.X, here.Y, here.Z) is { } mark
                        ? string.Create(CultureInfo.InvariantCulture, $"Bookmark {mark.Number} saved")
                        : "Bookmarks full on this map");
                    break;
                case Act.Next or Act.Previous:
                    Jump(here.Map, act == Act.Next ? 1 : -1);
                    break;
                case Act.Guide:
                    Guide(here);
                    break;
            }
        }
        catch (Exception)
        {
        }
    }

    private void Jump(int map, int by)
    {
        var marked = bookmarks.On(map);
        if (marked.Count == 0)
        {
            Say("No bookmarks on this map");
            return;
        }
        Spot moved;
        int at;
        lock (gate)
        {
            at = jump < 0 ? (by > 0 ? 0 : marked.Count - 1) : (((jump + by) % marked.Count) + marked.Count) % marked.Count;
            jump = at;
            var mark = marked[at];
            moved = new Spot(map, mark.X, mark.Y, mark.Z);
            spot = moved;
        }
        if (field.ReadPose() is { } pose)
        {
            Say(string.Create(CultureInfo.InvariantCulture,
                $"Bookmark {marked[at].Number} of {marked.Count}, {Describe(moved, pose)}"));
        }
    }

    private void Guide(Spot here)
    {
        if (field.ReadPose() is not { } pose)
        {
            Say("Can't read the field");
            return;
        }
        if (Ground(pose.X, pose.Z, here.X, here.Z) < Nearby.UnitsPerStep / 2)
        {
            Say("The cursor is at you");
            return;
        }
        var name = BookmarkAt(here) is { } mark
            ? string.Create(CultureInfo.InvariantCulture, $"bookmark {mark.Number}")
            : "the map cursor";
        if (!guide(new BeaconTarget(new FieldUnit(FieldKind.Person, here.X, here.Y, here.Z, name), null)))
        {
            Say("Guidance is off");
        }
    }

    private MapBookmark? BookmarkAt(Spot here) => bookmarks.On(here.Map)
        .Where(mark => Ground(mark.X, mark.Z, here.X, here.Z) <= BookmarkSteps * Nearby.UnitsPerStep)
        .MinBy(mark => Ground(mark.X, mark.Z, here.X, here.Z));

    // "Bookmark 2, Objective marker, Door, open, 10 steps ahead-left": what lies at the cursor, then where it is.
    private string Describe(Spot here, FieldPose pose)
    {
        var parts = new List<string>();
        if (BookmarkAt(here) is { } mark)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"Bookmark {mark.Number}"));
        }
        var there = ThereSteps * Nearby.UnitsPerStep;
        var icon = marks(here.Map, here.Y)
            .Where(point => Ground(point.X, point.Z, here.X, here.Z) <= there)
            .OrderBy(point => Ground(point.X, point.Z, here.X, here.Z))
            .Select(point => (Found: true, point.Objective))
            .FirstOrDefault();
        if (icon.Found)
        {
            parts.Add(icon.Objective ? "Objective marker" : "Map icon");
        }
        var view = field.Read() is { } read ? names?.Apply(read) ?? read : null;
        if (view?.Units.Where(unit => Ground(unit.X, unit.Z, here.X, here.Z) <= there)
                .MinBy(unit => Ground(unit.X, unit.Z, here.X, here.Z)) is { } unit)
        {
            parts.Add(unit.Name ?? Nearby.KindWord(unit.Says));
        }
        if (edges(here.Map)?.Open is { } open)
        {
            parts.Add(open(new Vector3(here.X, here.Y, here.Z)) ? "open" : "wall");
        }
        var distance = Ground(pose.X, pose.Z, here.X, here.Z);
        if (distance < Nearby.UnitsPerStep / 2)
        {
            parts.Add("at you");
        }
        else
        {
            var steps = Nearby.Steps(distance);
            parts.Add(string.Create(CultureInfo.InvariantCulture,
                $"{steps} {(steps == 1 ? "step" : "steps")} {Nearby.Direction(CameraFrame.Bearing(pose, here.X, here.Z))}"));
        }
        var line = string.Join(", ", parts);
        return char.ToUpperInvariant(line[0]) + line[1..];
    }

    private void Say(string text) => announcer.Announce(new Announcement(text, Interrupt: true));

    private static double Ground(double ax, double az, double bx, double bz)
    {
        var dx = ax - bx;
        var dz = az - bz;
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    private readonly record struct Spot(int Map, float X, float Y, float Z);
}
