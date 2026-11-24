using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the quest list (#194): each row's name and position ("Mad Mischief, 1 of 4"), the name the list's view
/// fetched by serial for its place in the view (<see cref="FetchedRows"/>); and an opened quest's name and description on
/// opening, the two the game fetches from the quest files just before its cursor. Off, or a row whose name has no known
/// text, says the position alone, as before. Not spoken, <c>[NEEDS VERIFICATION]</c>: a row's state, deadline and rank,
/// whose labels the list fetches once per view, not per row. Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 (Steam build 18330018): the list, <c>CampQuestChoice</c>, fetches every row's name from
/// <see cref="MessageSerials.QuestNames"/> with flags 9, top first, before each settled cursor input; the opened quest,
/// <c>CampQuestDetailChoice</c>, fetches its name with flags 10 and one of its texts from
/// <see cref="MessageSerials.QuestTexts"/> with flags 7 (docs/findings/quest-message-serials-static.md).</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.QuestList)]
public sealed class QuestFocus : IScreenObserver, ISerialResolveObserver, ICursorObserver
{
    /// <summary>The flags the list fetches its row names with.</summary>
    public const int RowNameFlags = 9;

    /// <summary>The flags an opened quest fetches its name with.</summary>
    public const int TitleFlags = 10;

    /// <summary>The flags an opened quest fetches its description with.</summary>
    public const int DescriptionFlags = 7;

    /// <summary>The quest list, by its owner's class on build 548ddc95.</summary>
    public static Screen List { get; } = new("Quests", "CampQuestChoice@ope@ui@app");

    /// <summary>An opened quest, by its owner's class on build 548ddc95. Its cursor's count (9 in session 2026-11-22
    /// "c") isn't the quest's, so its moves are left silent.</summary>
    public static Screen Detail { get; } = new("Quest detail", "CampQuestDetailChoice@ope@ui@app");

    /// <summary>The quest screens.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [List, Detail];

    private readonly MessageSerials serials;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Func<bool> on;
    private readonly FetchedRows rows = new(List.ScreenClass);
    private readonly Lock gate = new();
    private int? title;
    private int? description;

    /// <param name="serials">The game's messages by serial.</param>
    /// <param name="announcer">Where the lines are said: the menus' speech.</param>
    /// <param name="cleaner">Cleans a message's text for speech.</param>
    /// <param name="on">Whether names and descriptions are said, read at each line.</param>
    public QuestFocus(MessageSerials serials, IAnnouncer announcer, TextCleaner cleaner, Func<bool> on)
    {
        ArgumentNullException.ThrowIfNull(serials);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(on);
        this.serials = serials;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.on = on;
    }

    /// <summary>Speaks a row on entering the list or moving its focus, and an opened quest on opening.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Change == ScreenChange.Left || change.Index < 0)
        {
            return;
        }
        if (change.Screen == List)
        {
            var name = on() && rows.SerialAt(change.Index) is { } serial ? serials.TextOf(serial, cleaner) : null;
            announcer.Announce(new Announcement(change.Positioned(name), Interrupt: true));
        }
        else if (change.Screen == Detail && change.Change == ScreenChange.Entered)
        {
            int? named;
            int? described;
            lock (gate)
            {
                (named, described) = (title, description);
                (title, description) = (null, null);
            }
            if (!on())
            {
                return;
            }
            string?[] parts = [Text(named), Text(described)];
            if (parts.OfType<string>().ToList() is { Count: > 0 } said)
            {
                announcer.Announce(new Announcement(string.Join(" ", said.Select(Sentence)), Interrupt: true));
            }
        }
    }

    /// <summary>Takes the list's row names and an opened quest's name and description.</summary>
    public void Observe(SerialResolved resolve)
    {
        if (serials.InFile(resolve.Serial, MessageSerials.QuestNames))
        {
            if (resolve.Flags == RowNameFlags)
            {
                rows.Fetched(resolve.Serial);
            }
            else if (resolve.Flags == TitleFlags)
            {
                lock (gate)
                {
                    title = resolve.Serial;
                }
            }
        }
        else if (resolve.Flags == DescriptionFlags && serials.InFile(resolve.Serial, MessageSerials.QuestTexts))
        {
            lock (gate)
            {
                description = resolve.Serial;
            }
        }
    }

    /// <summary>Takes the list's view from each settled cursor input, before the menu screens tell it; any other
    /// screen's input but the opened quest's forgets a name and description fetched before it.</summary>
    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        rows.Cursor(input);
        if (!string.Equals(input.ScreenClass, Detail.ScreenClass, StringComparison.Ordinal))
        {
            lock (gate)
            {
                (title, description) = (null, null);
            }
        }
    }

    private string? Text(int? serial) => serial is { } known ? serials.TextOf(known, cleaner) : null;

    // A name or description ends a sentence before the next.
    private static string Sentence(string text) => text[^1] is '.' or '?' or '!' ? text : text + ".";
}
