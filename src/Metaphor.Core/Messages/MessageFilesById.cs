namespace Metaphor.Core.Messages;

/// <summary>The messages of each file the game compiled, by the id it compiled it under (#545), in file order, so a
/// <see cref="MessageReference"/> finds its message. A file compiled again under an id replaces what that id held: the
/// game reuses ids and compiles a file again under a new one. Files compiled without an id (negative) are not kept.
/// Safe to use from several threads.</summary>
public sealed class MessageFilesById : IMessageByReference, IMessageInFile, IMessageFileObserver
{
    private readonly Dictionary<int, GameMessage[]> files = [];
    private readonly Dictionary<string, GameMessage[]> byName = new(StringComparer.OrdinalIgnoreCase);
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
            byName[Path.GetFileName(file.Path)] = messages;
        }
    }

    /// <summary>The message at <paramref name="position"/> in the latest file compiled with an id whose file name is
    /// <paramref name="fileName"/> (any case), or null.</summary>
    public GameMessage? FindInFile(string fileName, int position)
    {
        lock (gate)
        {
            return byName.TryGetValue(fileName, out var messages) && position >= 0 && position < messages.Length
                ? messages[position]
                : null;
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
