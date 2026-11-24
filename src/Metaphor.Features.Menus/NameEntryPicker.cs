namespace Metaphor.Features.Menus;

/// <summary>A move on the name entry picker (#870), from the pad's D-pad and face buttons once it is wired.</summary>
public enum PickerMove
{
    /// <summary>The previous character in the group, wrapping round.</summary>
    Left,

    /// <summary>The next character in the group, wrapping round.</summary>
    Right,

    /// <summary>The previous group.</summary>
    Up,

    /// <summary>The next group.</summary>
    Down,

    /// <summary>Type the current character.</summary>
    Pick,

    /// <summary>Delete the last character typed.</summary>
    Delete,
}

/// <summary>What one move on the picker asks for: <paramref name="Speech"/> to say, or null; <paramref name="Typed"/>,
/// a character to hand the game's text box; <paramref name="Deletes"/>, one character to delete from it.</summary>
public readonly record struct PickerStep(string? Speech, char? Typed = null, bool Deletes = false);

/// <summary>The mod's own spoken character picker for name entry on a pad (#870): Steam's controller keyboard can't
/// be read by a screen reader. Left and right move through a group of characters, up and down change group, each
/// character said as it is reached; Pick types it and Delete removes the last one. The typed character itself is said
/// by <see cref="NameEntrySpeech"/> from the box, so a character the game refuses isn't said as typed. Keeps each
/// group's place. Holds no lock: its one caller, the pad read, is serial.</summary>
/// <remarks>Idea from Arcana (github.com/ChipsAhoiMcCoy/arcana, Zlib), release v0.6.0, tag commit 73db802, its
/// release notes read 2026-11-23; no code taken (CREDITS.md). Which characters the game's name box accepts beyond the
/// letters and digits is <c>[NEEDS VERIFICATION]</c> on a pad (docs/findings/name-entry-static.md: how a key becomes a
/// character is in <c>.text1</c>), so the symbols are a short guess the by-ear check confirms.</remarks>
public sealed class NameEntryPicker
{
    private static readonly Group[] Groups =
    [
        new("Capital letters", "ABCDEFGHIJKLMNOPQRSTUVWXYZ"),
        new("Small letters", "abcdefghijklmnopqrstuvwxyz"),
        new("Digits", "0123456789"),
        new("Symbols", " -.'"),
    ];

    private readonly int[] places = new int[Groups.Length];
    private int group;

    /// <summary>The character a Pick would type.</summary>
    public char Current => Groups[group].Characters[places[group]];

    /// <summary>The group's name and the current character, as said on arriving at the screen.</summary>
    public string Describe() => Groups[group].Name + ", " + Said(Current);

    /// <summary>Goes back to the first character of every group, in the first group, for a new visit.</summary>
    public void Reset()
    {
        Array.Clear(places);
        group = 0;
    }

    /// <summary>Makes <paramref name="move"/> and says what it asks for.</summary>
    public PickerStep Move(PickerMove move)
    {
        switch (move)
        {
            case PickerMove.Left:
                places[group] = Wrap(places[group] - 1, Groups[group].Characters.Length);
                return new(Said(Current));
            case PickerMove.Right:
                places[group] = Wrap(places[group] + 1, Groups[group].Characters.Length);
                return new(Said(Current));
            case PickerMove.Up:
                group = Wrap(group - 1, Groups.Length);
                return new(Describe());
            case PickerMove.Down:
                group = Wrap(group + 1, Groups.Length);
                return new(Describe());
            case PickerMove.Pick:
                return new(null, Typed: Current);
            case PickerMove.Delete:
                return new(null, Deletes: true);
            default:
                throw new ArgumentOutOfRangeException(nameof(move), move, null);
        }
    }

    private static int Wrap(int index, int count) => ((index % count) + count) % count;

    // A capital is said as such: a screen reader's pitch change for capitals isn't on for every reader. The rest go
    // through the name box's own way of saying a character, so the picker and the box say a symbol the same way.
    private static string Said(char character) =>
        char.IsUpper(character)
            ? "capital " + character
            : NameEntrySpeech.Change("", character.ToString()) ?? character.ToString();

    private sealed record Group(string Name, string Characters);
}
