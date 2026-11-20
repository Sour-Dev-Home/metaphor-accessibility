using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus;

/// <summary>How a known list screen's focus is spoken (spec #211): on entering the screen, its heading and the focused
/// item, then on each focus move "item, n of total", each cutting off the last.</summary>
internal static class ListFocus
{
    /// <summary>What to say for <paramref name="change"/> on <paramref name="screen"/>, a list of
    /// <paramref name="count"/> items headed <paramref name="heading"/>, or with no heading when it's null;
    /// <paramref name="label"/> gives an item's text from its position, or null to speak the position alone. Null when
    /// the change is another screen's, a screen left, or a position outside the list.</summary>
    public static Announcement? Of(
        ScreenEvent change, Screen screen, string? heading, int count, Func<int, string?> label)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(label);
        if (change.Screen != screen || change.Change == ScreenChange.Left || change.Index < 0 || change.Index >= count)
        {
            return null;
        }
        var position = $"{change.Index + 1} of {count}";
        var item = label(change.Index) is { } text ? $"{text}, {position}" : position;
        var headed = change.Change == ScreenChange.Entered && heading is not null;
        return new Announcement(headed ? $"{heading}, {item}" : item, Interrupt: true);
    }
}
