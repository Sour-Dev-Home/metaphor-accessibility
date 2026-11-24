using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>The dialogue project's keys, for the help key and the settings menu's key list (#868, #754). Every binding
/// <see cref="DialogueBindings.From"/> reads is here, with its default from <see cref="DialogueBindings.Default"/>; a
/// test fails when one isn't.</summary>
public static class DialogueKeys
{
    private static readonly DialogueBindings Defaults = DialogueBindings.Default;
    private static readonly DialogueKeyBindings Context = DialogueKeyBindings.Default;

    /// <summary>Every key the project lists, all in dialogue: the repeat and reader keys behind the dialogue's setting,
    /// the context keys behind <see cref="DialogueFeature.DialogueKeys"/>.</summary>
    public static IReadOnlyList<KeyEntry> All { get; } =
    [
        new(KeyPlaces.Dialogue, DialogueFeature.Dialogue, new(
            "Repeat the last line",
            new("dialogue", "repeatKey", Defaults.RepeatKey), new("dialogue", "repeatButtons", Defaults.RepeatButtons))),
        new(KeyPlaces.Dialogue, DialogueFeature.Dialogue, new(
            "Turn the reader on or off",
            new("dialogue", "readerKey", Defaults.ReaderKey), new("dialogue", "readerButtons", Defaults.ReaderButtons))),
        // The context keys (#879), behind their own setting; DialogueKeyBindings.From reads each.
        new(KeyPlaces.Dialogue, DialogueFeature.DialogueKeys, new(
            "Stop the speech",
            new("dialogue", "stopKey", Context.StopKey), new("dialogue", "stopButtons", Context.StopButtons))),
        new(KeyPlaces.Dialogue, DialogueFeature.DialogueKeys, new(
            "Say who is speaking",
            new("dialogue", "speakerKey", Context.SpeakerKey),
            new("dialogue", "speakerButtons", Context.SpeakerButtons))),
        new(KeyPlaces.Dialogue, DialogueFeature.DialogueKeys, new(
            "Say the line on screen",
            new("dialogue", "lineKey", Context.LineKey), new("dialogue", "lineButtons", Context.LineButtons))),
    ];
}
