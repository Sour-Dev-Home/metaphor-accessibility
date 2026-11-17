using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the title menu's focus (#94). The first menu cursor whose input the game handles after it compiles
/// the title's message file is taken as the title menu's; when it first becomes active this says "Main menu" and the
/// focused item, then each item the cursor settles on as "item, n of total", each cutting off the last. Item text is
/// looked up at run time (<see cref="MessageLibrary"/>); only the label and the count are ours
/// (docs/raw-sources/title-menu-labels-2026-11-16.md). Safe to call from several threads.</summary>
public sealed class TitleMenuFocus : IMessageFileObserver, ICursorObserver
{
    // The title's message file, under each language's folder (seen as EN/message/system/Title_System.msg).
    private const string TitleFileEnd = "/system/Title_System.msg";

    // The message whose lines are the title menu's items in cursor order, and how many there are, on build 548ddc95
    // (docs/raw-sources/title-menu-labels-2026-11-16.md).
    private const string ItemsLabel = "Title_System_08";
    private const int ItemCount = 3;

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Lock gate = new();
    private bool titleFileCompiled;
    private long? titleCursor;
    private int? lastIndex;

    public TitleMenuFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Watches for the title's message file: the next cursor to become active is the title menu's.</summary>
    public void Observe(MessageFileCompiled file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Path.Replace('\\', '/').EndsWith(TitleFileEnd, StringComparison.OrdinalIgnoreCase))
        {
            lock (gate)
            {
                titleFileCompiled = true;
                titleCursor = null;
                lastIndex = null;
            }
        }
    }

    /// <summary>Speaks the title menu's item where its cursor settled, when that changed. A move is skipped: the game
    /// can refuse one and put the cursor back on its next call.</summary>
    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.IsMove || input.Index is < 0 or >= ItemCount)
        {
            return;
        }
        bool first;
        lock (gate)
        {
            if (!titleFileCompiled || (titleCursor is { } title && title != input.Manager) || lastIndex == input.Index)
            {
                return;
            }
            first = titleCursor is null;
            titleCursor = input.Manager;
            lastIndex = input.Index;
        }
        var item = $"{ItemText(input.Index)}{input.Index + 1} of {ItemCount}";
        announcer.Announce(new Announcement(first ? $"Main menu, {item}" : item, Interrupt: true));
    }

    // The item's text and a comma, or nothing when its message or line is missing.
    private string ItemText(int index)
    {
        var lines = (messages.Find(ItemsLabel)?.Text ?? "").Split('\n')
            .Select(cleaner.Clean).Where(line => line.Length > 0).ToList();
        return index < lines.Count ? $"{lines[index]}, " : "";
    }
}
