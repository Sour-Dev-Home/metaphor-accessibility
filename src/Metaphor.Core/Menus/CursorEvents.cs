using System.Diagnostics.CodeAnalysis;

namespace Metaphor.Core.Menus;

/// <summary>One input a menu's cursor manager handled (#94): the manager, its class (its vtable as a module offset, null
/// when unreadable), the command and what the handler returned, the cursor after it ran (<c>top</c> plus <c>row</c>) and
/// the manager's flags. Fields, command range and build: docs/raw-sources/ui-focus-candidates-2026-11-16.md. Numbers
/// only, no game text.</summary>
public sealed record CursorInput(long Manager, long? ClassRva, int Command, int Result, int Top, int Row, int Flags)
{
    /// <summary>The cursor's position: what the class's index getter returns.</summary>
    public int Index => Top + Row;

    /// <summary>Whether this is one of the four commands the handler moves the cursor for (<c>0x14</c>–<c>0x17</c>). A
    /// move can be refused and put back on the next call (docs/raw-sources/title-menu-labels-2026-11-16.md), so where
    /// the cursor settled is the next input that isn't a move.</summary>
    public bool IsMove => Command is >= 0x14 and <= 0x17;
}

/// <summary>Told of each menu cursor input that changed something.</summary>
public interface ICursorObserver
{
    void Observe(CursorInput input);
}

/// <summary>Hands each input the menu cursor hook decoded to its observers (#94), as <see cref="Messages.MessageEvents"/>
/// does for messages. The game calls the handler on every frame a menu is open, so an input equal to that manager's
/// last one is dropped. Observers hear each input in the order they were added, and one that throws never stops the
/// rest. Add observers before the hooks go live; telling them is safe from any game thread.</summary>
public sealed class CursorEvents
{
    // Managers are game objects that come and go; the last inputs of this many are kept before starting afresh.
    private const int ManagersRemembered = 64;

    private readonly Lock gate = new();
    private readonly Dictionary<long, CursorInput> lastByManager = [];
    private ICursorObserver[] observers = [];

    /// <summary>Adds an observer of cursor inputs, after those already added.</summary>
    public void Observe(ICursorObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (gate)
        {
            observers = [.. observers, observer];
        }
    }

    /// <summary>Tells every observer that a manager handled <paramref name="input"/>, unless it equals that manager's
    /// last input.</summary>
    public void Handled(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock (gate)
        {
            if (lastByManager.TryGetValue(input.Manager, out var last) && last == input)
            {
                return;
            }
            if (lastByManager.Count >= ManagersRemembered && !lastByManager.ContainsKey(input.Manager))
            {
                lastByManager.Clear();
            }
            lastByManager[input.Manager] = input;
        }
        TellEach(Volatile.Read(ref observers), input);
    }

    [SuppressMessage("Design", "CA1031", Justification = "One observer's failure must not keep the input from the others, or reach the game.")]
    private static void TellEach(ICursorObserver[] observers, CursorInput input)
    {
        foreach (var observer in observers)
        {
            try
            {
                observer.Observe(input);
            }
            catch (Exception)
            {
            }
        }
    }
}
