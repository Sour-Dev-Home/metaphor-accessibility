using System.Diagnostics.CodeAnalysis;

namespace Metaphor.Core.Menus;

/// <summary>A screen the mod knows (spec #211): its name, and the screen class that identifies it, as the game's
/// run-time type information names it (such as <c>TitleChoice@ope@ui@app</c>). Each screen's feature declares its
/// own. <paramref name="OutsideMenus"/> marks a screen the game also shows while its menus read closed, such as a
/// yes/no dialog in a conversation or New Game's difficulty list (#540): it is told then too, and each new manager
/// enters it afresh, since nothing leaves it there.</summary>
public sealed record Screen(string Name, string ScreenClass, bool OutsideMenus = false);

/// <summary>What happened to a screen.</summary>
public enum ScreenChange
{
    /// <summary>Its cursor settled after another screen's: <see cref="ScreenEvent.Index"/> is the focus.</summary>
    Entered,

    /// <summary>Another screen's cursor settled: <see cref="ScreenEvent.Index"/> is this screen's last focus.</summary>
    Left,

    /// <summary>Its cursor settled on another item: <see cref="ScreenEvent.Index"/> is the new focus.</summary>
    FocusMoved,

    /// <summary>A settings page's values changed with its focus where it was (#96): <see cref="ScreenEvent.Values"/> are
    /// the new ones.</summary>
    ValueChanged,
}

/// <summary>One change to a screen: which (<paramref name="Screen"/>, null for one the mod doesn't know), its screen
/// class, the focus's position from 0, and the list's item count the game keeps (<see cref="CursorInput.Count"/>), null
/// when unreadable. <paramref name="Mode"/> is which list a choice that serves more than one shows
/// (<see cref="CursorInput.Mode"/>), null when it has none or it is unreadable. <paramref name="OffList"/> is whether
/// the focus is off the list's items (<see cref="CursorInput.OffList"/>), as on a settings page's OK button, where
/// <paramref name="Index"/> reads 0. <paramref name="Grid"/> is the focus's cell on a grid cursor
/// (<see cref="CursorInput.Grid"/>, #563), null on any other. <paramref name="Values"/> is a settings page's values
/// (<see cref="CursorInput.Values"/>, #96), null on any other screen.</summary>
public sealed record ScreenEvent(
    ScreenChange Change, Screen? Screen, string ScreenClass, int Index, int? Count = null, int? Mode = null,
    bool OffList = false, GridCursor? Grid = null, SettingValues? Values = null)
{
    /// <summary>The longest list taken as a count (#289). The longest seen in session 6 had 145 items
    /// (docs/raw-sources/list-count-session6-2026-11-17.md); a larger value is a screen whose field isn't the count, as
    /// far as we know (<c>[NEEDS VERIFICATION]</c>: whether any list is longer).</summary>
    public const int LongestList = 999;

    /// <summary>The list's total when it is known, holds the focus and is no longer than <see cref="LongestList"/>,
    /// else null.</summary>
    public int? Total => Count is { } count && Index >= 0 && Index < count && count <= LongestList ? count : null;
}

/// <summary>Told of each screen entered, left, and focus moved.</summary>
public interface IScreenObserver
{
    void Observe(ScreenEvent change);
}

/// <summary>A screen's feature that answers the details key (#214).</summary>
public interface IScreenDetails
{
    /// <summary>The rest of the focused item's panel when <paramref name="focus"/> is on this feature's screen, or null
    /// when the screen isn't its own.</summary>
    string? Details(ScreenEvent focus);
}

/// <summary>Turns menu cursor inputs into screen entered, screen left and focus moved (spec #211). The screen is the
/// input's screen class, never its manager: managers are made per visit and one cursor class serves nearly every list
/// (docs/raw-sources/screen-class-session-2026-11-17.md). Only inputs where the cursor settled count (a move can be put
/// back, <see cref="CursorInput.IsMove"/>); an input without a screen class is skipped. Observers hear each change in
/// the order they were added, and one that throws never stops the rest. Add observers before the hooks go live. It also
/// knows whether the game has its menus open (#268), from the reads the input hooks hand it: only while they are open
/// does a cursor count, except on a screen shown outside the menus (<see cref="Screen.OutsideMenus"/>, #540), and when
/// they close, the current screen is left. A screen's mode (<see cref="CursorInput.Mode"/>)
/// changing on the same item is a focus move, so a choice's other list is heard even where the index stays; so is the
/// focus moving on or off the list's items (<see cref="CursorInput.OffList"/>), as from a settings page's OK button to
/// its first row, both at index 0 (#560); and on a grid cursor, its count changing on the same cell, as when Key
/// Configuration's sub-tab changes (#589). A settings page's values (<see cref="CursorInput.Values"/>) changing with
/// nothing else is a value change (#96); values that stop being read keep the last ones read.
/// </summary>
public sealed class MenuScreens : ICursorObserver
{
    private readonly Dictionary<string, Screen> known;
    private readonly Lock gate = new();
    private IScreenObserver[] observers = [];
    private string? currentClass;
    private long currentManager;
    private int currentIndex;
    private int? currentCount;
    private int? currentMode;
    private bool currentOffList;
    private GridCursor? currentGrid;
    private SettingValues? currentValues;
    private bool menusOpen;

    /// <summary>A module that tags each screen whose class is one of <paramref name="knownScreens"/>' with it.</summary>
    public MenuScreens(IEnumerable<Screen> knownScreens)
    {
        ArgumentNullException.ThrowIfNull(knownScreens);
        known = knownScreens.ToDictionary(screen => screen.ScreenClass, StringComparer.Ordinal);
    }

    /// <summary>Adds an observer of screen changes, after those already added.</summary>
    public void Observe(IScreenObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (gate)
        {
            observers = [.. observers, observer];
        }
    }

    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.IsMove || input.ScreenClass is not { } screenClass)
        {
            return;
        }
        var outside = known.GetValueOrDefault(screenClass)?.OutsideMenus == true;
        ScreenEvent[] changes;
        lock (gate)
        {
            if (!menusOpen && !outside)
            {
                return;
            }
            if (screenClass == currentClass && (!outside || input.Manager == currentManager))
            {
                var values = input.Values ?? currentValues;
                if (input.Index == currentIndex && input.Mode == currentMode && input.OffList == currentOffList
                    && (input.Grid is null || input.Count == currentCount))
                {
                    if (Equals(values, currentValues))
                    {
                        return;
                    }
                    changes = [Change(ScreenChange.ValueChanged, screenClass, input, values)];
                }
                else
                {
                    changes = [Change(ScreenChange.FocusMoved, screenClass, input, values)];
                }
                currentValues = values;
            }
            else
            {
                var entered = Change(ScreenChange.Entered, screenClass, input, input.Values);
                changes = currentClass is { } left ? [Current(ScreenChange.Left, left), entered] : [entered];
                currentValues = input.Values;
            }
            currentClass = screenClass;
            currentManager = input.Manager;
            currentIndex = input.Index;
            currentCount = input.Count;
            currentMode = input.Mode;
            currentOffList = input.OffList;
            currentGrid = input.Grid;
        }
        var each = Volatile.Read(ref observers);
        foreach (var change in changes)
        {
            TellEach(each, change);
        }
    }

    /// <summary>Whether the last read (<see cref="MenusRead"/>) found the game's menus open: false before any read.
    /// </summary>
    public bool MenusOpen => Volatile.Read(ref menusOpen);

    /// <summary>Takes one read of whether the game has its menus open; a read that failed is closed, so menu-only
    /// controls give their input back to the game. When the menus close, the current screen is left at its last focus
    /// and forgotten: nothing repeats it, and the next cursor to settle once they open, even on the same screen, enters
    /// afresh. A cursor that settles while the menus are closed (a dialogue's choice, a battle's commands) is no menu
    /// screen and tells nothing, unless its screen is one shown outside the menus.</summary>
    /// <remarks>What the game's state is, and that it reads open on the title screen too, is in
    /// docs/raw-sources/menus-open-session6-2026-11-17.md.</remarks>
    public void MenusRead(bool open)
    {
        ScreenEvent? left = null;
        lock (gate)
        {
            if (open == menusOpen)
            {
                return;
            }
            Volatile.Write(ref menusOpen, open);
            if (!open && currentClass is { } screenClass)
            {
                left = Current(ScreenChange.Left, screenClass);
                currentClass = null;
            }
        }
        if (left is not null)
        {
            TellEach(Volatile.Read(ref observers), left);
        }
    }

    /// <summary>Tells every observer the current screen as entered again at its focus, as when the reader is turned
    /// back on (#214); nothing when no screen has been entered.</summary>
    public void RepeatCurrent()
    {
        ScreenEvent change;
        lock (gate)
        {
            if (currentClass is not { } screenClass)
            {
                return;
            }
            change = Current(ScreenChange.Entered, screenClass);
        }
        TellEach(Volatile.Read(ref observers), change);
    }

    // The current focus as a change to screenClass; called under the gate.
    private ScreenEvent Current(ScreenChange change, string screenClass) =>
        new(change, known.GetValueOrDefault(screenClass), screenClass, currentIndex, currentCount, currentMode,
            currentOffList, currentGrid, currentValues);

    private ScreenEvent Change(ScreenChange change, string screenClass, CursorInput input, SettingValues? values) =>
        new(change, known.GetValueOrDefault(screenClass), screenClass, input.Index, input.Count, input.Mode,
            input.OffList, input.Grid, values);

    [SuppressMessage("Design", "CA1031", Justification = "One observer's failure must not keep the change from the others, or reach the game.")]
    private static void TellEach(IScreenObserver[] observers, ScreenEvent change)
    {
        foreach (var observer in observers)
        {
            try
            {
                observer.Observe(change);
            }
            catch (Exception)
            {
            }
        }
    }
}
