using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The "Skip?" menu the Menu button opens in a 2D cutscene (#809): it has no cursor and fetches no message the
/// mod sees, so when the game asks its component to show it (<see cref="MoviePauseRequests"/>), <see cref="Opened"/>
/// says the question and its two buttons for the device the game draws glyphs for (#778).</summary>
/// <remarks>On build 548ddc95 (Steam 18330018; by-ear session 2026-11-23 evening, mod 4d936b2): every
/// <c>ui-anim class=MoviePauseLayout@ui@app anim=4768</c> landed on the same millisecond as a back press (pad B, or
/// Escape), and each open was a Start press then cue 4 with no <c>ui-anim</c>, so the layout's animation is the close.
/// The open is the component's show request (docs/findings/movie-pause-open-static.md), not yet heard. The words are
/// the mod's own: which message, if any, the layout draws is <c>[NEEDS VERIFICATION]</c>. Confirm is A on a pad or
/// Space on the keyboard, as on the game-over screen (#623); B, Escape and C all go back (heard 2026-11-23).</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.SkipSceneQuestion)]
public sealed class SkipSceneQuestion : IMoviePauseObserver
{
    /// <summary>The skip menu's layout class.</summary>
    public const string Layout = "MoviePauseLayout@ui@app";

    /// <summary>The animation the layout starts as the menu closes, not as it opens.</summary>
    public const int CloseAnimation = 4768;

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

    /// <summary>Says the question as the menu opens.</summary>
    public void Opened()
    {
        var (confirm, back) = device.Current == InputDevice.Keyboard ? ("Space", "Escape") : ("A", "B");
        announcer.Announce(new Announcement($"Skip scene? {confirm} to skip, {back} to keep watching.", Interrupt: true));
    }
}
