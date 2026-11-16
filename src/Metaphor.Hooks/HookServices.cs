using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;
using Metaphor.Features.Dialogue;

namespace Metaphor.Hooks;

/// <summary>What hooks are handed through their constructor (#85, architecture rule 12): the entry point builds it once
/// speech has started and the dev log is open, and the loader passes it to each hook it creates.</summary>
/// <remarks>Not a positional record: its generated <c>Deconstruct</c> writes through <c>out</c> parameters, which
/// architecture rule 8 flags in the hook layer.</remarks>
internal sealed record HookServices
{
    /// <summary>Every message the game has compiled since the mod loaded.</summary>
    public required MessageLibrary Messages { get; init; }

    /// <summary>Where requested dialogue lines are spoken.</summary>
    public required DialogueLines Lines { get; init; }

    /// <summary>#55's message log: null in Release builds, or when the file couldn't be opened.</summary>
    public required MessageLog? Log { get; init; }

    /// <summary>Services whose dialogue lines are spoken through <paramref name="announcer"/>.</summary>
    public static HookServices Speaking(IAnnouncer announcer, MessageLog? log)
    {
        var messages = new MessageLibrary();
        return new()
        {
            Messages = messages,
            Lines = new DialogueLines(messages, new DialogueAnnouncer(announcer, new TextCleaner(TextCleanerRules.None))),
            Log = log,
        };
    }
}
