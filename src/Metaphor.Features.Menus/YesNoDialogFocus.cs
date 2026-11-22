using System.Text.RegularExpressions;
using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Menus;

/// <summary>Knows the question of a yes/no dialog the game opens without a select window, such as the settings pages'
/// (#562).</summary>
public interface IYesNoQuestion
{
    /// <summary>The label of the message the yes/no dialog now opening asks, or null when it's unknown; given once.
    /// </summary>
    string? TakeQuestion();
}

/// <summary>Speaks a yes/no dialog (#549), such as the name-entry confirmation: on entering it and on each focus move,
/// "choice, n of 2", each cutting off the last. The choice's text is the line of the game's yes/no choice message at the
/// cursor's index, looked up at run time (<see cref="MessageLibrary"/>), or the position alone when it's missing. A
/// dialog the game opens outside its scripts' choices, such as the name entry's and the difficulty list's, says its
/// text first, once, in the same announcement as the first focus (#569), as does one whose question
/// <see cref="IYesNoQuestion"/> knows (#562). Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 the name-entry confirmation takes its choices from <c>System_Choice_YESNO</c>, Yes then
/// No, and index 0 accepted the name (docs/raw-sources/yes-no-dialog-labels-2026-11-20.md). The settings confirmations
/// (#562) were heard as "Yes" and "No" too (#562's comments, 2026-11-21); whether every dialog of this class uses that
/// message is <c>[NEEDS VERIFICATION]</c>. The name entry and
/// the difficulty list open theirs through the select window call with their prompt's label, and the scripts' choices
/// go through the same call (docs/raw-sources/yes-no-dialog-text-2026-11-20.md).</remarks>
public sealed partial class YesNoDialogFocus : IScreenObserver, IChoiceRequestObserver, ISelectRequestObserver
{
    /// <summary>A yes/no dialog: its choice's class on build 548ddc95, from its run-time type information
    /// (docs/raw-sources/screen-class-session-2026-11-17.md, yes-no-dialog-labels-2026-11-20.md). The game shows it
    /// outside the menus too, in conversations (docs/raw-sources/menus-open-session6-2026-11-17.md) and after New
    /// Game's difficulty list (docs/findings/difficulty-select-shared-cursor.md).</summary>
    public static Screen Screen { get; } = new("Yes or no", "YesNoChoiceSample@ope@ui@app", OutsideMenus: true);

    // The message whose lines are the choices in cursor order, and how many there are, on build 548ddc95
    // (docs/raw-sources/yes-no-dialog-labels-2026-11-20.md).
    private const string ChoicesLabel = "System_Choice_YESNO";
    private const int ChoiceCount = 2;

    // A line of the dialog's text that starts with this marks a note: the name confirmation's "*You will name the
    // protagonist later." is read "Note: You will name the protagonist later." (the maintainer's wording, #569,
    // 2026-11-20). Only the dialog texts read here are changed this way, not the game's other text.
    private const char NoteMarker = '*';
    private const string NoteWord = "Note: ";

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly IYesNoQuestion? questions;
    private readonly Lock gate = new();

    // The scripts' last choice, whose select window call comes next and whose prompt they already speak.
    private ChoiceRequested? scripted;

    // The label of the text the next yes/no dialog to open shows, until it opens or another screen does.
    private string? pendingText;

    // The next dialog opened from a scripts' choice, whose prompt is being spoken: its first focus waits its turn.
    private bool afterScriptsPrompt;

    public YesNoDialogFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
        : this(messages, announcer, cleaner, null)
    {
    }

    /// <summary>A focus that asks <paramref name="questions"/>, when given, for the question of a dialog opened without
    /// a select window.</summary>
    public YesNoDialogFocus(
        IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner, IYesNoQuestion? questions)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.questions = questions;
    }

    /// <summary>Notes a choice the game's scripts opened, so its select window isn't given the prompt again.</summary>
    public void Observe(ChoiceRequested choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        lock (gate)
        {
            scripted = choice;
        }
    }

    /// <summary>Keeps the prompt's label of a yes/no select window opened outside the scripts' choices, for when the
    /// dialog opens.</summary>
    public void Observe(SelectRequested window)
    {
        ArgumentNullException.ThrowIfNull(window);
        lock (gate)
        {
            var fromScripts = scripted is { } choice &&
                choice.PromptLabel == window.PromptLabel && choice.ChoiceLabel == window.ChoiceLabel;
            scripted = null;
            pendingText = !fromScripts && window.ChoiceLabel == ChoicesLabel ? window.PromptLabel : null;
            afterScriptsPrompt = fromScripts && window.ChoiceLabel == ChoicesLabel;
        }
    }

    /// <summary>Speaks the focus when a yes/no dialog is entered or its focus moves, after the dialog's text when it is
    /// entered and the text is known.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        string? textLabel = null;
        var waitsItsTurn = false;
        if (change.Change == ScreenChange.Entered)
        {
            lock (gate)
            {
                textLabel = change.Screen == Screen ? pendingText : null;
                waitsItsTurn = change.Screen == Screen && afterScriptsPrompt;
                pendingText = null;
                afterScriptsPrompt = false;
            }
            if (change.Screen == Screen && questions?.TakeQuestion() is { } question)
            {
                textLabel ??= question;
            }
        }
        var heading = textLabel is null ? null : Text(textLabel);
        var said = ListFocus.Of(
            change, Screen, heading, ChoiceCount, index => MessageLines.Line(messages, cleaner, ChoicesLabel, index));
        if (said is not null)
        {
            // After a scripts' choice spoke the prompt and "Choose: …" (DialogueChoices, #82), the first focus queues
            // behind them instead of cutting them off (the save prompt, heard 2026-11-21); later moves cut off as usual.
            announcer.Announce(waitsItsTurn ? said with { Interrupt = false } : said);
        }
    }

    // The message's lines cleaned and joined, a note line starting "Note:", or null when it has no text.
    private string? Text(string label)
    {
        var lines = (messages.Find(label)?.Text ?? "").Split('\n')
            .Select(cleaner.Clean)
            .Where(line => line.Length > 0)
            .Select(line => line[0] == NoteMarker ? NoteWord + line[1..].TrimStart() : line);
        var text = SpaceBeforePunctuation().Replace(string.Join(" ", lines), "$1");
        return text.Length > 0 ? text : null;
    }

    // A space left before punctuation where a dropped tag stood ("Is the name <V 0>?").
    [GeneratedRegex(@"\s+([?!.,])")]
    private static partial Regex SpaceBeforePunctuation();
}
