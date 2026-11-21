using System.Globalization;
using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus;

/// <summary>Speaks a tutorial pop-up as it opens (#118): "title, n pages", then its first page's text, cutting off what
/// was being said. The title and pages are the messages the game's tutorial table names for the pop-up's tutorial
/// number (<see cref="TutorialTable"/>), found by label hash in the files the game compiled. A title that isn't
/// compiled is said as "Tutorial", and so is a pop-up whose number the table hasn't, with no page count. Safe to call
/// from several threads.</summary>
/// <remarks>A pop-up is told apart by its window's own fields: the open writes the tutorial number to the window and
/// builds the object it keeps for it (docs/findings/tutorial-popup-window-static.md). A window whose object is gone or
/// whose state says ended is closed, so the next object it holds is a new pop-up. That those fields mean this is a lead
/// from the exe and one run (#594) <c>[NEEDS VERIFICATION]</c>.</remarks>
public sealed class TutorialPopups
{
    /// <summary>What a pop-up is said as when its title can't be found.</summary>
    public const string Untitled = "Tutorial";

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Func<TutorialTable?> table;
    private readonly Lock gate = new();
    private (long Window, int Number, long Model)? spoken;

    /// <param name="messages">The compiled messages, by label hash.</param>
    /// <param name="announcer">Where the pop-up is spoken.</param>
    /// <param name="cleaner">Turns a message's text into speech.</param>
    /// <param name="table">The tutorial table, or null while it loads or when it couldn't be read.</param>
    public TutorialPopups(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner, Func<TutorialTable?> table)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(table);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.table = table;
    }

    /// <summary>The pop-up window <paramref name="window"/> after one of its updates: the tutorial number it holds, the
    /// object it keeps for that tutorial (0 for none) and whether its state says it has ended. A window holding a new
    /// object, or a new number, is a pop-up that just opened, and is spoken once.</summary>
    public void WindowUpdated(long window, int number, long model, bool ended)
    {
        lock (gate)
        {
            if (ended || model == 0)
            {
                if (spoken?.Window == window)
                {
                    spoken = null;
                }
                return;
            }
            if (spoken == (window, number, model))
            {
                return;
            }
            spoken = (window, number, model);
        }
        announcer.Announce(new Announcement(Said(number), Interrupt: true));
    }

    private string Said(int number)
    {
        if (table()?.Entry(number) is not { } entry)
        {
            return Untitled;
        }
        var title = Text(entry.TitleHash) ?? Untitled;
        var pages = entry.PageHashes.Count;
        var said = string.Create(CultureInfo.InvariantCulture, $"{title}, {pages} {(pages == 1 ? "page" : "pages")}");
        return pages > 0 && Text(entry.PageHashes[0]) is { } first ? $"{said}. {first}" : said;
    }

    private string? Text(string labelHash) =>
        messages.FindByLabelHash(labelHash) is { } message && cleaner.Clean(message.Text) is { Length: > 0 } text
            ? text
            : null;
}
