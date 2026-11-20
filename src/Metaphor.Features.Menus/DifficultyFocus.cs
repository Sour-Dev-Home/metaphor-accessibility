using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus;

/// <summary>Speaks New Game's difficulty list (#540): on entering it "Difficulty, name, n of 5", then on each focus
/// move "name, n of 5", each cutting off the last. The names are the system file's difficulty labels, looked up at run
/// time (<see cref="MessageLibrary"/>), or the position alone when one is missing. Safe to call from several threads.
/// </summary>
/// <remarks>On build 548ddc95 the list is <c>UUIC_Common_DifficultySelect</c>, whose cursor is a shared
/// <c>CursorManager</c> at its <c>+0x28</c>, index 0 at the top; the five labels and their order are in
/// docs/findings/difficulty-select-shared-cursor.md. That the list draws these same words is
/// <c>[NEEDS VERIFICATION]</c> by ear.</remarks>
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
        var said = ListFocus.Of(
            change, Screen, Screen.Name, NameLabels.Length, index => MessageLines.Line(messages, cleaner, NameLabels[index], 0));
        if (said is not null)
        {
            announcer.Announce(said);
        }
    }
}
