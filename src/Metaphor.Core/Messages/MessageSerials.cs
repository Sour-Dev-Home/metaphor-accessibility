using Metaphor.Core.Announcements;

namespace Metaphor.Core.Messages;

/// <summary>A message file's serial numbers on one game build: its path from the <c>message</c> folder on, its first
/// serial and how many messages it holds; and, when known, its <paramref name="Entry"/> in the game's message-file
/// table, the number a message reference names its file by (<see cref="MessageReference"/>).</summary>
public sealed record SerialRange(string File, int First, int Count, int? Entry = null);

/// <summary>Finds a game message by the serial number the game fetches it by.</summary>
public interface IMessageBySerial
{
    /// <summary>The message with this serial, or null when no known file holds it.</summary>
    GameMessage? FindBySerial(int serial);
}

/// <summary>The messages the game fetches by serial number (#584), from the message files it compiles. A file's
/// serials are its first serial plus each message's position in the file, and the game compiles its system files whole at
/// start-up, so their messages are known before any menu asks for one (docs/findings/message-serials-from-file-positions.md).
/// Only the files in its table are kept, and only when they hold the table's number of messages: another count means
/// another build's file, whose positions can't be trusted. Safe to use from several threads.</summary>
public sealed class MessageSerials : IMessageBySerial, IMessageByReference, IMessageFileObserver
{
    private const string MessageFolder = "message/";

    private static readonly TextCleaner Plain = new(TextCleanerRules.None);

    /// <summary>The files whose serials are known on game build 548ddc95… (Steam build 18330018): each first serial and
    /// count is the range test the game's message-file table holds for it, read from the exe, and matches the file as
    /// the game compiled it in play (docs/findings/message-serials-from-file-positions.md).</summary>
    public static IReadOnlyList<SerialRange> Steam18330018 { get; } =
    [
        new("message/system/System_Message.msg", 1142, 790),
        new("message/system/System_Message_PC.msg", 11453, 354),
    ];

    /// <summary>The place-name files on game build 548ddc95… (Steam build 18330018), for the travel map (#545): each
    /// table entry, first serial and count is read from the exe's message-file table
    /// (docs/findings/travel-map-destination-text-static.md).</summary>
    public static IReadOnlyList<SerialRange> Steam18330018PlaceNames { get; } =
    [
        new("message/AccessPointName.msg", 10873, 408, Entry: 34),
        new("message/system/Big_Mapname.msg", 10102, 23, Entry: 40),
        new("message/system/WorldMap_Message.msg", 10860, 13, Entry: 66),
    ];

    private readonly SerialRange[] ranges;
    private readonly GameMessage[]?[] files;
    private readonly Lock gate = new();

    public MessageSerials(IEnumerable<SerialRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        this.ranges = [.. ranges];
        files = new GameMessage[]?[this.ranges.Length];
    }

    /// <summary>Keeps a compiled file's messages in order when its table entry names it; a file compiled again replaces
    /// the one before, and one with another count drops it.</summary>
    public void Observe(MessageFileCompiled file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var name = FromMessageFolder(file.Path);
        var index = Array.FindIndex(ranges, range => string.Equals(range.File, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }
        var messages = MessageLibrary.Parse(file.Source);
        lock (gate)
        {
            files[index] = messages.Count == ranges[index].Count ? [.. messages] : null;
        }
    }

    /// <summary>The message with this serial, or null when no file kept holds it.</summary>
    public GameMessage? FindBySerial(int serial)
    {
        lock (gate)
        {
            for (var i = 0; i < ranges.Length; i++)
            {
                var position = serial - ranges[i].First;
                if (files[i] is { } messages && position >= 0 && position < messages.Length)
                {
                    return messages[position];
                }
            }
        }
        return null;
    }

    /// <summary>The message a reference names (#545): the one at its position in the file with its table entry, or null
    /// when no file kept has that entry or the position is outside it.</summary>
    public GameMessage? Find(MessageReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var range = Array.Find(ranges, range => range.Entry == reference.File);
        return range is not null && reference.Position >= 0 && reference.Position < range.Count
            ? FindBySerial(range.First + reference.Position)
            : null;
    }

    /// <summary>The message's text as <paramref name="cleaner"/> says it, or null when it is unknown or empty.</summary>
    public string? TextOf(int serial, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(cleaner);
        return FindBySerial(serial) is { } message && cleaner.Clean(message.Text) is { Length: > 0 } text ? text : null;
    }

    /// <summary>For the dev log (#584): the message's label and its text with its tags dropped, or that it is unknown.
    /// </summary>
    public string Describe(int serial) =>
        FindBySerial(serial) is { } message
            ? $"label={message.Label} text=\"{LogText.Escaped(Plain.Clean(message.Text))}\""
            : "label=(unknown)";

    // "EN/message/system/X.msg" from "message/" on: the language folder before it differs by the game's language.
    private static string FromMessageFolder(string path)
    {
        var slashed = path.Replace('\\', '/');
        if (slashed.StartsWith(MessageFolder, StringComparison.OrdinalIgnoreCase))
        {
            return slashed;
        }
        var at = slashed.IndexOf("/" + MessageFolder, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? slashed : slashed[(at + 1)..];
    }
}
