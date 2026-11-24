using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the System &gt; Tutorials list (#806): on entering it "Tutorials", then each row as its tutorial's
/// title and position ("Walking About, 3 of 23"), each cutting off the last. A row's tutorial is the number its list
/// keeps for it (<see cref="ScreenEvent.RowId"/>, <see cref="TutorialList"/>), and its title the message the tutorial
/// table names for that number, the same one its pop-up says (#118). A row whose number or title isn't known, or every
/// row while the feature is off, is said by its position. Safe to call from several threads.</summary>
[FeatureSetting<MenusFeature>(MenusFeature.TutorialList)]
public sealed class TutorialListFocus : IScreenObserver
{
    /// <summary>The list, by its choice's class on build 548ddc95.</summary>
    public static Screen Screen { get; } = new("Tutorials", TutorialList.ScreenClass);

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Func<TutorialTable?> table;
    private readonly Func<bool> on;

    /// <param name="messages">The compiled messages, by label hash.</param>
    /// <param name="announcer">Where the rows are spoken: the menus' speech, so the positions stay with them.</param>
    /// <param name="cleaner">Turns a title's text into speech.</param>
    /// <param name="table">The tutorial table, or null while it loads or when it couldn't be read.</param>
    /// <param name="on">Whether the titles are said; off, the positions alone.</param>
    public TutorialListFocus(
        IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner, Func<TutorialTable?> table, Func<bool> on)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(on);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.table = table;
        this.on = on;
    }

    /// <summary>Speaks the focus when the list is entered or its focus moves, a tab change included.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != Screen || change.Change == ScreenChange.Left || change.Index < 0)
        {
            return;
        }
        var line = change.Positioned(on() ? Title(change.RowId) : null);
        var heading = change.Change == ScreenChange.Entered ? $"{Screen.Name}, " : "";
        announcer.Announce(new Announcement(heading + line, Interrupt: true));
    }

    private string? Title(int? number) =>
        number is { } tutorial && table()?.Entry(tutorial) is { } entry
            && messages.FindByLabelHash(entry.TitleHash) is { } message
            && cleaner.Clean(message.Text) is { Length: > 0 } text
            ? text
            : null;
}
