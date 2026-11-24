using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>The dialogue project's keys, for the help key and the settings menu's key list (#868, #754). Every binding
/// <see cref="DialogueBindings.From"/> reads is here, with its default from <see cref="DialogueBindings.Default"/>; a
/// test fails when one isn't.</summary>
public static class DialogueKeys
{
    private static readonly DialogueBindings Defaults = DialogueBindings.Default;

    /// <summary>Every key the project lists, all in dialogue, behind the dialogue's setting.</summary>
    public static IReadOnlyList<KeyEntry> All { get; } =
    [
        new(KeyPlaces.Dialogue, DialogueFeature.Dialogue, new(
            "Repeat the last line",
            new("dialogue", "repeatKey", Defaults.RepeatKey), new("dialogue", "repeatButtons", Defaults.RepeatButtons))),
        new(KeyPlaces.Dialogue, DialogueFeature.Dialogue, new(
            "Turn the reader on or off",
            new("dialogue", "readerKey", Defaults.ReaderKey), new("dialogue", "readerButtons", Defaults.ReaderButtons))),
    ];
}
