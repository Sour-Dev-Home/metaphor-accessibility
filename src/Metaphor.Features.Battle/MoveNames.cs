using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>Says the skill a battle's move banner shows (#954): the skill's name as it is used, queued before its
/// hits. A kind 0 banner's id is the skill's <c>Skill.TBL</c> row, whose name label hash
/// (<see cref="Core.Battle.SkillCosts.LabelHashOf"/>) finds the name in <see cref="MessageSerials.SkillNames"/>. A basic
/// attack (id 0) and a skill whose name isn't known say nothing. Safe to call from several threads.</summary>
/// <remarks>Build 548ddc95… (Steam build 18330018): the banner reads its label from the skill record's <c>+0x2</c>
/// (docs/findings/battle-affinity-discovery-from-move-banner.md). The banner hands its layout a text reference, so the
/// name never passes a message lookup the mod hooks (docs/findings/battle-move-banner-text-reference-static.md), and
/// before this the move went unsaid. The party's own moves are named as the enemies' are (the maintainer's decision of
/// 2026-11-24 on #954), so nothing here asks whose move it is; whether the game shows the banner for the party's moves
/// is <c>[NEEDS VERIFICATION]</c> by ear. The idea of saying the skill-name banner comes from Arcana, the Persona 3 Reload accessibility mod
/// (<c>ChipsAhoiMcCoy/arcana</c>, Zlib licence, <c>main</c> at <c>ed3b005f86b1cde27e30ab299ae91af3d974c988</c>,
/// <c>src/screens/battle.cpp</c> L107-124, as docs/raw-sources/arcana-combat-navigation-2026-11-22.md describes it); no
/// code is copied.</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleMoveNames)]
public sealed class MoveNames
{
    private readonly Func<int, string?> labelHashOf;
    private readonly MessageSerials serials;
    private readonly TextCleaner cleaner;
    private readonly IAnnouncer announcer;

    /// <param name="labelHashOf">A skill id's name label hash, or null when unknown or not loaded.</param>
    /// <param name="serials">The messages, holding the skill names file.</param>
    /// <param name="cleaner">How a name is said.</param>
    /// <param name="announcer">Where the name goes, queued.</param>
    public MoveNames(Func<int, string?> labelHashOf, MessageSerials serials, TextCleaner cleaner, IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(labelHashOf);
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(announcer);
        this.labelHashOf = labelHashOf;
        this.serials = serials;
        this.cleaner = cleaner;
        this.announcer = announcer;
    }

    /// <summary>A move banner showed skill <paramref name="skill"/> (0 for a basic attack).</summary>
    public void Moved(int skill)
    {
        if (skill == 0 || labelHashOf(skill) is not { } hash ||
            serials.FindByLabelHash(MessageSerials.SkillNames, hash) is not { } serial ||
            serials.TextOf(serial, cleaner) is not { } name)
        {
            return;
        }
        announcer.Announce(new Announcement(name, Interrupt: false));
    }
}
