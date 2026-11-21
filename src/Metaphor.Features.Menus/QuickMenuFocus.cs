using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the field's quick menu (#584): on entering it and on each focus move, "name, n of 8", each cutting
/// off the last. The names are the system file's quick-menu labels, looked up at run time
/// (<see cref="MessageLibrary"/>), so nothing here is game content. A menu of another length (a ninth item comes later
/// in the game) is not this order, so only its position is said. Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 the menu's class is <c>InstantMenuChoice</c>; as it opens, the game fetches its items'
/// help lines by serial in cursor order, and each help line's label names its item's label
/// (docs/findings/message-serials-from-file-positions.md). The order is read from one run's log, not yet heard
/// <c>[NEEDS VERIFICATION]</c>.</remarks>
public sealed class QuickMenuFocus : IScreenObserver
{
    /// <summary>The quick menu, by its owner's class on build 548ddc95.</summary>
    public static Screen Screen { get; } = new("Quick menu", "InstantMenuChoice@ope@ui@app");

    // The item names in cursor order, on build 548ddc95 (docs/findings/message-serials-from-file-positions.md).
    private const int ItemCount = 8;
    private static readonly string[] NameLabels =
    [
        "System_EasyMenu_01", "System_EasyMenu_05", "System_EasyMenu_06", "System_EasyMenu_03",
        "System_EasyMenu_02", "System_EasyMenu_15", "System_EasyMenu_04", "System_EasyMenu_07",
    ];

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;

    public QuickMenuFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Speaks the focus when the quick menu is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != Screen || change.Change == ScreenChange.Left || change.Index < 0)
        {
            return;
        }
        var ours = change.Total is null or ItemCount && change.Index < ItemCount;
        var said = ours
            ? ListFocus.Of(change, Screen, heading: null, ItemCount, Name)
            : new Announcement(change.Total is { } total ? $"{change.Index + 1} of {total}" : $"{change.Index + 1}", Interrupt: true);
        if (said is not null)
        {
            announcer.Announce(said);
        }
    }

    private string? Name(int index) =>
        messages.Find(NameLabels[index]) is { } message && cleaner.Clean(message.Text) is { Length: > 0 } text
            ? text
            : null;
}
