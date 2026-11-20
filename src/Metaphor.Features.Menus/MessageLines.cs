using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus;

/// <summary>A message whose lines are a list's items, in cursor order, as the title menu's and the yes/no choice's are.
/// </summary>
internal static class MessageLines
{
    /// <summary>The cleaned text of line <paramref name="index"/> of the message labelled <paramref name="label"/>, empty
    /// lines skipped, or null when the message or the line is missing.</summary>
    public static string? Line(IMessageLookup messages, TextCleaner cleaner, string label, int index)
    {
        var lines = (messages.Find(label)?.Text ?? "").Split('\n')
            .Select(cleaner.Clean).Where(line => line.Length > 0).ToList();
        return index >= 0 && index < lines.Count ? lines[index] : null;
    }
}
