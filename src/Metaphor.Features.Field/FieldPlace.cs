using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Field;

/// <summary>The place name "where am I" says (#181): the names the game looked up in <c>MapName.msg</c> since the map
/// changed. On game build 548ddc95… (Steam build 18330018) a <c>GetMessageByLabelIndex</c> call with message id 3 is a
/// <c>MapName.msg</c> lookup (entry 3 of the message-file table) whose label index is the message's position in the
/// file, and the game makes them when the arrival banner or the map strip shows
/// (docs/findings/field-nearby-unit-names-static.md). [NEEDS VERIFICATION]: that the first lookups after the map id
/// changes name the place arrived at, region before area, and not a place the travel map browses. So it keeps the first
/// <see cref="MaxNames"/> distinct ones after each change; with no map id read, the last ones. Safe to call from several
/// threads.</summary>
public sealed class FieldPlace : IMessageFileObserver
{
    /// <summary>The message-file table's entry for <c>MapName.msg</c>, as a lookup's message id names it.</summary>
    public const int MapNameEntry = 3;

    /// <summary>The most names "where am I" joins: a region and an area.</summary>
    public const int MaxNames = 2;

    private const string MapNameFile = "message/MapName.msg";

    private readonly TextCleaner cleaner;
    private readonly Lock gate = new();
    private readonly List<int> labels = [];
    private GameMessage[] names = [];
    private int? map;

    public FieldPlace(TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(cleaner);
        this.cleaner = cleaner;
    }

    /// <summary>Keeps <c>MapName.msg</c>'s messages in file order when the game compiles it.</summary>
    public void Observe(MessageFileCompiled file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Source is null || file.Path is null ||
            !string.Equals(MessageSerials.FromMessageFolder(file.Path), MapNameFile, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        GameMessage[] messages = [.. MessageLibrary.Parse(file.Source)];
        lock (gate)
        {
            names = messages;
        }
    }

    /// <summary>The game looked up label <paramref name="label"/> of message <paramref name="entry"/> while the map's id
    /// read <paramref name="mapId"/> (null when unread).</summary>
    public void LookedUp(int entry, int label, int? mapId)
    {
        if (entry != MapNameEntry || label < 0)
        {
            return;
        }
        lock (gate)
        {
            if (mapId is null)
            {
                labels.Remove(label);
                labels.Add(label);
                if (labels.Count > MaxNames)
                {
                    labels.RemoveAt(0);
                }
                return;
            }
            if (mapId != map)
            {
                map = mapId;
                labels.Clear();
            }
            if (labels.Count < MaxNames && !labels.Contains(label))
            {
                labels.Add(label);
            }
        }
    }

    /// <summary>The place's name, such as "Region, Area", or null when none is known for <paramref name="mapId"/> (null
    /// when unread, which takes the names kept whatever the map).</summary>
    public string? Name(int? mapId)
    {
        lock (gate)
        {
            if (mapId is not null && map is not null && mapId != map)
            {
                return null;
            }
            var said = labels
                .Where(label => label < names.Length)
                .Select(label => cleaner.Clean(names[label].Text))
                .Where(text => text is { Length: > 0 })
                .ToList();
            return said.Count == 0 ? null : string.Join(", ", said);
        }
    }
}
