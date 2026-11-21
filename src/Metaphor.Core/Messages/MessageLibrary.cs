using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Metaphor.Core.Messages;

/// <summary>One message from a game message file: its label, the speaker's name as the file stores it (null when
/// none) and the rest of its text, tags kept. <paramref name="SpeakerId"/> is the quoted value of the
/// <c>#SPEAKER_ID</c> line before the message, such as <c>51</c>, or null when there was none.</summary>
public sealed record GameMessage(string Label, string? Speaker, string Text, string? SpeakerId = null);

/// <summary>Finds a game message by its label.</summary>
public interface IMessageLookup
{
    /// <summary>The message with this label, or null when there is none.</summary>
    GameMessage? Find(string label);

    /// <summary>The message whose label's MD5 is <paramref name="labelHash"/> (32 hex digits, either case), or null when
    /// there is none.</summary>
    GameMessage? FindByLabelHash(string labelHash);
}

/// <summary>The messages of every message file the game has compiled, by label (#56). The game shows a line by its
/// label alone (docs/raw-sources/msg-request-run-2026-11-16.md), and its text is in a message file compiled earlier.
/// Safe to use from several threads.</summary>
public sealed class MessageLibrary : IMessageLookup, IMessageFileObserver
{
    private const string SpeakerStart = "<NS>";
    private const string SpeakerEnd = "<NE>";
    private const string SpeakerIdLine = "#SPEAKER_ID";

    private readonly Dictionary<string, GameMessage> byLabel = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameMessage> byLabelHash = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock gate = new();

    /// <summary>Adds the messages of a message file's source, as the game compiles it. A label seen before takes its
    /// newer message.</summary>
    public void Compiled(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var messages = Parse(source);
        var hashes = messages.ConvertAll(message => LabelHash(message.Label));
        lock (gate)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                byLabel[messages[i].Label] = messages[i];
                byLabelHash[hashes[i]] = messages[i];
            }
        }
    }

    /// <summary>The MD5 of a label's bytes as 32 upper-case hex digits: the key the game's message window finds a line
    /// by, on game build 548ddc95… (Steam build 18330018; docs/raw-sources/msg-window-show-line-2026-11-20.md), and the
    /// key its name tables store (docs/raw-sources/name-lookup-candidates-2026-11-16.md).</summary>
    [SuppressMessage("Security", "CA5351", Justification = "Matches the game's own label hash; nothing is protected by it.")]
    public static string LabelHash(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(label)));
    }

    /// <summary>Adds the messages of a file the game compiled (#86).</summary>
    public void Observe(MessageFileCompiled file)
    {
        ArgumentNullException.ThrowIfNull(file);
        Compiled(file.Source);
    }

    /// <summary>The message with this label, or null when no compiled file has one.</summary>
    public GameMessage? Find(string label)
    {
        lock (gate)
        {
            return byLabel.GetValueOrDefault(label);
        }
    }

    /// <summary>The message whose label's MD5 is <paramref name="labelHash"/>, or null when no compiled file has
    /// one.</summary>
    public GameMessage? FindByLabelHash(string labelHash)
    {
        ArgumentNullException.ThrowIfNull(labelHash);
        lock (gate)
        {
            return byLabelHash.GetValueOrDefault(labelHash);
        }
    }

    // The shape logged on build 548ddc95 (docs/raw-sources/message-listening-run-2026-11-16.md): "@label", then the
    // body between lines "{" and "}", the speaker in <NS>…<NE>. A line #SPEAKER_ID "51" before the label gives the
    // speaker's number; other lines outside a body are skipped. In file order, which MessageSerials and the field's place
    // names (#181) rely on.
    /// <summary>A compiled file's messages in file order.</summary>
    public static List<GameMessage> Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var messages = new List<GameMessage>();
        string? label = null;
        string? speakerId = null;
        string? labelSpeakerId = null;
        List<string>? body = null;
        foreach (var raw in source.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (body is null)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(SpeakerIdLine, StringComparison.Ordinal))
                {
                    speakerId = trimmed[SpeakerIdLine.Length..].Trim().Trim('"');
                }
                else if (trimmed.StartsWith('@'))
                {
                    label = trimmed[1..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    labelSpeakerId = string.IsNullOrEmpty(speakerId) ? null : speakerId;
                    speakerId = null;
                }
                else if (trimmed == "{" && label is not null)
                {
                    body = [];
                }
            }
            else if (line.Trim() == "}")
            {
                messages.Add(ToMessage(label!, string.Join('\n', body)) with { SpeakerId = labelSpeakerId });
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
