using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>The quick menu rows' help lines (#907, ADR 0023): the details key says the focused row's, and, when
/// <see cref="SayOnFocus"/> is on, each focus says it after the row. The lines are the ones the game fetched as the menu
/// opened, never fetched again here. Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 (Steam build 18330018) the menu, as it opens, fetches each row's help line by serial in
/// cursor order, <c>System_EasyMenu_08</c>, <c>_12</c>, <c>_13</c>, <c>_10</c>, <c>_09</c>, <c>_16</c>, <c>_11</c>, <c>_14</c>
/// (a ninth row first, <c>_18</c>, later in the game), each with flags 8 and in one frame before the menu's first cursor
/// record (docs/findings/message-serials-from-file-positions.md, docs/raw-sources/quick-menu-nine-items-run-2026-11-21.md).
/// So the batch is the rows' lines in row order, which <see cref="FetchedRows"/> keeps as the view. Whether the root
/// main menu fetches its own lines is <c>[NEEDS VERIFICATION]</c>: its screen is not covered here.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.QuickMenuHelp)]
public sealed class QuickMenuHelp : IScreenObserver, ISerialResolveObserver, ICursorObserver, IScreenDetails
{
    /// <summary>The flags the menu fetches a help line with.</summary>
    public const int HelpFlags = 8;

    private const string HelpPrefix = "System_EasyMenu_";

    private readonly MessageSerials serials;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly FetchedRows rows = new(QuickMenuFocus.Screen.ScreenClass);

    public QuickMenuHelp(MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.serials = serials;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Whether the details key says a line, from its setting; on when not given.</summary>
    public Func<bool> On { get; init; } = () => true;

    /// <summary>Whether each focus says its row's line after the row, from its setting; off when not given.</summary>
    public Func<bool> SayOnFocus { get; init; } = () => false;

    /// <summary>Keeps each help line the game fetches.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (resolve.Flags == HelpFlags && serials.FindBySerial(resolve.Serial) is { } message
            && message.Label.StartsWith(HelpPrefix, StringComparison.Ordinal))
        {
            rows.Fetched(resolve.Serial);
        }
    }

    /// <summary>Takes the menu's view from each cursor input, before the screen event is told.</summary>
    public void Observe(CursorInput input) => rows.Cursor(input);

    /// <summary>Says the focused row's line after the row, when that is on.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Change is ScreenChange.Entered or ScreenChange.FocusMoved && SayOnFocus() && Help(change) is { } help)
        {
            announcer.Announce(new Announcement(help, Interrupt: false));
        }
    }

    /// <summary>The focused row's help line for the details key, or null on another screen, for a row with none, or
    /// when it is off.</summary>
    public string? Details(ScreenEvent focus)
    {
        ArgumentNullException.ThrowIfNull(focus);
        return On() ? Help(focus) : null;
    }

    private string? Help(ScreenEvent change) =>
        change.Screen == QuickMenuFocus.Screen && rows.SerialAt(change.Index) is { } serial
            ? serials.TextOf(serial, cleaner)
            : null;
}
