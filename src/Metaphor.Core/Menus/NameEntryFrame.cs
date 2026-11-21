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

    /// <summary>What the box holds.</summary>
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));

    /// <summary>The frame as log fields: the text's length only.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"player={(PlayerName ? 1 : 0)} chars={Text.Length}");
}

/// <summary>Takes each update of a name entry screen (#585).</summary>
public interface INameEntryObserver
{
    void Observe(NameEntryFrame frame);
}
