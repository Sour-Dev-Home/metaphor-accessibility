using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>Formation in battle (#259): watches the four party members' Formation bit (unit <c>+0x0</c> bit 22,
/// <see cref="PartyMember.BackRowBit"/>) and says "Junah, row changed" when one flips, interrupting. Which value is the
/// back row is <c>[NEEDS VERIFICATION]</c> by ear, so the line doesn't say which row
/// (docs/findings/battle-party-swap-and-formation-static.md, build 548ddc95…). Only read; takes each input read only
/// as a clock, as <see cref="TurnLine"/> does: it asks <see cref="IBattleScene"/> at most once every
/// <see cref="AskSceneEveryMilliseconds"/>, and while that says battle reads the party at most once every
/// <see cref="ReadEveryMilliseconds"/>. A battle's first read sets what later reads compare with and says nothing; a
/// slot's new member later in the battle is a swap with the reserve (#258), said as "Strohl, in for Hulkenberg".
/// Whether the party slots change during a battle at all is <c>[NEEDS VERIFICATION]</c>: the battle copies them back
/// at its end (docs/findings/battle-party-swap-and-formation-static.md), so the line may wait for a live read. Every read reaches the game unchanged. Safe to call from any of the game's
/// threads.</summary>
[FeatureSetting<BattleFeature>(BattleFeature.BattleFormation)]
public sealed class FormationWatch : IInputControls
{
    /// <summary>The least time between two questions to the scene.</summary>
    public const long AskSceneEveryMilliseconds = 1000;

    /// <summary>The least time between two reads of the party in a battle.</summary>
    public const long ReadEveryMilliseconds = 200;

    private readonly IBattleParty party;
    private readonly IBattleScene scene;
    private readonly IAnnouncer announcer;
    private readonly TimeProvider time;
    private readonly Func<int, int, string, string> memberSays;
    private readonly Lock gate = new();
    // Each slot's member and Formation bit as last read.
    private readonly Dictionary<int, (int Unit, bool Bit)> rows = [];
    private long sceneAsked = long.MinValue / 2;
    private long partyRead = long.MinValue / 2;
    private bool inBattle;

    /// <summary>Lines said through <paramref name="announcer"/>, timed by <paramref name="time"/>;
    /// <paramref name="memberSays"/> makes the line about a member from its slot, its character and what is said after
    /// its name, as <see cref="HitLines.MemberSays"/> does.</summary>
    public FormationWatch(
        IBattleParty party, IBattleScene scene, IAnnouncer announcer, TimeProvider time,
        Func<int, int, string, string> memberSays)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(memberSays);
        this.party = party;
        this.scene = scene;
        this.announcer = announcer;
        this.time = time;
        this.memberSays = memberSays;
    }

    /// <summary>A Debug build's log of each Formation bit and party slot change read, for the by-ear check of which
    /// value is the back row and whether a swap shows in the party slots (#258). None, when not set.</summary>
    public Action<string>? Log { get; init; }

    /// <summary>The name a character goes by, for the member a swap took out (#258); none known when unset.</summary>
    public Func<int, string?> NameOf { get; init; } = _ => null;

    public int Pad(int pad, int buttons, bool made)
    {
        Tick();
        return buttons;
    }

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys)
    {
        Tick();
        return keys;
    }

    private void Tick()
    {
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        List<string> said = [];
        List<string> logged = [];
        lock (gate)
        {
            if (now - sceneAsked >= AskSceneEveryMilliseconds)
            {
                sceneAsked = now;
                var wasInBattle = inBattle;
                inBattle = scene.InBattle();
                if (inBattle && !wasInBattle)
                {
                    rows.Clear();
                }
            }
            if (!inBattle || now - partyRead < ReadEveryMilliseconds)
            {
                return;
            }
            partyRead = now;
            foreach (var member in party.Members())
            {
                if (member.RowBit is not { } bit)
                {
                    continue;
                }
                if (!rows.TryGetValue(member.Slot, out var was))
                {
                    logged.Add(string.Create(CultureInfo.InvariantCulture,
                        $"slot={member.Slot} unit={member.Unit} bit={bit}"));
                }
                else if (was.Unit != member.Unit)
                {
                    logged.Add(string.Create(CultureInfo.InvariantCulture,
                        $"slot={member.Slot} unit {was.Unit} -> {member.Unit} bit={bit}"));
                    said.Add(memberSays(
                        member.Slot, member.Unit, NameOf(was.Unit) is { } left ? $"in for {left}" : "swapped in"));
                }
                else if (was.Bit != bit)
                {
                    logged.Add(string.Create(CultureInfo.InvariantCulture,
                        $"slot={member.Slot} unit={member.Unit} bit {was.Bit} -> {bit}"));
                    said.Add(memberSays(member.Slot, member.Unit, "row changed"));
                }
                rows[member.Slot] = (member.Unit, bit);
            }
        }
        foreach (var line in logged)
        {
            Log?.Invoke(line);
        }
        if (said.Count > 0)
        {
            announcer.Announce(new Announcement(string.Join(". ", said), Interrupt: true));
        }
    }
}
