namespace Metaphor.Core.Menus;

/// <summary>A feature that hears the cutscene skip menu open (#809).</summary>
public interface IMoviePauseObserver
{
    /// <summary>The skip menu was asked to show.</summary>
    void Opened();
}

/// <summary>Hands the cutscene skip menu's requests to the features that hear it (#809). The game sends the menu's
/// component (<c>UUIC_Common_MoviePause</c>) a request holding one byte; on 1 its handler shows the layout with no
/// animation, and on anything else, or none, it closes a shown layout with animation 4768, which the by-ear session of
/// 2026-11-23 heard as the close (docs/findings/movie-pause-open-static.md). So a request of <see cref="Show"/> is the
/// open. Observers join on the start-up thread before any hook goes in; after that it is only read, from any game
/// thread.</summary>
public sealed class MoviePauseRequests
{
    /// <summary>The request's value that shows the menu.</summary>
    public const int Show = 1;

    private readonly List<IMoviePauseObserver> observers = [];

    /// <summary>Whether any feature hears the menu: none, and the hook reads nothing in a Release build.</summary>
    public bool Any => observers.Count > 0;

    /// <summary>Adds an observer of the menu's open.</summary>
    public void Observe(IMoviePauseObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        observers.Add(observer);
    }

    /// <summary>A request with <paramref name="value"/>, null when it held none or was unreadable: on
    /// <see cref="Show"/> each observer hears the menu open.</summary>
    public void Requested(int? value)
    {
        if (value != Show)
        {
            return;
        }
        foreach (var observer in observers)
        {
            observer.Opened();
        }
    }
}
