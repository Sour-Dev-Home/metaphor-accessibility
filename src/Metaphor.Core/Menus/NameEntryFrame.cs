using System.Globalization;

namespace Metaphor.Core.Menus;

/// <summary>One update of a name entry screen while the player types (#585): the screen's object, whether it asks for the
/// player's own name rather than the protagonist's (its <c>isPlayerNameEntry</c> flag), and the text in its box. Layout
/// and build: docs/findings/name-entry-static.md. The text is the player's typed name, personal data: it goes to the
/// screen reader only, and <see cref="ToString"/> gives its length, never the text, so a log can't hold it.</summary>
public sealed class NameEntryFrame(long entry, bool playerName, string text)
{
    /// <summary>The name entry screen's object, telling one visit from the next.</summary>
    public long Entry { get; } = entry;

    /// <summary>Whether the screen asks for the player's name; false for the protagonist's.</summary>
    public bool PlayerName { get; } = playerName;

    /// <summary>What the box holds: the text given, up to its first zero character. The game's array always
    /// counts 10 characters and pads the name with zeros (the dev log of 2026-11-21 on #593: <c>chars=10</c> throughout
    /// typing and deleting), so a deletion turns a character into a zero rather than shortening the array.</summary>
    public string Text { get; } = UpToZero(text ?? throw new ArgumentNullException(nameof(text)));

    private static string UpToZero(string text) => text.IndexOf('\0', StringComparison.Ordinal) is var end and >= 0
        ? text[..end]
        : text;

    /// <summary>The frame as log fields: the text's length only.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"player={(PlayerName ? 1 : 0)} chars={Text.Length}");
}

/// <summary>Takes each update of a name entry screen (#585).</summary>
public interface INameEntryObserver
{
    void Observe(NameEntryFrame frame);
}
