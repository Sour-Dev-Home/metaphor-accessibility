using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the Followers list (#199): each row's follower and the archetype beside it, and its position
/// ("Name, Archetype, 2 of 14"), the names the list's view fetched by serial for its place in the view
/// (<see cref="FetchedRows"/>). Off, or a row whose name has no known text, says the position alone, as before. Not
/// spoken, <c>[NEEDS VERIFICATION]</c>: the follower's rank, which the list fetches no message for. Safe to call from
/// several threads.</summary>
/// <remarks>On build 548ddc95 (Steam build 18330018): the main menu's Follower row opens <c>CooperationMenuChoice</c>,
/// which fetched, before its first cursor, 14 pairs of a <see cref="MessageSerials.CharacterNames"/> serial with flags
/// 8 and a <see cref="MessageSerials.ArchetypeNames"/> serial with flags 6, for its 14 rows
/// (docs/findings/follower-and-archetype-tree-screen-classes-static.md). That the rows are in fetch order, top first,
/// as the other lists, is <c>[NEEDS VERIFICATION]</c> by ear: the logged visit never moved the cursor.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.FollowerList)]
public sealed class FollowerFocus : IScreenObserver, ISerialResolveObserver, ICursorObserver
{
    /// <summary>The flags the list fetches a follower's name with.</summary>
    public const int NameFlags = 8;

    /// <summary>The flags the list fetches a follower's archetype with, after the name.</summary>
    public const int ArchetypeFlags = 6;

    /// <summary>The Followers list, by its owner's class on build 548ddc95.</summary>
    public static Screen List { get; } = new("Followers", "CooperationMenuChoice@ope@ui@app");

    private readonly MessageSerials serials;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Func<bool> on;
    private readonly FetchedRows rows = new(List.ScreenClass);
    private readonly Lock gate = new();
    private readonly Dictionary<int, int> archetypeOf = [];
    private int? lastName;

    /// <param name="serials">The game's messages by serial.</param>
    /// <param name="announcer">Where the lines are said: the menus' speech.</param>
    /// <param name="cleaner">Cleans a message's text for speech.</param>
    /// <param name="on">Whether names are said, read at each line.</param>
    public FollowerFocus(MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner, Func<bool> on)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(on);
        this.serials = serials;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.on = on;
    }

    /// <summary>Speaks a row on entering the list or moving its focus.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != List || change.Change == ScreenChange.Left || change.Index < 0)
        {
            return;
        }
        var line = change.Positioned();
        if (on() && rows.SerialAt(change.Index) is { } serial && serials.TextOf(serial, cleaner) is { } name)
        {
            int? archetype;
            lock (gate)
            {
                archetype = archetypeOf.TryGetValue(serial, out var found) ? found : null;
            }
            var kind = archetype is { } known ? serials.TextOf(known, cleaner) : null;
            line = change.Positioned(name, kind);
        }
        announcer.Announce(new Announcement(line, Interrupt: true));
    }

    /// <summary>Takes each follower name the list fetches, and the archetype fetched right after it.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (resolve.Flags == NameFlags && serials.InFile(resolve.Serial, MessageSerials.CharacterNames))
        {
            rows.Fetched(resolve.Serial);
            lock (gate)
            {
                lastName = resolve.Serial;
            }
        }
        else if (resolve.Flags == ArchetypeFlags && serials.InFile(resolve.Serial, MessageSerials.ArchetypeNames))
        {
            lock (gate)
            {
                if (lastName is { } name)
                {
                    archetypeOf[name] = resolve.Serial;
                }
                lastName = null;
            }
        }
    }

    /// <summary>Takes the list's view from each settled cursor input, before the menu screens tell it.</summary>
    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        rows.Cursor(input);
        lock (gate)
        {
            lastName = null;
        }
    }
}
