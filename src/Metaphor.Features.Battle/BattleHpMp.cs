using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;
using Metaphor.Core.Input;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>The HP and MP key (#692, the maintainer's decision of 2026-11-23): in a battle, <c>keys.battle.hpMpKey</c>
/// (M) pressed alone says each party member, by slot, "name HP of max HP, MP" ("of max" once a hit has shown it), and is
/// hidden from the game; elsewhere it reaches it, as <see cref="BattleKey"/> does. No pad button yet. MP is the unit's
/// <c>+0x10</c>, as the hit popup's MP getter reads it (docs/findings/battle-hit-result-hp-left-static.md, build
/// 548ddc95…).</summary>
/// <remarks>The decision asks for the current character's HP and MP. Which member is acting is a lead only, the command
/// panel's <c>+0x128</c> (docs/findings/battle-command-panel-static.md, <c>[NEEDS VERIFICATION]</c>), and no hook reads
/// it yet, so the key says every member.</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleHits, BattleFeature.BattleHpMp)]
public sealed class BattleHpMp : IInputControls
{
    private readonly IBattleParty party;
    private readonly HitLines hits;
    private readonly BattleKey press;

    /// <summary>The key on the bindings in force, read on each input read (#661).</summary>
    public BattleHpMp(
        IBattleParty party, HitLines hits, IBattleScene scene, IAnnouncer announcer, Func<BattleBindings> bindings)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(bindings);
        this.party = party;
        this.hits = hits;
        press = new BattleKey(
            () => 0, () => bindings().HpMpKey, scene, () => announcer.Announce(new Announcement(Says(), Interrupt: true)));
    }

    public int Pad(int pad, int buttons, bool made) => press.Pad(pad, buttons, made);

    public IReadOnlyList<int> Keys(IReadOnlyList<int> keys) => press.Keys(keys);

    /// <summary>What the key says now.</summary>
    public string Says()
    {
        var members = party.Members().OrderBy(member => member.Slot).Select(member =>
        {
            var hp = member.HpMax is { } max
                ? string.Create(CultureInfo.InvariantCulture, $"{member.Hp} of {max} HP")
                : string.Create(CultureInfo.InvariantCulture, $"{member.Hp} HP");
            var mp = member.Mp is { } points ? string.Create(CultureInfo.InvariantCulture, $", {points} MP") : "";
            return hits.MemberSays(member.Slot, member.Unit, hp + mp);
        }).ToList();
        return members.Count > 0 ? string.Join(". ", members) : "No party status";
    }
}
