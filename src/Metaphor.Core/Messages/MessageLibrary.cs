using System.Buffers.Binary;
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
}

/// <summary>Finds a game message by its key: the MD5 of its label, as the game's name tables store it
/// (docs/raw-sources/name-lookup-candidates-2026-11-16.md, docs/findings/speaker-alias-from-chara-id-table.md).</summary>
public interface IMessageKeyLookup
{
    /// <summary>The message whose label's MD5 is <paramref name="key"/>, or null when there is none.</summary>
    GameMessage? FindByKey(ReadOnlySpan<byte> key);
}

/// <summary>The messages of every message file the game has compiled, by label (#56). The game shows a line by its
/// label alone (docs/raw-sources/msg-request-run-2026-11-16.md), and its text is in a message file compiled earlier.
/// Safe to use from several threads.</summary>
public sealed class MessageLibrary : IMessageLookup, IMessageKeyLookup, IMessageFileObserver
{
    private const string SpeakerStart = "<NS>";
    private const string SpeakerEnd = "<NE>";
    private const string SpeakerIdLine = "#SPEAKER_ID";

    private readonly Dictionary<string, GameMessage> byLabel = new(StringComparer.Ordinal);
    private readonly Dictionary<UInt128, string> labelByKey = [];
    private readonly Lock gate = new();

    /// <summary>Adds the messages of a message file's source, as the game compiles it. A label seen before takes its
    /// newer message.</summary>
    public void Compiled(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var messages = Parse(source);
        var keys = messages.Select(message => Key(message.Label)).ToList();
        lock (gate)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                byLabel[messages[i].Label] = messages[i];
                labelByKey[keys[i]] = messages[i].Label;
            }
        }
    }

    /// <summary>The message whose label's MD5 is <paramref name="key"/>, or null when no compiled file has one or the
    /// key isn't 16 bytes.</summary>
    public GameMessage? FindByKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != 16)
        {
            return null;
        }
        var wanted = AsKey(key);
        lock (gate)
        {
            return labelByKey.TryGetValue(wanted, out var label) ? byLabel.GetValueOrDefault(label) : null;
        }
    }

    // The game finds a message by the MD5 of its label's bytes (docs/raw-sources/name-lookup-candidates-2026-11-16.md);
    // labels are ASCII, so UTF-8 gives the same bytes. MD5 is the game's key here, not a security measure.
#pragma warning disable CA5351
    private static UInt128 Key(string label) => AsKey(MD5.HashData(Encoding.UTF8.GetBytes(label)));
#pragma warning restore CA5351

    private static UInt128 AsKey(ReadOnlySpan<byte> key) =>
        new(BinaryPrimitives.ReadUInt64BigEndian(key), BinaryPrimitives.ReadUInt64BigEndian(key[8..]));

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

    // The shape logged on build 548ddc95 (docs/raw-sources/message-listening-run-2026-11-16.md): "@label", then the
    // body between lines "{" and "}", the speaker in <NS>…<NE>. A line #SPEAKER_ID "51" before the label gives the
    // speaker's number; other lines outside a body are skipped.
    private static List<GameMessage> Parse(string source)
    {
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
