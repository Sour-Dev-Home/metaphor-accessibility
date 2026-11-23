namespace Metaphor.Features.Field;

/// <summary>The one check every field cue and sound goes through (#702): the beacon's beep, the reach call-outs, and the
/// sonar and wall tones to come play only while the player roams the field freely, and go quiet as soon as a cutscene,
/// dialogue, menu or scripted move takes control, until free roam comes back. The architecture tests hold every type in
/// the field feature that plays a cue or speaks to this gate (docs/coding-standards.md, rule 19).
/// <para>The rule is Arcana's, the Persona 3 Reload accessibility mod, which silences its field sounds whenever the game
/// takes control away: <c>ChipsAhoiMcCoy/arcana</c>, Zlib licence, <c>main</c> at
/// <c>ed3b005f86b1cde27e30ab299ae91af3d974c988</c>, <c>CHANGELOG.md</c> (Unreleased). No code is copied
/// (docs/raw-sources/arcana-combat-navigation-2026-11-22.md).</para>
/// <para>Free roam is <see cref="IFieldReads.InFreeRoam"/>: no menu, no message window, and the field's idle scene. Whether
/// the idle scene also ends while a script walks the player is [NEEDS VERIFICATION]: no session has logged it yet. The
/// static lead for one that doesn't, the player's input lock and auto-control, is in
/// docs/raw-sources/field-state-and-map-2026-11-16.md.</para></summary>
public sealed class FreeRoamGate
{
    private readonly IFieldReads field;
    private readonly long settleMilliseconds;
    private readonly Func<long>? milliseconds;
    private long closedAt;
    private bool closedBefore;

    /// <summary>A gate that asks <paramref name="field"/>. With a <paramref name="settleMilliseconds"/> above zero and a
    /// clock, it stays shut after it has seen free roam end until free roam has held that long (#709: the log showed
    /// free roam reading true for a moment between the lines of one scene).</summary>
    public FreeRoamGate(IFieldReads field, long settleMilliseconds = 0, Func<long>? milliseconds = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (settleMilliseconds > 0)
        {
            ArgumentNullException.ThrowIfNull(milliseconds);
        }
        this.field = field;
        this.settleMilliseconds = settleMilliseconds;
        this.milliseconds = milliseconds;
    }

    /// <summary>Whether a field sound may play now: the player roams freely, and has for the settle time since it last
    /// ended. A read that throws reaches the caller.</summary>
    public bool Open()
    {
        if (!field.InFreeRoam())
        {
            if (milliseconds is not null)
            {
                closedAt = milliseconds();
                closedBefore = true;
            }
            return false;
        }
        return !closedBefore || milliseconds!() - closedAt >= settleMilliseconds;
    }
}
