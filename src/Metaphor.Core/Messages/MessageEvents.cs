using System.Diagnostics.CodeAnalysis;

namespace Metaphor.Core.Messages;

/// <summary>A message file the game compiled: its path, the number the game gave it, and its source text.</summary>
public sealed record MessageFileCompiled(string Path, int Id, string Source);

/// <summary>A line the game's scripts asked for by label, and the kind of request it came with.</summary>
public sealed record LineRequested(int Kind, string Label);

/// <summary>A choice the game's scripts opened (#82): the request's kind, the label of the prompt asked before it and
/// the label of the message holding its options, each null when it couldn't be read.</summary>
public sealed record ChoiceRequested(int Kind, string? PromptLabel, string? ChoiceLabel);

/// <summary>Told of each message file the game compiles.</summary>
public interface IMessageFileObserver
{
    void Observe(MessageFileCompiled file);
}

/// <summary>Told of each line the game asks for by label.</summary>
public interface ILineRequestObserver
{
    void Observe(LineRequested line);
}

/// <summary>Told of each choice the game opens.</summary>
public interface IChoiceRequestObserver
{
    void Observe(ChoiceRequested choice);
}

/// <summary>Hands each message event a hook decoded to its observers (#86), so one hook per game function serves
/// them all. Observers hear each event in the order they were added, and one that throws never stops the rest. Add
/// observers before the hooks go live; telling them is safe from any game thread.</summary>
public sealed class MessageEvents
{
    private readonly Lock gate = new();
    private IMessageFileObserver[] fileObservers = [];
    private ILineRequestObserver[] lineObservers = [];
    private IChoiceRequestObserver[] choiceObservers = [];

    /// <summary>Adds an observer of compiled message files, after those already added.</summary>
    public void ObserveFiles(IMessageFileObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (gate)
        {
            fileObservers = [.. fileObservers, observer];
        }
    }

    /// <summary>Adds an observer of requested lines, after those already added.</summary>
    public void ObserveLines(ILineRequestObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (gate)
        {
            lineObservers = [.. lineObservers, observer];
        }
    }

    /// <summary>Adds an observer of opened choices, after those already added.</summary>
    public void ObserveChoices(IChoiceRequestObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (gate)
        {
            choiceObservers = [.. choiceObservers, observer];
        }
    }

    /// <summary>Tells every file observer that the game compiled <paramref name="file"/>.</summary>
    public void Compiled(MessageFileCompiled file) => TellEach(Volatile.Read(ref fileObservers), observer => observer.Observe(file));

    /// <summary>Tells every line observer that the game asked for <paramref name="line"/>.</summary>
    public void Requested(LineRequested line) => TellEach(Volatile.Read(ref lineObservers), observer => observer.Observe(line));

    /// <summary>Tells every choice observer that the game opened <paramref name="choice"/>.</summary>
    public void ChoiceOpened(ChoiceRequested choice) =>
        TellEach(Volatile.Read(ref choiceObservers), observer => observer.Observe(choice));

    [SuppressMessage("Design", "CA1031", Justification = "One observer's failure must not keep the event from the others, or reach the game.")]
    private static void TellEach<T>(T[] observers, Action<T> tell)
    {
        foreach (var observer in observers)
        {
            try
            {
                tell(observer);
            }
            catch (Exception)
            {
            }
        }
    }
}
