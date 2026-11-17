using Metaphor.Core.Announcements;

namespace Metaphor.Features.Dialogue;

/// <summary>A choice's options as they are spoken, from its message as the game stores it.</summary>
internal static class ChoiceOptions
{
    /// <summary>One option per line that doesn't clean to nothing, in order. An option of only dots or ellipses (which a
    /// screen reader reads as nothing) becomes "dot dot dot". None for a missing message (null).</summary>
    public static IReadOnlyList<string> From(string? message, TextCleaner cleaner) =>
        (message ?? "").Split('\n').Select(cleaner.Clean).Where(line => line.Length > 0)
            .Select(line => line.All(c => c is '.' or '…' or ' ') ? "dot dot dot" : line).ToList();
}
