using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;
using Metaphor.Features.Dialogue;
using Metaphor.Features.Menus;

namespace Metaphor.Hooks;

/// <summary>What hooks are handed through their constructor (#85, architecture rule 12): the entry point builds it once
/// speech has started and the dev log is open, and the loader passes it to each hook it creates.</summary>
/// <remarks>Not a positional record: its generated <c>Deconstruct</c> writes through <c>out</c> parameters, which
/// architecture rule 8 flags in the hook layer.</remarks>
internal sealed record HookServices
{
    /// <summary>Where the message hooks report what the game compiled and asked for (#86).</summary>
    public required MessageEvents Messages { get; init; }

    /// <summary>Where the menu cursor hook reports each input a menu's cursor handled (#94).</summary>
    public required CursorEvents Cursors { get; init; }

    /// <summary>#55's message log, for the listening hooks that remain: null in Release builds, or when the file
    /// couldn't be opened.</summary>
    public required MessageLog? Log { get; init; }

    /// <summary>The last dialogue lines (#126), for the repeat key (#58) and the backlog fallback (#114). Empty unless
    /// the dialogue feature is feeding it.</summary>
    public DialogueHistory DialogueHistory { get; init; } = new();

    /// <summary>The dialogue controls (#58): the window hooks tell them when the dialogue window opens and closes, and
    /// the input hooks hand them each read.</summary>
    public required DialogueControls DialogueControls { get; init; }

    /// <summary>Names each cursor manager's screen class (#208): the choice cursor getter hook feeds it, and the menu
    /// cursor hook asks it for each input.</summary>
    public ScreenClassLookup ScreenClasses { get; init; } = new();

    /// <summary>The mod's folder, outside the repository, where a development run leaves files for its hooks.</summary>
    public required string ModFolder { get; init; }

    /// <summary>Services for the mod in <paramref name="modFolder"/> whose requested dialogue lines and opened choices
    /// are spoken through <paramref name="announcer"/>, voiced lines as <paramref name="settings"/> say. The library
    /// hears each compiled file first, speech each requested line and choice first, and the menu screens (#208, which
    /// tell the title menu, #94) and a choice's focus (#95) each cursor input; the report log (#188) and the message log,
    /// when there are, hear every event last.</summary>
    public static HookServices Speaking(
        IAnnouncer announcer, SettingsReloader settings, ReportLog? report, MessageLog? log, string modFolder)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var library = new MessageLibrary();
        var history = new DialogueHistory();
        var events = new MessageEvents();
        var cursors = new CursorEvents();
        var cleaner = new TextCleaner(TextCleanerRules.None);
        // Lines go through the reader on/off switch; the repeat and the switch's own state are spoken either way.
        // The voice language is unknown until #103 finds it, so Auto speaks the full line.
        var reader = new DialogueReader(announcer);
        var dialogue = new DialogueAnnouncer(reader, cleaner, history, () => settings.Current.VoicedLines);
        var controls = new DialogueControls(DialogueBindings.Default, history, reader, announcer);
        var screens = new MenuScreens([TitleMenuFocus.Screen]);
        screens.Observe(new TitleMenuFocus(library, announcer, cleaner));
        events.ObserveFiles(library);
        events.ObserveLines(new DialogueLines(library, dialogue));
        events.ObserveChoices(new DialogueChoices(library, dialogue));
        var choiceFocus = new ChoiceFocus(library, announcer, cleaner);
        events.ObserveChoices(choiceFocus);
        events.ObserveLines(choiceFocus);
        cursors.Observe(screens);
        cursors.Observe(choiceFocus);
        if (report is not null)
        {
            events.ObserveFiles(report);
            events.ObserveLines(report);
            events.ObserveChoices(report);
            cursors.Observe(report);
        }
        if (log is not null)
        {
            events.ObserveFiles(log);
            events.ObserveLines(log);
            events.ObserveChoices(log);
            cursors.Observe(log);
        }
        return new()
        {
            Messages = events,
            Cursors = cursors,
            Log = log,
            ModFolder = modFolder,
            DialogueHistory = history,
            DialogueControls = controls,
        };
    }
}
