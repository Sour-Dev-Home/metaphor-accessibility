using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the camp Equipment screen's focus (#217): on entering the character list, "Equipment" and the
/// position, then each move's position ("n of total", or "n" when the game's count can't be read), each cutting off the
/// last, on the characters, the slots, a slot's items and the swap's characters. Character names, slot names, item
/// names, the "equipped" mark and the sort key are not spoken: no finding gives their source, and the slot rows' order
/// against the issue's list is unconfirmed <c>[NEEDS VERIFICATION]</c>. Safe to call from several threads.</summary>
/// <remarks>Classes on build 548ddc95 (docs/raw-sources/screen-class-session-2026-11-17.md): the characters, slots,
/// items and swap, in that order. The slot list had 5 rows in the issue's description; the count is the game's.</remarks>
[FeatureSetting(Feature.Menus)]
public sealed class EquipmentFocus : IScreenObserver
{
    /// <summary>The character list, by its owner's class on build 548ddc95.</summary>
    public static Screen Characters { get; } = new("Equipment", "EquipCharaSelChoice@ope@ui@app");

    /// <summary>The slot list.</summary>
    public static Screen Slots { get; } = new("Equipment slots", "EquipPartsChoice@ope@ui@app");

    /// <summary>The list of items for a slot.</summary>
    public static Screen Items { get; } = new("Equipment items", "EquipMenuItemList@ope@ui@app");

    /// <summary>The characters a swap can go to.</summary>
    public static Screen Swap { get; } = new("Equipment swap", "EquipSwapCharaSelChoice@ope@ui@app");

    /// <summary>All the Equipment screens.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [Characters, Slots, Items, Swap];

    private readonly IAnnouncer announcer;

    public EquipmentFocus(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
    }

    /// <summary>Speaks the focus when an Equipment screen is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen is null || !Screens.Contains(change.Screen) || change.Change == ScreenChange.Left
            || change.Index < 0)
        {
            return;
        }
        var heading = change.Change == ScreenChange.Entered && change.Screen == Characters ? $"{Characters.Name}, " : "";
        announcer.Announce(new Announcement(heading + ListFocus.Position(change), Interrupt: true));
    }
}
