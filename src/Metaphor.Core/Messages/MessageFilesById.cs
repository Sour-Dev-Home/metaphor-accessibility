namespace Metaphor.Core.Messages;

/// <summary>The messages of each file the game compiled, by the id it compiled it under (#545), in file order, so a
/// <see cref="MessageReference"/> finds its message. A file compiled again under an id replaces what that id held: the
/// game reuses ids and compiles a file again under a new one. Files compiled without an id (negative) are not kept.
/// Safe to use from several threads.</summary>
public sealed class MessageFilesById : IMessageByReference, IMessageFileObserver
{
    private readonly Dictionary<int, GameMessage[]> files = [];
    private readonly Lock gate = new();

    /// <summary>Keeps a compiled file's messages under its id.</summary>
    public void Observe(MessageFileCompiled file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Id < 0)
        {
            return;
        }
        GameMessage[] messages = [.. MessageLibrary.Parse(file.Source)];
        lock (gate)
        {
            files[file.Id] = messages;
        }
    }

    /// <summary>The message at the reference's position in the file last compiled under its id, or null.</summary>
    public GameMessage? Find(MessageReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        lock (gate)
        {
            return files.TryGetValue(reference.File, out var messages) && reference.Position < messages.Length
                ? messages[reference.Position]
                : null;
        }
    }
}
