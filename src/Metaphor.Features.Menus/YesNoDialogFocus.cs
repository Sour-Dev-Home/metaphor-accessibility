using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus;

/// <summary>Speaks a yes/no dialog (#549), such as the name-entry confirmation: on entering it and on each focus move,
/// "choice, n of 2", each cutting off the last. The choice's text is the line of the game's yes/no choice message at the
/// cursor's index, looked up at run time (<see cref="MessageLibrary"/>), or the position alone when it's missing. Safe to
/// call from several threads.</summary>
/// <remarks>On build 548ddc95 the name-entry confirmation takes its choices from <c>System_Choice_YESNO</c>, Yes then
/// No, and index 0 accepted the name (docs/raw-sources/yes-no-dialog-labels-2026-11-20.md). Whether every dialog of this
/// class, such as the settings confirmations (#562), uses that message is <c>[NEEDS VERIFICATION]</c>.</remarks>
public sealed class YesNoDialogFocus : IScreenObserver
{
    /// <summary>A yes/no dialog: its choice's class on build 548ddc95, from its run-time type information
    /// (docs/raw-sources/screen-class-session-2026-11-17.md, yes-no-dialog-labels-2026-11-20.md).</summary>
    public static Screen Screen { get; } = new("Yes or no", "YesNoChoiceSample@ope@ui@app");

    // The message whose lines are the choices in cursor order, and how many there are, on build 548ddc95
    // (docs/raw-sources/yes-no-dialog-labels-2026-11-20.md).
    private const string ChoicesLabel = "System_Choice_YESNO";
    private const int ChoiceCount = 2;

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;

    public YesNoDialogFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Speaks the focus when a yes/no dialog is entered or its focus moves. No heading: the dialog's question
    /// is the game's and isn't known here.</summary>
    public void Observe(ScreenEvent change)
    {
        var said = ListFocus.Of(
            change, Screen, heading: null, ChoiceCount, index => MessageLines.Line(messages, cleaner, ChoicesLabel, index));
        if (said is not null)
        {
            announcer.Announce(said);
        }
    }
}
