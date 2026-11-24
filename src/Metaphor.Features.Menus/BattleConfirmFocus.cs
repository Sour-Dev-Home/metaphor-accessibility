using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks battle's confirm prompts (#262, #257), the yes/no choices on the shared cursor that said only "1 of
/// 2": on opening, the Retry prompt's question, then its first choice, and on each move "Yes, 1 of 2" or "No, 2 of 2",
/// each cutting off the last (ADR 0023). The question is the game's own <c>CommandBattle_HelpText_05</c>, found by its
/// label in the message library; the choices are <c>System_Choice_YESNO</c>'s lines in cursor order. A prompt whose
/// question isn't known says "Confirm" in the mod's own word, as the prompt may be the pass or the Auto Battle one.
/// Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 (Steam build 18330018) the RTTI names <c>BTL_RewindComfirm</c> (the Retry prompt) and
/// <c>BTL_Comfirm</c> (the battle's other yes/no prompts), both yes/no choices whose slot 11 is the shared cursor getter
/// (docs/findings/battle-escape-pass-retry-signals-logged.md). <c>CommandBattle_HelpText_05</c> is the retry text, <c>_07</c>
/// the pass and <c>_08</c> the auto-battle one by their labels' order there; which of them <c>BTL_Comfirm</c> shows is
/// <c>[NEEDS VERIFICATION]</c> (by ear), so it says "Confirm" for now. That these choices read Yes then No like the other
/// yes/no dialogs is <c>[NEEDS VERIFICATION]</c> (by ear).</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.BattleConfirms)]
public sealed class BattleConfirmFocus : IScreenObserver
{
    /// <summary>The Retry prompt, by its owner's class on build 548ddc95.</summary>
    public static Screen Retry { get; } = new("Retry", "BTL_RewindComfirm@ope@ui@app", OutsideMenus: true);

    /// <summary>The battle's other confirm prompt, by its owner's class on build 548ddc95.</summary>
    public static Screen Confirm { get; } = new("Confirm", "BTL_Comfirm@ope@ui@app", OutsideMenus: true);

    /// <summary>Both prompts, for the known screens.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [Retry, Confirm];

    private const string RetryQuestionLabel = "CommandBattle_HelpText_05";
    private const string ChoicesLabel = "System_Choice_YESNO";
    private const int ChoiceCount = 2;

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;

    public BattleConfirmFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Speaks the focus when either prompt is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != Retry && change.Screen != Confirm)
        {
            return;
        }
        var heading = change.Screen == Retry ? Question() ?? Confirm.Name : Confirm.Name;
        var said = ListFocus.Of(
            change, change.Screen, heading, ChoiceCount, index => MessageLines.Line(messages, cleaner, ChoicesLabel, index));
        if (said is not null)
        {
            announcer.Announce(said);
        }
    }

    // The retry question's lines cleaned and joined, or null when the message is missing or empty.
    private string? Question()
    {
        var lines = (messages.Find(RetryQuestionLabel)?.Text ?? "").Split('\n')
            .Select(cleaner.Clean).Where(line => line.Length > 0);
        var text = string.Join(" ", lines);
        return text.Length > 0 ? text : null;
    }
}
