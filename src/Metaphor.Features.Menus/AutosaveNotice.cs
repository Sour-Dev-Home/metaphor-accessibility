using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Says "Autosaving" when the field's autosave notice shows (#543): its layout plays
/// <see cref="ShowAnimation"/>. Queued, so it never cuts dialogue. The word is the mod's own.</summary>
/// <remarks>Build 548ddc95… (Steam build 18330018): every field autosave in four Debug session logs was one
/// <c>AutoSavingLayout</c> record with animation 748, and a manual save was <c>SystemSavingLayout</c> 758
/// (docs/findings/autosave-notice-is-a-layout-animation.md). The manual save isn't said here: its pop-up already
/// speaks (#622).</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.AutosaveNotice)]
public sealed class AutosaveNotice(IAnnouncer announcer) : ILayoutAnimationObserver
{
    /// <summary>The autosave notice's layout class.</summary>
    public const string Layout = "AutoSavingLayout@ui@app";

    /// <summary>The animation it plays as the notice shows.</summary>
    public const int ShowAnimation = 748;

    private readonly IAnnouncer announcer = announcer ?? throw new ArgumentNullException(nameof(announcer));

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Layouts { get; } = [Layout];

    /// <summary>Says "Autosaving" for <see cref="ShowAnimation"/>, queued; anything else says nothing.</summary>
    public void Played(LayoutAnimation played)
    {
        ArgumentNullException.ThrowIfNull(played);
        if (played.Layout == Layout && played.Animation == ShowAnimation)
        {
            announcer.Announce(new Announcement("Autosaving", Interrupt: false));
        }
    }
}
