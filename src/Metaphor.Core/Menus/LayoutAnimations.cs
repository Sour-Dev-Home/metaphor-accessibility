namespace Metaphor.Core.Menus;

/// <summary>One animation a UI layout started (#809): the layout's class as its run-time type information names it,
/// such as <c>MoviePauseLayout@ui@app</c>, and the animation's number.</summary>
public sealed record LayoutAnimation(string Layout, int Animation);

/// <summary>A feature that hears the animations some layout classes start (#809).</summary>
public interface ILayoutAnimationObserver
{
    /// <summary>The layout classes it hears; it is told of no other.</summary>
    IReadOnlyCollection<string> Layouts { get; }

    /// <summary>An animation one of <see cref="Layouts"/> started.</summary>
    void Played(LayoutAnimation played);
}

/// <summary>Hands the layout animation hook's calls to the features that asked for their layout's class (#809). The hook
/// runs on every animation any layout starts, so it asks <see cref="Wants"/> first and reads nothing more for a class
/// no feature asked for. Observers join on the start-up thread before any hook goes in; after that it is only read,
/// from any game thread.</summary>
public sealed class LayoutAnimations
{
    private readonly List<ILayoutAnimationObserver> observers = [];
    private readonly HashSet<string> wanted = new(StringComparer.Ordinal);

    /// <summary>Whether any feature asked for a class: none, and the hook reads nothing in a Release build.</summary>
    public bool Any => wanted.Count > 0;

    /// <summary>Adds an observer of its <see cref="ILayoutAnimationObserver.Layouts"/>.</summary>
    public void Observe(ILayoutAnimationObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        observers.Add(observer);
        wanted.UnionWith(observer.Layouts);
    }

    /// <summary>Whether a feature asked for <paramref name="layout"/>; false for an unreadable class (null).</summary>
    public bool Wants(string? layout) => layout is not null && wanted.Contains(layout);

    /// <summary>Tells each observer that asked for the animation's layout class.</summary>
    public void Played(LayoutAnimation played)
    {
        ArgumentNullException.ThrowIfNull(played);
        foreach (var observer in observers)
        {
            if (observer.Layouts.Contains(played.Layout))
            {
                observer.Played(played);
            }
        }
    }
}
