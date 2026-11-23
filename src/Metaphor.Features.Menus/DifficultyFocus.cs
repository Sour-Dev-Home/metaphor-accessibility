using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks New Game's difficulty list (#540): on entering it "Difficulty, name, n of 5", then on each focus
/// move "name, n of 5", each cutting off the last. The names are the system file's difficulty labels, looked up at run
/// time (<see cref="MessageLibrary"/>), or the position alone when one is missing. Safe to call from several threads.
/// </summary>
/// <remarks>On build 548ddc95 the list is <c>UUIC_Common_DifficultySelect</c>, whose cursor is a shared
/// <c>CursorManager</c> at its <c>+0x28</c>, index 0 at the bottom; the five labels and their order are in
/// docs/findings/difficulty-select-shared-cursor.md. The order on screen, top first, was heard by the maintainer on
/// 2026-11-20.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.Menus)]
public sealed class DifficultyFocus : IScreenObserver
{
    /// <summary>The difficulty list: its owner's class on build 548ddc95, from its run-time type information, shown
    /// while the menus read closed (docs/findings/difficulty-select-shared-cursor.md).</summary>
    public static Screen Screen { get; } =
        new("Difficulty", "UUIC_Common_DifficultySelect@ui@app", OutsideMenus: true);

    // The names in cursor order, top first, on build 548ddc95 (docs/findings/difficulty-select-shared-cursor.md).
    private static readonly string[] NameLabels =
        ["System_General_17", "System_General_18", "System_General_19", "System_General_20", "System_General_20_1"];

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;

    public DifficultyFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Speaks the focus when the difficulty list is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);

        // The cursor counts from the bottom: index 0 is the last name on screen, and Up raises the index (heard by the
        // maintainer, 2026-11-20: Up from "Normal" on index 2 showed "Easy" on index 3). Turn it into the on-screen
        // position, top first.
        var fromTop = change.Index >= 0 && change.Index < NameLabels.Length
            ? change with { Index = NameLabels.Length - 1 - change.Index }
            : change;
        var said = ListFocus.Of(
            fromTop, Screen, Screen.Name, NameLabels.Length, index => MessageLines.Line(messages, cleaner, NameLabels[index], 0));
        if (said is not null)
        {
            announcer.Announce(said);
        }
    }
}
