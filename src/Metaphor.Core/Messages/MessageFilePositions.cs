namespace Metaphor.Core.Messages;

/// <summary>A message file as one game build compiles it: its path from the <c>message</c> folder on and how many
/// messages it holds.</summary>
public sealed record MessageFileShape(string File, int Count);

/// <summary>Messages by their position in the file the game compiled (#108), for tags whose number is a position, such
/// as <c>&lt;SKILL 1 2&gt;</c>. Only the files named are kept, and only when they hold the named number of messages:
/// another count means another build's file, whose positions can't be trusted. Safe to use from several threads.
/// </summary>
public sealed class MessageFilePositions : IMessageFileObserver
{
    private readonly MessageFileShape[] shapes;
    private readonly GameMessage[]?[] files;
    private readonly Lock gate = new();

    public MessageFilePositions(IEnumerable<MessageFileShape> shapes)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        this.shapes = [.. shapes];
        files = new GameMessage[]?[this.shapes.Length];
    }

    /// <summary>Keeps a named file's messages in order; one compiled again replaces the one before, and one with another
    /// count drops it.</summary>
    public void Observe(MessageFileCompiled file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var name = MessageSerials.FromMessageFolder(file.Path);
        var index = Array.FindIndex(shapes, shape => string.Equals(shape.File, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }
        var messages = MessageLibrary.Parse(file.Source);
        lock (gate)
        {
            files[index] = messages.Count == shapes[index].Count ? [.. messages] : null;
        }
    }

    /// <summary>The message at <paramref name="position"/> of <paramref name="file"/> (its path from <c>message/</c>
    /// on), or null when that file isn't kept or has no such position.</summary>
    public GameMessage? Find(string file, int position)
    {
        ArgumentNullException.ThrowIfNull(file);
        var index = Array.FindIndex(shapes, shape => string.Equals(shape.File, file, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return null;
        }
        lock (gate)
        {
            return files[index] is { } messages && position >= 0 && position < messages.Length
                ? messages[position]
                : null;
        }
    }
}
