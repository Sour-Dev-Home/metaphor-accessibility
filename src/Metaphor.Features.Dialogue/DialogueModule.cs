using Metaphor.Core.Features;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Dialogue;

/// <summary>The dialogue project's wiring and settings (#784, ADR 0022): lines and choices (#58, #95, #541), the
/// dialogue controls (#58), and the game-over screen's words (#623). It registers after the menus' and the battle's, so
/// its controls take what theirs leave, and it reads the menus' <see cref="PopupOnScreen"/>.</summary>
public sealed class DialogueModule : IFeatureModule
{
    public IEnumerable<Enum> Settings => Enum.GetValues<DialogueFeature>().Select(feature => (Enum)feature);

    public void Register(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = context.Settings;
        var reader = context.Reader;
        var library = context.Library;
        var cleaner = context.Cleaner;
        var names = context.Names;
        var history = context.History;
        var events = context.Events;
        // Lines go through the reader on/off switch; the repeat and the switch's own state are spoken either way.
        // Auto follows the game's voice language, read on each line (#538); unknown speaks the full line.
        // The window's lines, choices and repeat stop when the window closes (#676), while still the last thing said.
        var windowSpeech = context.Stoppable.Source();
        var dialogueSpeech = settings.Speech(DialogueFeature.Dialogue, reader);
        var dialogue = new DialogueAnnouncer(
            windowSpeech.Through(dialogueSpeech), cleaner, history, () => settings.Current.VoicedLines,
            context.VoiceLanguage, () => settings.Current.IsOn(DialogueFeature.ChoiceCount),
            choiceDelayOn: () => settings.Current.IsOn(DialogueFeature.ChoiceDelay));
        // The game-over screen's words follow its voiced line once the window closes (#623); they go through the
        // dialogue's setting like the line before them.
        var gameOver = new GameOverScreen(new ShownSpeakers(library, names), dialogue, dialogueSpeech, context.LastDevice);
        // The bindings are the settings file's (#661), worked out again only when it changes. While a pop-up is up,
        // the repeat binding says its page again; the menus' module, which registers first, provides it.
        var dialogueBindings = settings.Live(DialogueBindings.From);
        var popup = context.Find<PopupOnScreen>();
        var controls = new DialogueControls(
            dialogueBindings, history, reader, windowSpeech.Through(context.Announcer),
            () =>
            {
                dialogue.EndConversation();
                gameOver.WindowClosed();
            },
            windowSpeech, popup is null ? null : () => popup.Said,
            () => settings.Current.IsOn(DialogueFeature.WindowCloseStop));
        // Lines and choices name each speaker as the screen shows it (#81).
        var (lines, choices) = DialogueSpeech.Shown(library, names, dialogue);
        events.ObserveLines(lines);
        events.ObserveShownLines(lines);
        events.ObserveShownLines(gameOver);
        // A choice's prompt, spoken as it opens, isn't spoken again when the window shows it (#541).
        events.ObserveChoices(lines);
        events.ObserveChoices(choices);
        // A choice's count waits for its first option, when the options are on screen (#814).
        var choiceFocus = new ChoiceFocus(
            library, settings.Speech(DialogueFeature.Dialogue, context.Announcer), cleaner, dialogue.Release)
        {
            SayPositions = () => settings.Current.IsOn(SpokenDetail.Positions),
        };
        events.ObserveChoices(choiceFocus);
        events.ObserveLines(choiceFocus);
        // A reply choice opened outside the scripts reaches only the select window call (#541).
        var selectChoices = new SelectWindowChoices(lines, choices, choiceFocus);
        events.ObserveChoices(selectChoices);
        events.ObserveSelects(selectChoices);
        // After the above, which holds the options of a choice only the window reports, so the window call can say a
        // choice with no options (#744, #814).
        events.ObserveSelects(choices);
        context.Cursors.Observe(choiceFocus);
        // The input frame (#301) hands the controls each read and, with it, the dialogue window's message pointer
        // (#232).
        context.AddControls(settings.Input(DialogueFeature.Dialogue, controls));
        context.AddKeys(DialogueKeys.All);
    }
}
