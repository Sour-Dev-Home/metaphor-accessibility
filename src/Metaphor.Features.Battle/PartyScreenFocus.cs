using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>Speaks the battle Order menu's Party screen (#258): on entering "Party, Junah, 120 HP, 40 MP, 2 of 4", then
/// on each focus move the row alone, each cutting off the last. A row is the party slot at its index, its HP and MP read
/// from the unit record (<see cref="IBattleParty"/>); its name is the <c>CharaName</c> message the screen fetched by
/// serial (flags 9) as the cursor first landed on it, kept by the slot's character, so a later visit or a swap keeps
/// it. The protagonist, for whom no name is fetched, is said by his typed name. Safe to call from several threads.
/// </summary>
/// <remarks>From the static note on build 548ddc95 (docs/findings/battle-party-swap-and-formation-static.md) and the
/// maintainer's 2026-11-23 evening log (Debug, Steam 18330018): as the screen opened at index 1 the game fetched
/// <c>CharaName_PC05_Junus</c>, and on the first landing on index 2 <c>CharaName_PC08_Basilio</c>. Not yet checked:
/// that the screen's index is the party slot, that a name is fetched within <see cref="NameMilliseconds"/> of its
/// landing (before or after), and that character 1 is the protagonist (the 010 templates' <c>CharacterID</c>,
/// docs/raw-sources/metaphor-010-templates-tables-2026-11-17.md), all <c>[NEEDS VERIFICATION]</c>; a Debug build logs
/// each focus's read for the by-ear check.</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattlePartyScreen)]
public sealed class PartyScreenFocus : IScreenObserver, ISerialResolveObserver
{
    /// <summary>The flags the screen fetches a member's name with.</summary>
    public const int NameFlags = 9;

    /// <summary>The protagonist's character number, <c>[NEEDS VERIFICATION]</c> on this build.</summary>
    public const int Protagonist = 1;

    /// <summary>How far apart a name fetch and the focus it names may be: many frames, far less than a deliberate
    /// move (<see cref="CursorEvents.QuietMilliseconds"/>).</summary>
    public const long NameMilliseconds = CursorEvents.QuietMilliseconds;

    private readonly MessageSerials serials;
    private readonly TextCleaner cleaner;
    private readonly IBattleParty party;
    private readonly IAnnouncer announcer;
    private readonly Func<long> millisecondsNow;
    private readonly Lock gate = new();
    // Each character's name serial, once fetched.
    private readonly Dictionary<int, int> names = [];
    private ScreenEvent? focus;
    private int? focusUnit;
    private long focusAt;
    private bool focusNamed;
    private int? fetched;
    private long fetchedAt;

    public PartyScreenFocus(
        MessageSerials serials, TextCleaner cleaner, IBattleParty party, IAnnouncer announcer, Func<long> millisecondsNow)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(millisecondsNow);
        this.serials = serials;
        this.cleaner = cleaner;
        this.party = party;
        this.announcer = announcer;
        this.millisecondsNow = millisecondsNow;
    }

    /// <summary>The protagonist's typed name, or null when unknown. None, when not set.</summary>
    public Func<string?>? ProtagonistName { get; init; }

    /// <summary>A Debug build's log of each focus's read. None, when not set.</summary>
    public Action<string>? Log { get; init; }

    /// <summary>The name character <paramref name="unit"/> goes by on this screen, or null while none is known.</summary>
    public string? NameOf(int unit)
    {
        int? serial;
        lock (gate)
        {
            serial = names.TryGetValue(unit, out var known) ? known : null;
        }
        return serial is { } found
            ? serials.TextOf(found, cleaner)
            : unit == Protagonist ? ProtagonistName?.Invoke() : null;
    }

    /// <summary>Speaks the focus when the screen is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != BattleScreens.Party || change.Change == ScreenChange.Left)
        {
            lock (gate)
            {
                focus = null;
            }
            return;
        }
        var member = party.Members().FirstOrDefault(each => each.Slot == change.Index);
        var now = millisecondsNow();
        lock (gate)
        {
            focus = change;
            focusUnit = member?.Unit;
            focusAt = now;
            // A name fetched just before the cursor landed is this row's.
            if (fetched is { } serial && now - fetchedAt <= NameMilliseconds)
            {
                Name(serial);
            }
            fetched = null;
            focusNamed = member is not null && names.ContainsKey(member.Unit);
        }
        Log?.Invoke(member is null
            ? string.Create(CultureInfo.InvariantCulture, $"index={change.Index} count={change.Count} member=none")
            : string.Create(CultureInfo.InvariantCulture,
                $"index={change.Index} count={change.Count} unit={member.Unit} hp={member.Hp} mp={member.Mp} flags=0x{member.Flags ?? 0:x} named={focusNamed}"));
        announcer.Announce(Line(change, member));
    }

    /// <summary>Keeps each member name the game fetches, and says again a focus spoken before its name came.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (resolve.Flags != NameFlags || !serials.InFile(resolve.Serial, MessageSerials.CharacterNames))
        {
            return;
        }
        var now = millisecondsNow();
        ScreenEvent? again = null;
        lock (gate)
        {
            if (focus is { } current && now - focusAt <= NameMilliseconds)
            {
                // Just after a landing the name is that row's; one already named keeps its name.
                if (focusUnit is not null && !focusNamed)
                {
                    Name(resolve.Serial);
                    focusNamed = true;
                    again = current;
                }
            }
            else
            {
                // Before the cursor lands, as the screen opens: the next landing's.
                fetched = resolve.Serial;
                fetchedAt = now;
            }
        }
        if (again is not null)
        {
            var member = party.Members().FirstOrDefault(each => each.Slot == again.Index);
            announcer.Announce(Line(again, member));
        }
    }

    // Gives the focused row's character the name fetched, once: a row already named keeps its name. Called under the
    // gate.
    private void Name(int serial)
    {
        fetched = null;
        if (focusUnit is { } unit)
        {
            names.TryAdd(unit, serial);
        }
    }

    private Announcement Line(ScreenEvent change, PartyMember? member)
    {
        string? hp = null;
        string? mp = null;
        string? name = null;
        if (member is not null)
        {
            name = NameOf(member.Unit);
            hp = member.HpMax is { } max
                ? string.Create(CultureInfo.InvariantCulture, $"{member.Hp} of {max} HP")
                : string.Create(CultureInfo.InvariantCulture, $"{member.Hp} HP");
            mp = member.Mp is { } points ? string.Create(CultureInfo.InvariantCulture, $"{points} MP") : null;
        }
        var row = change.Positioned(name, hp, mp);
        return new Announcement(
            change.Change == ScreenChange.Entered ? $"{BattleScreens.Party.Name}, {row}" : row, Interrupt: true);
    }
}
