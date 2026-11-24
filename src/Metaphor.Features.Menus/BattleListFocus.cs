using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks battle's skill list or item list (#247): on entering it "Skills, name, n of total", then on each focus
/// move "name, n of total", each cutting off the last. A row's name is the message the list fetched by serial for it,
/// from the names file (<see cref="MessageSerials"/>). The names fetched for the list's first page, in the order fetched,
/// are taken as its rows from the top, as the quick menu's are (docs/findings/message-serials-from-file-positions.md).
/// Past the first page each row keeps its own name by its place in the whole list, so a scroll never shifts them. A row
/// whose name hasn't been fetched yet is said once it is; one whose name can't be told, or has no known text, is said by
/// its position, never by another row's name. Safe to call from several threads.</summary>
/// <remarks>
/// <para>On build 548ddc95 the lists are <c>BTL_SkillSelect</c> and <c>BTL_ItemSelect</c>, shown while the menus read
/// closed; they fetch a row's name with flags 9 and its description with flags 7 (#247's by-ear run of 2026-11-20).
/// That the fetch order is the cursor order on these lists is <c>[NEEDS VERIFICATION]</c> by ear.</para>
/// <para>A page fetched more than <see cref="RunGapMilliseconds"/> after the list's last focus or fetch, then entered or
/// focused at its top, starts the rows afresh, so a list opened again, for another member or on another tab, starts
/// afresh even though the game fetches its names before its cursor settles. On a move by one row the game fetches the
/// row the move scrolled in along with rows it redraws, some of the page and not always all of it: the row brought in is
/// the last name fetched going down (docs/raw-sources/battle-item-list-scroll-by-ear-2026-11-22.md). Going up it is
/// taken as the first, as on the settings pages (docs/raw-sources/settings-ok-and-rows-by-ear-2026-11-21.md); for the
/// battle lists that is <c>[NEEDS VERIFICATION]</c>. When several names were fetched (the whole view again after a wrap,
/// #737) the row is named only if they match rows already known; else it is said by its position. A whole view
/// fetched again, as many names as the first page, names every row it holds, lined up by its known rows or, after a
/// wrap up, ending at the last row (<see cref="MenusFeature.BattleListViews"/>; F8 marks 18 to 20 of the by-ear session
/// of 2026-11-22, Steam build 18330018; <c>[NEEDS VERIFICATION]</c> on other lists).</para>
/// </remarks>
[FeatureSetting<MenusFeature>(MenusFeature.BattleLists)]
public sealed class BattleListFocus : IScreenObserver, ISerialResolveObserver, IScreenDetails
{
    /// <summary>The flags the battle lists fetch a row's name with.</summary>
    public const int NameFlags = 9;

    /// <summary>The flags they fetch the focused row's description with, for its help line.</summary>
    public const int DescriptionFlags = 7;

    /// <summary>How long after the list's last focus or fetch a name fetch starts a new run of rows: many frames, far
    /// less than a turn (<see cref="CursorEvents.QuietMilliseconds"/>).</summary>
    public const long RunGapMilliseconds = CursorEvents.QuietMilliseconds;

    /// <summary>The longest pause between two names of one fetch burst. In the by-ear session of 2026-11-23 (build
    /// 548ddc95, Steam 18330018) the names of one burst came at most 34 ms apart and separate bursts at least 100 ms apart
    /// (docs/raw-sources/battle-item-tabs-wrap-log-2026-11-23.md).</summary>
    public const long BurstMilliseconds = 50;

    // The most rows kept: the longest list taken as a count (ScreenEvent.LongestList).
    private const int MaxRows = ScreenEvent.LongestList;

    private readonly MessageSerials serials;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly string namesFile;
    private readonly string descriptionsFile;
    private readonly Func<long> millisecondsNow;
    private readonly Func<bool> viewsOn;
    private readonly Lock gate = new();
    // How many names the first page's run fetched: the view's rows, once the list is longer (#737).
    private int pageSize;
    // Each row's name serial by the row's place in the whole list, from 0.
    private readonly Dictionary<int, int> rows = [];
    // Names fetched since the last focus once the first page's run is over, for the next focus to place.
    private readonly List<int> pending = [];
    // Where each fetch burst begins in pending, and when the last name was fetched (#737).
    private readonly List<int> bursts = [];
    private long lastNameAt = long.MinValue / 2;
    // Each row's description by its name's serial, and the name fetched last, for the next description to pair with.
    private readonly Dictionary<int, int> descriptions = [];
    private int? lastName;
    private ScreenEvent? waiting;
    private ScreenEvent? lastFocus;
    private long lastActivity = long.MinValue / 2;
    private bool onList;
    private bool otherScreen;
    // While the first page's run is open, each name fetched is the next row; a focus move ends it.
    private bool runOpen;
    // Whether the pending names began after a gap: a fresh page, or a scroll's fetch.
    private bool pendingAfterGap;

    // Steps, so a Left can tell whether the rows were fetched for the visit it ends or after it.
    private long step;
    private long runStep;
    private long focusStep;

    private BattleListFocus(
        Screen screen, string namesFile, string descriptionsFile, MessageSerials serials, IAnnouncer announcer,
        TextCleaner cleaner, Func<long> millisecondsNow, Func<bool>? viewsOn)
    {
        this.viewsOn = viewsOn ?? (() => true);
        this.descriptionsFile = descriptionsFile;
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(millisecondsNow);
        Screen = screen;
        this.namesFile = namesFile;
        this.serials = serials;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.millisecondsNow = millisecondsNow;
    }

    /// <summary>Battle's skill list, by its owner's class on build 548ddc95 (#247).</summary>
    public static Screen SkillList { get; } = new("Skills", "BTL_SkillSelect@ope@ui@app", OutsideMenus: true);

    /// <summary>Battle's item list, by its owner's class on build 548ddc95 (#247).</summary>
    public static Screen ItemList { get; } = new("Items", "BTL_ItemSelect@ope@ui@app", OutsideMenus: true);

    /// <summary>The screen this one speaks.</summary>
    public Screen Screen { get; }

    /// <summary>The skill list, its names from the skill names file, timed by <paramref name="millisecondsNow"/>;
    /// <paramref name="viewsOn"/> says whether a whole view fetched again names its rows (#737,
    /// <see cref="MenusFeature.BattleListViews"/>), on when null.</summary>
    public static BattleListFocus Skills(
        MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner, Func<long> millisecondsNow,
        Func<bool>? viewsOn = null) =>
        new(SkillList, MessageSerials.SkillNames, MessageSerials.SkillDescriptions, serials, announcer, cleaner,
            millisecondsNow, viewsOn);

    /// <summary>The item list, its names from the item names file, timed by <paramref name="millisecondsNow"/>;
    /// <paramref name="viewsOn"/> as for <see cref="Skills"/>.</summary>
    public static BattleListFocus Items(
        MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner, Func<long> millisecondsNow,
        Func<bool>? viewsOn = null) =>
        new(ItemList, MessageSerials.ItemNames, MessageSerials.ItemDescriptions, serials, announcer, cleaner,
            millisecondsNow, viewsOn);

    /// <summary>The focused row's description for the details key (#247): the description fetched from the list's
    /// descriptions file right after the row's name. Null on another screen or for a row with none.</summary>
    /// <remarks>On build 548ddc95 the item list, opening, fetched each row's name then its description, row by row
    /// (name 5143, description 5916, name 5144, description 5917: the maintainer's by-ear run of 2026-11-21). That the
    /// skill list fetches them in the same order is <c>[NEEDS VERIFICATION]</c>; its names and descriptions don't sit
    /// at the same file positions (docs/findings/item-skill-message-serials-static.md), so positions can't pair them.
    /// The skill's cost is not read.</remarks>
    public string? Details(ScreenEvent focus)
    {
        ArgumentNullException.ThrowIfNull(focus);
        int? serial = null;
        lock (gate)
        {
            if (focus.Screen == Screen && onList && rows.TryGetValue(focus.Index, out var name)
                && descriptions.TryGetValue(name, out var found))
            {
                serial = found;
            }
        }
        return serial is { } known ? serials.TextOf(known, cleaner) : null;
    }

    /// <summary>Speaks the focus when the list is entered or its focus moves, or waits for its row's name. Leaving the
    /// list forgets the rows its visit used; entering another screen forgets them all.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Announcement? said;
        lock (gate)
        {
            if (change.Screen != Screen)
            {
                // While another screen is current (a field menu, the title), its fetches are none of this list's. The
                // other battle list is the exception: backing out of it to battle's commands tells nothing, as they are
                // no screen, so it stays current until this list's cursor settles, after this list fetched its names
                // (by-ear run of 2026-11-21). Its own fetches are from other files, which this list ignores anyway.
                if (change.Change == ScreenChange.Entered)
                {
                    Forget();
                    onList = false;
                    var otherList = change.Screen == SkillList || change.Screen == ItemList;
                    Volatile.Write(ref otherScreen, !otherList);
                }
                else if (change.Change == ScreenChange.Left)
                {
                    Volatile.Write(ref otherScreen, false);
                }
                return;
            }
            Volatile.Write(ref otherScreen, false);
            waiting = null;
            if (change.Change == ScreenChange.Left)
            {
                onList = false;
                lastFocus = null;
                // Names fetched after this visit's last focus belong to the next visit, which fetched them already.
                if (pending.Count > 0)
                {
                    StartRun(pending);
                }
                else if (runStep < focusStep)
                {
                    Forget();
                }
                return;
            }
            var now = millisecondsNow();
            // The list fetches its names just before its cursor settles: rows fetched long before it opened came from
            // something else. Entered again while on the list (the reader turned back on) keeps them.
            if (change.Change == ScreenChange.Entered && !onList && now - lastActivity > RunGapMilliseconds)
            {
                Forget();
            }
            Place(change);
            onList = true;
            lastActivity = now;
            lastFocus = change;
            focusStep = ++step;
            said = LineFor(change);
            if (said is null && change.Index >= 0)
            {
                // Before any row is known the list's first names are still to come. Past them a row whose name can't
                // be told is said by its position, never left silent: a name fetched after its focus can't be told
                // from the next move's.
                if (rows.Count > 0)
                {
                    said = PositionLine(change);
                }
                else
                {
                    waiting = change;
                }
            }
        }
        if (said is not null)
        {
            announcer.Announce(said);
        }
    }

    /// <summary>Whether this list wants the game's fetches: not while another screen is current, so the hook can skip
    /// its reads then.</summary>
    public bool Listening => !Volatile.Read(ref otherScreen);

    /// <summary>Keeps each row name the game fetches from the names file, in order, and speaks a focus that was waiting
    /// for it. Ignored while another screen is current.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (Volatile.Read(ref otherScreen))
        {
            return;
        }
        if (resolve.Flags == DescriptionFlags && serials.InFile(resolve.Serial, descriptionsFile))
        {
            lock (gate)
            {
                // A description is the row's whose name was fetched just before it; one after no name (the help line
                // fetched again) changes nothing.
                if (lastName is { } name)
                {
                    descriptions[name] = resolve.Serial;
                    lastName = null;
                }
            }
            return;
        }
        if (resolve.Flags != NameFlags || !serials.InFile(resolve.Serial, namesFile))
        {
            return;
        }
        Announcement? said = null;
        lock (gate)
        {
            var now = millisecondsNow();
            var gap = now - lastActivity > RunGapMilliseconds;
            var newBurst = now - lastNameAt > BurstMilliseconds;
            lastActivity = now;
            lastNameAt = now;
            lastName = resolve.Serial;
            if (rows.Count == 0 && pending.Count == 0)
            {
                descriptions.Clear();
                StartRun([resolve.Serial]);
            }
            // A run a focus has already spoken takes no later burst: that is a move's fetch, such as a wrap's view
            // fetched within the run gap of a tab's first page (#737).
            else if (runOpen && !gap && pending.Count == 0 && !(newBurst && focusStep > runStep && viewsOn()))
            {
                if (rows.Count < MaxRows && !rows.ContainsValue(resolve.Serial))
                {
                    rows[rows.Count] = resolve.Serial;
                }
            }
            else
            {
                if (pending.Count == 0)
                {
                    pendingAfterGap = gap;
                }
                if (pending.Count < MaxRows)
                {
                    if (pending.Count == 0 || newBurst)
                    {
                        bursts.Add(pending.Count);
                    }
                    pending.Add(resolve.Serial);
                }
            }
            if (waiting is { } focus && (said = LineFor(focus)) is not null)
            {
                waiting = null;
                focusStep = ++step;
            }
        }
        if (said is not null)
        {
            announcer.Announce(said);
        }
    }

    // Called under the gate.
    private void Forget()
    {
        rows.Clear();
        ClearPending();
        descriptions.Clear();
        lastName = null;
        waiting = null;
        runOpen = false;
    }

    // Takes names, in the order fetched, as the rows from the top, and keeps the run open for more. Called under the
    // gate.
    private void StartRun(IEnumerable<int> names)
    {
        var fresh = names.Distinct().Take(MaxRows).ToList();
        rows.Clear();
        ClearPending();
        foreach (var name in fresh)
        {
            rows[rows.Count] = name;
        }
        runOpen = true;
        runStep = ++step;
    }

    // Places the names fetched since the last focus, for this focus. A move by one row takes the row it scrolled in from
    // them, unless another row has that name; a page fetched afresh (on entering, or after a gap or on a list of another
    // length with the focus on the top row, as on another tab or a wrap to the top) starts the rows over; any other
    // jump to a list of another length forgets them. Called under the gate.
    private void Place(ScreenEvent focus)
    {
        var moved = focus.Change == ScreenChange.FocusMoved;
        var otherLength = moved && lastFocus is { } before && focus.Count != before.Count;
        var byOne = moved && !otherLength && lastFocus is { } last && Math.Abs(focus.Index - last.Index) == 1;
        if (moved && runOpen)
        {
            runOpen = false;
            pageSize = rows.Count;
        }
        if (pending.Count > 0
            && (focus.Change == ScreenChange.Entered || (!byOne && focus.Index == 0 && (pendingAfterGap || otherLength))))
        {
            StartRun(pending);
            return;
        }
        if (otherLength && pending.Count > 0 && pendingAfterGap && viewsOn())
        {
            AnotherTab(focus);
            return;
        }
        if (otherLength)
        {
            Forget();
        }
        else if (moved && viewsOn())
        {
            PlaceView(focus, byOne, fromTop: lastFocus is { Index: 0 });
        }
        if (!otherLength && byOne && pending.Count > 0 && !rows.ContainsKey(focus.Index))
        {
            var down = focus.Index > lastFocus!.Index;
            var brought = down ? pending[^1] : pending[0];
            if (!rows.ContainsValue(brought) && Matches(focus.Index, down))
            {
                rows[focus.Index] = brought;
            }
        }
        ClearPending();
    }

    // Called under the gate.
    private void ClearPending()
    {
        pending.Clear();
        bursts.Clear();
    }

    // A move on another tab whose own switch the game reported no focus for, as its cursor stayed on the top row with
    // only the count changed (#737: the HP recovery tab of 2026-11-23). The first burst fetched after the gap is the
    // tab's first page, its rows from the top; the last burst, if another, is this move's fetch, placed as a view
    // fetched from the top row. Called under the gate.
    private void AnotherTab(ScreenEvent focus)
    {
        var pageEnd = bursts.Count > 1 ? bursts[1] : pending.Count;
        var moveFetch = bursts.Count > 1 ? pending.Skip(bursts[^1]).ToList() : [];
        StartRun(pending.Take(pageEnd));
        runOpen = false;
        pageSize = rows.Count;
        pending.AddRange(moveFetch);
        PlaceView(focus, byOne: false, fromTop: true);
        ClearPending();
    }

    // Names a view fetched again (#737): as many names as the first page, taken as that many rows in a row, in list
    // order (by ear on 2026-11-22, the 14- and 40-item tabs; a partial fetch can skip rows, so only a whole view is
    // placed). It lines up by the rows it holds that are already known, all at one offset; holding none, after a jump to
    // the last row (a wrap up), it ends there. A wrap up from the top row of a list shorter than two views fetches only
    // the rows it brings into view, none known (the 11-item tab of 2026-11-23,
    // docs/raw-sources/battle-item-tabs-wrap-log-2026-11-23.md): exactly that many names end at the last row. A view
    // that would put a name on another known row, or out of the list, names nothing. Called under the gate.
    private void PlaceView(ScreenEvent focus, bool byOne, bool fromTop)
    {
        if (pageSize < 2 || pending.Distinct().Count() != pending.Count || focus.Total is not { } total)
        {
            return;
        }
        var brought = fromTop && !byOne && focus.Index == total - 1 && total > pageSize
            && pending.Count == Math.Min(pageSize, total - pageSize);
        if (pending.Count != pageSize && !brought)
        {
            return;
        }
        if (pending.Count != pageSize && pending.Any(rows.ContainsValue))
        {
            return;
        }
        int? offset = null;
        for (var at = 0; at < pending.Count; at++)
        {
            if (rows.FirstOrDefault(row => row.Value == pending[at], new KeyValuePair<int, int>(-1, 0)).Key is var known
                && known >= 0)
            {
                if (offset is { } other && other != known - at)
                {
                    return;
                }
                offset = known - at;
            }
        }
        if (offset is null && !byOne && focus.Index == total - 1)
        {
            offset = total - pending.Count;
        }
        if (offset is not { } first || first < 0 || first + pending.Count > total)
        {
            return;
        }
        for (var at = 0; at < pending.Count; at++)
        {
            if (rows.TryGetValue(first + at, out var held) && held != pending[at])
            {
                return;
            }
        }
        for (var at = 0; at < pending.Count; at++)
        {
            rows[first + at] = pending[at];
        }
    }

    // Whether the names fetched can be matched to the focused row (#737). One name fetched is the row scrolled in. When
    // the game fetched several, as on re-fetching the whole view after a wrap, the row brought in is the last going down
    // and the first going up only if the other names are rows already known lying on the side they should (above the
    // row going down, below it going up), and at least one is known; otherwise the first or last fetched may be the
    // view's top or bottom, not the focused row, and it is left unsaid. Called under the gate.
    private bool Matches(int focusIndex, bool down)
    {
        if (pending.Count == 1)
        {
            return true;
        }
        var others = down ? pending.Take(pending.Count - 1) : pending.Skip(1);
        var known = 0;
        foreach (var name in others)
        {
            var at = rows.FirstOrDefault(row => row.Value == name, new KeyValuePair<int, int>(-1, 0)).Key;
            if (at < 0)
            {
                continue;
            }
            if (down ? at >= focusIndex : at <= focusIndex)
            {
                return false;
            }
            known++;
        }
        return known > 0;
    }

    // The line for a focus whose row's name has been fetched, or null while it hasn't. Called under the gate.
    private Announcement? LineFor(ScreenEvent focus) =>
        rows.TryGetValue(focus.Index, out var row) ? Line(focus, serials.TextOf(row, cleaner)) : null;

    // The line for a focus whose row's name can't be told: its position alone.
    private Announcement PositionLine(ScreenEvent focus) => Line(focus, null);

    private Announcement Line(ScreenEvent focus, string? name)
    {
        var item = focus.Positioned(name);
        return new Announcement(focus.Change == ScreenChange.Entered ? $"{Screen.Name}, {item}" : item, Interrupt: true);
    }
}
