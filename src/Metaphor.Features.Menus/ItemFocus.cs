using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the camp Item screen's focus (#216): on entering the item list, "Item" and the focused row's
/// position, then each move's position ("n of total", or "n" when the game's count can't be read, as on the scrolling
/// list), each cutting off the last; the same on the party-member target list, without the heading. Row names, tab names
/// and target names are not spoken: no finding gives where the list draws them from (the item names' label scheme is
/// known, <c>ItemName_&lt;category&gt;_&lt;nn&gt;</c>, but not which row holds which item) <c>[NEEDS VERIFICATION]</c>.
/// Safe to call from several threads.</summary>
/// <remarks>Classes on build 548ddc95: <c>ItemMenuChoice</c> and <c>ItemCharaSelChoice</c>, the list and the target
/// (docs/raw-sources/screen-class-session-2026-11-17.md; <c>ItemMenuChoice</c> also in the 2026-11-22 report log, where
/// its count was 2 on one visit). The tab list's class is not confirmed.</remarks>
[FeatureSetting(Feature.Menus)]
public sealed class ItemFocus : IScreenObserver
{
    /// <summary>The Item list, by its owner's class on build 548ddc95.</summary>
    public static Screen List { get; } = new("Item", "ItemMenuChoice@ope@ui@app");

    /// <summary>The party-member target list, by its owner's class on build 548ddc95.</summary>
    public static Screen Target { get; } = new("Item target", "ItemCharaSelChoice@ope@ui@app");

    /// <summary>Both Item screens.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [List, Target];

    private readonly IAnnouncer announcer;

    public ItemFocus(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
    }

    /// <summary>Speaks the focus when an Item screen is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen is null || !Screens.Contains(change.Screen) || change.Change == ScreenChange.Left
            || change.Index < 0)
        {
            return;
        }
        var position = ListFocus.Position(change);
        var heading = change.Change == ScreenChange.Entered && change.Screen == List ? $"{List.Name}, " : "";
        announcer.Announce(new Announcement(heading + position, Interrupt: true));
    }
}
