using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>Speaks the Party screen's reserve list (#922), the allies a picked battle member can swap with: "Reserve,
/// Strohl, 1 of 3" on entering, then each row as the focus moves, each cutting off the last. A row's name is the
/// <c>CharaName</c> message the screen fetches by serial (flags 9) just after the cursor lands on it, said again with
/// the row when it comes; a row named once keeps its name until the list opens again. Safe to call from several
/// threads.</summary>
/// <remarks>The maintainer's 2026-11-24 log (Debug, Steam 18330018): each landing on <c>BTL_OrderPartyReserve</c> was
/// followed 72 to 76 ms later by one <c>CharaName</c> fetch for that row, and a move also fetched the row it left in
/// the move's own millisecond, before the landing, as on the Party screen (<see cref="PartyScreenFocus"/>). A revisit
/// sometimes fetched nothing. The reserve members' HP isn't read: they have no battle unit.</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattlePartyScreen)]
public sealed class ReserveScreenFocus : IScreenObserver, ISerialResolveObserver
{
    /// <summary>The flags the screen fetches a member's name with.</summary>
    public const int NameFlags = PartyScreenFocus.NameFlags;

    /// <summary>How long after a landing its name may come.</summary>
    public const long NameMilliseconds = PartyScreenFocus.NameMilliseconds;

    private readonly MessageSerials serials;
    private readonly TextCleaner cleaner;
    private readonly IAnnouncer announcer;
    private readonly Func<long> millisecondsNow;
    private readonly Lock gate = new();
    // Each row's name serial, this visit.
    private readonly Dictionary<int, int> names = [];
    private ScreenEvent? focus;
    private long focusAt;
    private bool focusNamed;

    public ReserveScreenFocus(MessageSerials serials, TextCleaner cleaner, IAnnouncer announcer, Func<long> millisecondsNow)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(millisecondsNow);
        this.serials = serials;
        this.cleaner = cleaner;
        this.announcer = announcer;
        this.millisecondsNow = millisecondsNow;
    }

    /// <summary>Speaks the focus when the list is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        int? serial;
        lock (gate)
        {
            if (change.Screen != BattleScreens.Reserve || change.Change == ScreenChange.Left)
            {
                focus = null;
                return;
            }
            if (change.Change == ScreenChange.Entered)
            {
                names.Clear();
            }
            focus = change;
            focusAt = millisecondsNow();
            focusNamed = false;
            serial = names.TryGetValue(change.Index, out var known) ? known : null;
        }
        announcer.Announce(Line(change, serial));
    }

    /// <summary>Names the row just landed on with the first name fetched after the landing.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (resolve.Flags != NameFlags || !serials.InFile(resolve.Serial, MessageSerials.CharacterNames))
        {
            return;
        }
        ScreenEvent? again = null;
        lock (gate)
        {
            if (focus is { } current && !focusNamed && millisecondsNow() - focusAt <= NameMilliseconds)
            {
                focusNamed = true;
                if (!names.TryGetValue(current.Index, out var kept) || kept != resolve.Serial)
                {
                    names[current.Index] = resolve.Serial;
                    again = current;
                }
            }
        }
        if (again is not null)
        {
            announcer.Announce(Line(again, resolve.Serial));
        }
    }

    private Announcement Line(ScreenEvent change, int? serial)
    {
        var row = change.Positioned(serial is { } known ? serials.TextOf(known, cleaner) : null);
        return new Announcement(
            change.Change == ScreenChange.Entered ? $"{BattleScreens.Reserve.Name}, {row}" : row, Interrupt: true);
    }
}
