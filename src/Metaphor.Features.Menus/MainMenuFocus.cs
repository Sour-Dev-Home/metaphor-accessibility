using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the in-game main menu (#97, spec #211): on entering the screen, "Menu" and the focused entry,
/// then on each focus move "label, n of total", each cutting off the last. The menu draws its entries as sprites, so
/// the labels are the mod's own (<see cref="Labels"/>). Safe to call from several threads.
/// </summary>
[FeatureSetting(Feature.Menus)]
public sealed class MainMenuFocus : IScreenObserver
{
    /// <summary>The main menu: its choice's class on build 548ddc95, from its run-time type information
    /// (docs/raw-sources/screen-class-session-2026-11-17.md).</summary>
    public static Screen Screen { get; } = new("Menu", "RootMenuChoice@ope@ui@app");

    /// <summary>The mod's own English wording for the main menu's entries (#248), in cursor order on build 548ddc95
    /// (docs/raw-sources/main-menu-session-2026-11-17.md, menu-screens-inventory-2026-11-17.md). The game draws them as
    /// sprites, so these are not game content; a game label found later replaces ours. Ready for translation.</summary>
    internal static IReadOnlyList<string> Labels { get; } =
        ["Skill", "Item", "Equipment", "Party", "Follower", "Quest", "Calendar", "Memorandum", "System"];

    private static int EntryCount => Labels.Count;

    private readonly IAnnouncer announcer;

    public MainMenuFocus(IAnnouncer announcer)
    {
        ArgumentNullException.ThrowIfNull(announcer);
        this.announcer = announcer;
    }

    /// <summary>Speaks the focus when the main menu is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        if (ListFocus.Of(change, Screen, Screen.Name, EntryCount, index => Labels[index]) is { } said)
        {
            announcer.Announce(said);
        }
    }
}
