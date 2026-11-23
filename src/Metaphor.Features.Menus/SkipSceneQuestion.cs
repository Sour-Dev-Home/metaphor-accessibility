using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The "Skip?" menu the Menu button opens in a 2D cutscene (#809): it has no cursor and fetches no message the
/// mod sees, so its layout's open animation is what tells it is up, and it says the question and its two buttons for
/// the device the game draws glyphs for (#778).</summary>
/// <remarks>On build 548ddc95 (Steam 18330018; by-ear session 2026-11-23, mod 0c122d3, F8 mark 19): the menu opened with
/// the <c>ui-anim</c> record <c>class=MoviePauseLayout@ui@app anim=4768</c>, then cue 4; confirm played cue 2107 and
/// skipped the scene. The words are the mod's own: which message, if any, the layout draws is
/// <c>[NEEDS VERIFICATION]</c>. Confirm is A on a pad or Space on the keyboard, as on the game-over screen (#623);
/// B and Escape for back are <c>[NEEDS VERIFICATION]</c> by ear.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.SkipSceneQuestion)]
public sealed class SkipSceneQuestion : ILayoutAnimationObserver
{
    /// <summary>The skip menu's layout class.</summary>
    public const string Layout = "MoviePauseLayout@ui@app";

    /// <summary>The animation the layout starts as the menu opens.</summary>
    public const int OpenAnimation = 4768;

    private readonly IAnnouncer announcer;
    private readonly LastInputDevice device;

    /// <param name="announcer">Where the question is said, behind its setting.</param>
    /// <param name="device">The device the game draws button glyphs for, to name the buttons.</param>
    public SkipSceneQuestion(IAnnouncer announcer, LastInputDevice device)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(device);
        this.announcer = announcer;
        this.device = device;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Layouts { get; } = [Layout];

    /// <summary>Says the question when the menu opens; the layout's other animations say nothing.</summary>
    public void Played(LayoutAnimation played)
    {
        ArgumentNullException.ThrowIfNull(played);
        if (played.Layout != Layout || played.Animation != OpenAnimation)
        {
            return;
        }
        var (confirm, back) = device.Current == InputDevice.Keyboard ? ("Space", "Escape") : ("A", "B");
        announcer.Announce(new Announcement($"Skip scene? {confirm} to skip, {back} to keep watching.", Interrupt: true));
    }
}
