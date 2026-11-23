using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Battle;

/// <summary>Says "Ambushed" when a battle opens with the enemy striking first (#626): its start wipe's layout plays
/// <see cref="AmbushAnimation"/>. Other starts say nothing; a player-advantage start isn't told apart from a plain one
/// yet (<c>[NEEDS VERIFICATION]</c>).</summary>
/// <remarks>Build 548ddc95… (Steam build 18330018): <c>BattleCutInWipeLayout</c> played 910 at both enemy-first starts
/// in four Debug session logs and 906 at the ten others
/// (docs/findings/battle-turn-side-start-and-end-layouts-logged.md). The word is the mod's own.</remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleStart)]
public sealed class BattleStartLine(IAnnouncer announcer) : ILayoutAnimationObserver
{
    /// <summary>The battle start wipe's layout class.</summary>
    public const string Layout = "BattleCutInWipeLayout@ui@app";

    /// <summary>The animation it plays when the enemy strikes first.</summary>
    public const int AmbushAnimation = 910;

    private readonly IAnnouncer announcer = announcer ?? throw new ArgumentNullException(nameof(announcer));

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Layouts { get; } = [Layout];

    /// <summary>Says "Ambushed" for <see cref="AmbushAnimation"/>, queued.</summary>
    public void Played(LayoutAnimation played)
    {
        ArgumentNullException.ThrowIfNull(played);
        if (played.Layout == Layout && played.Animation == AmbushAnimation)
        {
            announcer.Announce(new Announcement("Ambushed", Interrupt: false));
        }
    }
}

/// <summary>Says how a battle ended (#166): "Victory", "Escaped" or "Defeated", as the turn panel's layout plays its
/// closing animation, queued behind the last hit lines. The results screen's numbers are not read yet.</summary>
/// <remarks>Build 548ddc95… (Steam build 18330018): <c>TurnInfoLayout</c> played 2572 after the last enemy fell (three
/// battles), 2570 after an escape (three) and 2571 after the protagonist fell (two), once each battle, in four Debug
/// session logs (docs/findings/battle-turn-side-start-and-end-layouts-logged.md). The words are the mod's own.
/// </remarks>
[FeatureSetting<BattleFeature>(BattleFeature.BattleEnd)]
public sealed class BattleEndLine(IAnnouncer announcer) : ILayoutAnimationObserver
{
    /// <summary>The turn panel's layout class.</summary>
    public const string Layout = "TurnInfoLayout@ui@app";

    /// <summary>The animation it plays after an escape.</summary>
    public const int EscapedAnimation = 2570;

    /// <summary>The animation it plays after a loss.</summary>
    public const int DefeatedAnimation = 2571;

    /// <summary>The animation it plays after a win.</summary>
    public const int VictoryAnimation = 2572;

    private readonly IAnnouncer announcer = announcer ?? throw new ArgumentNullException(nameof(announcer));

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Layouts { get; } = [Layout];

    /// <summary>Says the ending for one of the three animations, queued; any other says nothing.</summary>
    public void Played(LayoutAnimation played)
    {
        ArgumentNullException.ThrowIfNull(played);
        var said = played.Layout != Layout ? null : played.Animation switch
        {
            VictoryAnimation => "Victory",
            EscapedAnimation => "Escaped",
            DefeatedAnimation => "Defeated",
            _ => null,
        };
        if (said is not null)
        {
            announcer.Announce(new Announcement(said, Interrupt: false));
        }
    }
}
