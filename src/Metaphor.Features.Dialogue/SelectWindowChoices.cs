using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue;

/// <summary>Tells its observers of a reply choice the game opens outside its scripts (#541) as a choice opened, so it is
/// spoken as a scripted one is (#82, #95). Such a choice reaches only the select window call
/// (docs/findings/reply-choice-routes-static.md); a scripted one reaches the scripts' choice hook just before, and isn't
/// told again. Only kind 0, the kind a reply choice was seen with (docs/raw-sources/select-window-reply-choice-2026-11-20.md),
/// and not the game's yes/no choice, which <c>YesNoDialogFocus</c> speaks (#569). Safe to call from several threads.
/// </summary>
public sealed class SelectWindowChoices : IChoiceRequestObserver, ISelectRequestObserver
{
    // The yes/no dialogs' choice message on build 548ddc95 (docs/raw-sources/yes-no-dialog-labels-2026-11-20.md).
    private const string YesNoLabel = "System_Choice_YESNO";

    // Tells each observer, one that throws never stopping the rest.
    private readonly MessageEvents replies = new();
    private readonly Lock gate = new();

    // The scripts' last choice, whose select window call comes next.
    private ChoiceRequested? scripted;

    /// <summary>Tells <paramref name="observers"/>, in order, of each choice opened outside the scripts.</summary>
    public SelectWindowChoices(params IChoiceRequestObserver[] observers)
    {
        ArgumentNullException.ThrowIfNull(observers);
        foreach (var observer in observers)
        {
            replies.ObserveChoices(observer);
        }
    }

    /// <summary>Notes a choice the game's scripts opened, which its observers already heard.</summary>
    public void Observe(ChoiceRequested choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        lock (gate)
        {
            scripted = choice;
        }
    }

    /// <summary>Tells the observers of a kind 0 reply window the scripts didn't open, as a choice with its labels.
    /// </summary>
    public void Observe(SelectRequested window)
    {
        ArgumentNullException.ThrowIfNull(window);
        bool fromScripts;
        lock (gate)
        {
            // The labels, not the kind: that the scripts pass their kind on unchanged isn't established.
            fromScripts = scripted is { } choice
                && choice.PromptLabel == window.PromptLabel && choice.ChoiceLabel == window.ChoiceLabel;
            scripted = null;
        }
        if (fromScripts || window.Kind != 0 || window.ChoiceLabel is null or YesNoLabel)
        {
            return;
        }
        replies.ChoiceOpened(new ChoiceRequested(window.Kind, window.PromptLabel, window.ChoiceLabel));
    }
}
