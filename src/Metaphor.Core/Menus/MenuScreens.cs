using System.Diagnostics.CodeAnalysis;

namespace Metaphor.Core.Menus;

/// <summary>A screen the mod knows (spec #211): its name, and the screen class that identifies it, as the game's
/// run-time type information names it (such as <c>TitleChoice@ope@ui@app</c>). Each screen's feature declares its
/// own.</summary>
public sealed record Screen(string Name, string ScreenClass);

/// <summary>What happened to a screen.</summary>
public enum ScreenChange
{
    /// <summary>Its cursor settled after another screen's: <see cref="ScreenEvent.Index"/> is the focus.</summary>
    Entered,

    /// <summary>Another screen's cursor settled: <see cref="ScreenEvent.Index"/> is this screen's last focus.</summary>
    Left,

    /// <summary>Its cursor settled on another item: <see cref="ScreenEvent.Index"/> is the new focus.</summary>
    FocusMoved,
}

/// <summary>One change to a screen: which (<paramref name="Screen"/>, null for one the mod doesn't know), its screen
/// class, and the focus's position from 0.</summary>
public sealed record ScreenEvent(ScreenChange Change, Screen? Screen, string ScreenClass, int Index);

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
/// knows whether the game has its menus open (#268), from the reads the input hooks hand it: when they close, the
/// current screen is left.
/// </summary>
public sealed class MenuScreens : ICursorObserver
{
    private readonly Dictionary<string, Screen> known;
    private readonly Lock gate = new();
    private IScreenObserver[] observers = [];
    private string? currentClass;
    private int currentIndex;
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
        ScreenEvent[] changes;
        lock (gate)
        {
            if (screenClass == currentClass)
            {
                if (input.Index == currentIndex)
                {
                    return;
                }
                changes = [Change(ScreenChange.FocusMoved, screenClass, input.Index)];
            }
            else
            {
                var entered = Change(ScreenChange.Entered, screenClass, input.Index);
                changes = currentClass is { } left ? [Change(ScreenChange.Left, left, currentIndex), entered] : [entered];
            }
            currentClass = screenClass;
            currentIndex = input.Index;
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
    /// and forgotten: nothing repeats it, and the next cursor to settle, even on the same screen, enters afresh. A
    /// cursor that settles while the menus are closed (a dialogue's choice) still enters its screen.</summary>
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
                left = Change(ScreenChange.Left, screenClass, currentIndex);
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
            change = Change(ScreenChange.Entered, screenClass, currentIndex);
        }
        TellEach(Volatile.Read(ref observers), change);
    }

    private ScreenEvent Change(ScreenChange change, string screenClass, int index) =>
        new(change, known.GetValueOrDefault(screenClass), screenClass, index);

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
