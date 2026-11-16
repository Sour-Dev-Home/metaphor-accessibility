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
    /// <summary>Where the message hooks report what the game compiled and asked for (#86).</summary>
    public required MessageEvents Messages { get; init; }

    /// <summary>#55's message log, for the listening hooks that remain: null in Release builds, or when the file
    /// couldn't be opened.</summary>
    public required MessageLog? Log { get; init; }

    /// <summary>The mod's folder, outside the repository, where a development run leaves files for its hooks.</summary>
    public required string ModFolder { get; init; }

    /// <summary>Services for the mod in <paramref name="modFolder"/> whose requested dialogue lines and opened choices
    /// are spoken through <paramref name="announcer"/>. The library hears each compiled file first, and speech each
    /// requested line and choice first; the log, when there is one, hears all three last.</summary>
    public static HookServices Speaking(IAnnouncer announcer, MessageLog? log, string modFolder)
    {
        var library = new MessageLibrary();
        var events = new MessageEvents();
        var dialogue = new DialogueAnnouncer(announcer, new TextCleaner(TextCleanerRules.None));
        events.ObserveFiles(library);
        events.ObserveLines(new DialogueLines(library, dialogue));
        events.ObserveChoices(new DialogueChoices(library, dialogue));
        if (log is not null)
        {
            events.ObserveFiles(log);
            events.ObserveLines(log);
            events.ObserveChoices(log);
        }
        return new() { Messages = events, Log = log, ModFolder = modFolder };
    }
}
