using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the Settings category list and names its pages (#559, parent #96): on opening the list, "Settings"
/// and the focused category, then each category's text from the game's message file and "n of 5"; on opening the System, Sound, Graphics or Display page, that page's
/// category text and the focus's position, then on each move the position alone until the rows are read (#560). Each
/// announcement cuts off the last. The text is looked up at run time (<see cref="MessageLibrary"/>), so nothing here is
/// game content. Safe to call from several threads.</summary>
/// <remarks>Labels, classes and which row opens which page on build 548ddc95:
/// docs/raw-sources/settings-category-labels-session1-2026-11-20.md.</remarks>
public sealed class SettingsFocus : IScreenObserver
{
    // The category list, opened from the title or the field's quick menu, and from the main menu in play: one list.
    private static readonly Screen List = new("Settings", "?$ConfigTopChoicePc@VConfigTopChoicePcLayoutInit@ui@app@@@ope@ui@app");
    private static readonly Screen ListInPlay = new("Settings", "?$ConfigTopChoicePc@VConfigTopChoicePcLayout@ui@app@@@ope@ui@app");

    // The pages the list's first four rows open, in row order.
    private static readonly Screen[] Pages =
    [
        new("System settings", "?$ConfigMenuChoicePcT@$0HG@@ope@ui@app"),
        new("Sound settings", "?$ConfigMenuChoicePcT@$0HH@@ope@ui@app"),
        new("Graphics settings", "?$ConfigMenuChoicePcT@$0HI@@ope@ui@app"),
        new("Display settings", "?$ConfigMenuChoicePcT@$0HJ@@ope@ui@app"),
    ];

    // The list's rows in cursor order, and the message labels that name them; a page is named by its row's label.
    private const int CategoryCount = 5;
    private const string CategoryLabel = "System_category_name_PC_0";

    // The list's name, said when it opens. The game draws no heading from its message files there, so the word is ours,
    // English only: the game's own word for the item that opens the list. Approved by the maintainer on 2026-11-20 (#559).
    private const string ListName = "Settings";

    /// <summary>The category list (both its classes) and the four pages, for the menu-screens module.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [List, ListInPlay, .. Pages];

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;

    public SettingsFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Speaks the focus when the list or a page is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Change == ScreenChange.Left || change.Index < 0 || Said(change) is not { } text)
        {
            return;
        }
        announcer.Announce(new Announcement(text, Interrupt: true));
    }

    private string? Said(ScreenEvent change)
    {
        if (change.Screen == List || change.Screen == ListInPlay)
        {
            if (change.Index >= CategoryCount)
            {
                return null;
            }
            var item = Join(CategoryText(change.Index), $"{change.Index + 1} of {CategoryCount}");
            return change.Change == ScreenChange.Entered ? $"{ListName}, {item}" : item;
        }
        var page = Array.IndexOf(Pages, change.Screen);
        if (page < 0)
        {
            return null;
        }
        var position = change.Total is { } total ? $"{change.Index + 1} of {total}" : $"{change.Index + 1}";
        return change.Change == ScreenChange.Entered ? Join(CategoryText(page), position) : position;
    }

    private static string Join(string? name, string position) => name is null ? position : $"{name}, {position}";

    // The category's text, cleaned, or null when the message is missing or empty.
    private string? CategoryText(int index) =>
        messages.Find(CategoryLabel + (index + 1)) is { } message && cleaner.Clean(message.Text) is { Length: > 0 } text
            ? text
            : null;
}
