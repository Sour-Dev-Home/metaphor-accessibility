namespace Metaphor.Core.Messages;

/// <summary>One message from a game message file: its label, the speaker's name as the file stores it (null when
/// none) and the rest of its text, tags kept.</summary>
public sealed record GameMessage(string Label, string? Speaker, string Text);

/// <summary>Finds a game message by its label.</summary>
public interface IMessageLookup
{
    /// <summary>The message with this label, or null when there is none.</summary>
    GameMessage? Find(string label);
}

/// <summary>The messages of every message file the game has compiled, by label (#56). The game shows a line by its
/// label alone (docs/raw-sources/msg-request-run-2026-11-16.md), and its text is in a message file compiled earlier.
/// Safe to use from several threads.</summary>
public sealed class MessageLibrary : IMessageLookup
{
    private const string SpeakerStart = "<NS>";
    private const string SpeakerEnd = "<NE>";

    private readonly Dictionary<string, GameMessage> byLabel = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <summary>Adds the messages of a message file's source, as the game compiles it. A label seen before takes its
    /// newer message.</summary>
    public void Compiled(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var messages = Parse(source);
        lock (gate)
        {
            foreach (var message in messages)
            {
                byLabel[message.Label] = message;
            }
        }
    }

    /// <summary>The message with this label, or null when no compiled file has one.</summary>
    public GameMessage? Find(string label)
    {
        lock (gate)
        {
            return byLabel.GetValueOrDefault(label);
        }
    }

    // The shape logged on build 548ddc95 (docs/raw-sources/message-listening-run-2026-11-16.md): "@label", then the
    // body between lines "{" and "}", the speaker in <NS>…<NE>. Lines outside a body, such as #SPEAKER_ID, are skipped.
    private static List<GameMessage> Parse(string source)
    {
        var messages = new List<GameMessage>();
        string? label = null;
        List<string>? body = null;
        foreach (var raw in source.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (body is null)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('@'))
                {
                    label = trimmed[1..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                }
                else if (trimmed == "{" && label is not null)
                {
                    body = [];
                }
            }
            else if (line.Trim() == "}")
            {
                messages.Add(ToMessage(label!, string.Join('\n', body)));
                label = null;
                body = null;
            }
            else
            {
                body.Add(line);
            }
        }
        return messages;
    }

    private static GameMessage ToMessage(string label, string body)
    {
        string? speaker = null;
        var start = body.IndexOf(SpeakerStart, StringComparison.Ordinal);
        var end = start < 0 ? -1 : body.IndexOf(SpeakerEnd, start, StringComparison.Ordinal);
        if (end >= 0)
        {
            speaker = body[(start + SpeakerStart.Length)..end].Trim();
            body = body[..start] + body[(end + SpeakerEnd.Length)..];
        }
        return new GameMessage(label, string.IsNullOrEmpty(speaker) ? null : speaker, body.Trim());
    }
}
