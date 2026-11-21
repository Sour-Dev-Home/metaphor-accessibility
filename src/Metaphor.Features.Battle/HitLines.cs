using System.Globalization;
using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Battle;

namespace Metaphor.Features.Battle;

/// <summary>Says each target a battle action hits (#247): "35 damage, 89 HP left", or "20 healed" for a gain, each
/// queued after the last so a fast run of hits loses none. Only hits whose popup carries a number are said: which of the
/// others is a miss, a block or a drain isn't known yet. The target isn't named: no safe read of its name is known.
/// Safe to call from several threads.</summary>
/// <remarks>That a negative change is damage is the static note's lead, <c>[NEEDS VERIFICATION]</c> by ear
/// (docs/findings/battle-hit-result-hp-left-static.md, build 548ddc95…).</remarks>
public sealed class HitLines : IBattleHitObserver
{
    /// <summary>How long the same hit told again is taken as the same one: the game function runs once per target, as
    /// far as the static note reads it, so this only guards against a caller that repeats it.</summary>
    public const long RepeatMilliseconds = 500;

    private readonly IAnnouncer announcer;
    private readonly RepeatGate repeats;

    /// <summary>Hits said through <paramref name="announcer"/>, repeats timed by <paramref name="millisecondsNow"/>.
    /// </summary>
    public HitLines(IAnnouncer announcer, Func<long> millisecondsNow)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(millisecondsNow);
        this.announcer = announcer;
        repeats = new RepeatGate(RepeatMilliseconds, millisecondsNow);
    }

    public void Observe(BattleHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        if (!hit.HasNumber || !repeats.LetThrough(hit.Target, ((long)hit.HpChange << 32) | (uint)hit.HpLeft))
        {
            return;
        }
        var change = hit.HpChange switch
        {
            < 0 => string.Create(CultureInfo.InvariantCulture, $"{-(long)hit.HpChange} damage"),
            > 0 => string.Create(CultureInfo.InvariantCulture, $"{hit.HpChange} healed"),
            _ => "no damage",
        };
        announcer.Announce(new Announcement(
            string.Create(CultureInfo.InvariantCulture, $"{change}, {hit.HpLeft} HP left"), Interrupt: false));
    }
}
