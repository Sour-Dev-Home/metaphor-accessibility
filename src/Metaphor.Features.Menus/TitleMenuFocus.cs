using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the title menu (#94, #208): on entering the screen, "Main menu" and the focused item, then on each
/// focus move "item, n of total", each cutting off the last. The screen is recognised by its screen class
/// (<see cref="Screen"/>), so the title's message file compiling again during play changes nothing. Item text is looked
/// up at run time (<see cref="MessageLibrary"/>); only the label, the count and the third item's word are ours
/// (docs/raw-sources/title-menu-labels-2026-11-16.md, docs/raw-sources/title-menu-item-images-2026-11-20.md). Safe to
/// call from several threads.</summary>
public sealed class TitleMenuFocus : IScreenObserver
{
    /// <summary>The title menu: its choice's class on build 548ddc95, from its run-time type information
    /// (docs/raw-sources/screen-class-session-2026-11-17.md).</summary>
    public static Screen Screen { get; } = new("Title menu", "TitleChoice@ope@ui@app");

    // The message whose lines are the title menu's items in cursor order, and how many there are, on build 548ddc95
    // (docs/raw-sources/title-menu-labels-2026-11-16.md).
    private const string ItemsLabel = "Title_System_08";
    private const int ItemCount = 3;

    // The screen draws the items from images, not the message. The third image says "System" (it opens the Settings and
    // Credits submenu) while the message's third line says Settings, so that item is our own word, English only
    // (#537, docs/raw-sources/title-menu-item-images-2026-11-20.md).
    private const int SystemIndex = 2;
    private const string SystemWord = "System";

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;

    public TitleMenuFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Speaks the focus when the title menu is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        if (ListFocus.Of(change, Screen, "Main menu", ItemCount, ItemText) is { } said)
        {
            announcer.Announce(said);
        }
    }

    // The item's text: ours for the third, else the message's line, or null when the message or line is missing.
    private string? ItemText(int index)
    {
        if (index == SystemIndex)
        {
            return SystemWord;
        }

        var lines = (messages.Find(ItemsLabel)?.Text ?? "").Split('\n')
            .Select(cleaner.Clean).Where(line => line.Length > 0).ToList();
        return index < lines.Count ? lines[index] : null;
    }
}
