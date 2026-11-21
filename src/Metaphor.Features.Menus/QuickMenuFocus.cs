using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the field's quick menu (#584): on entering it and on each focus move, "name, n of 8", each cutting
/// off the last. Five names are the system file's quick-menu labels, looked up at run time
/// (<see cref="MessageLibrary"/>); the other three are the mod's own English words, since the screen draws words no
/// message holds. A menu of another length (a ninth item comes later in the game) is not this order, so only its
/// position is said. Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 the menu's class is <c>InstantMenuChoice</c>; as it opens, the game fetches its items'
/// help lines by serial in cursor order, and each help line's label names its item's label
/// (docs/findings/message-serials-from-file-positions.md). The order was heard right by the maintainer on 2026-11-21
/// (#591).</remarks>
[FeatureSetting(Feature.Menus)]
public sealed class QuickMenuFocus : IScreenObserver
{
    /// <summary>The quick menu, by its owner's class on build 548ddc95.</summary>
    public static Screen Screen { get; } = new("Quick menu", "InstantMenuChoice@ope@ui@app");

    // The items in cursor order, on build 548ddc95 (docs/findings/message-serials-from-file-positions.md): a label from
    // the system file, or null for a row whose own word is ours (OwnWords).
    private const int ItemCount = 8;
    private static readonly string?[] NameLabels =
    [
        "System_EasyMenu_01", "System_EasyMenu_05", "System_EasyMenu_06", null,
        "System_EasyMenu_02", null, null, "System_EasyMenu_07",
    ];

    // Rows 4, 6 and 7 draw words no compiled message holds, so their labels (System_EasyMenu_03, _15, _04) said
    // something else than the screen (heard by the maintainer on 2026-11-21, #591). The words are ours, English only, as
    // the screen shows them; chosen by the maintainer on 2026-11-21 (#584).
    private static readonly Dictionary<int, string> OwnWords = new()
    {
        [3] = "Tutorials",
        [5] = "Memorandum",
        [6] = "Settings",
    };

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
        OwnWords.TryGetValue(index, out var word)
            ? word
            : NameLabels[index] is { } label && messages.Find(label) is { } message
                && cleaner.Clean(message.Text) is { Length: > 0 } text
                ? text
                : null;
}
