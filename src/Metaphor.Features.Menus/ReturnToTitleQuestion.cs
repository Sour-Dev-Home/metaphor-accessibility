using Metaphor.Core.Menus;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Knows the question of the yes/no dialog the quick menu's Return to title row opens (#810): the dialog is
/// a <see cref="YesNoDialogFocus"/> one opened with no select window and no fetch of its text, so the row the focus was
/// on when it opened is all that tells it apart. The question is kept from the row's focus until the dialog opens, and
/// forgotten when the focus moves elsewhere first. Safe to call from several threads.</summary>
/// <remarks>On build 548ddc95 (Steam 18330018; 2026-11-23 session, mod 0c122d3): the dialog opened right after the quick
/// menu's focus on its last row, <c>System_EasyMenu_14</c>'s, with no resolve or select record between. Its label,
/// <c>System_Return_01</c> in <c>System_Message.msg</c>, is the one message there asking to return to the title screen
/// (the English file read from the game's archive with <c>CpkArchive</c>, 2026-11-23); that the dialog draws it is
/// <c>[NEEDS VERIFICATION]</c> by ear. The System menu's Return to Title row isn't covered: which dialog it opens is
/// unheard.</remarks>
[FeatureSetting<MenusFeature>(MenusFeature.ReturnToTitleQuestion)]
public sealed class ReturnToTitleQuestion : IScreenObserver, IYesNoQuestion
{
    /// <summary>The label of the dialog's question.</summary>
    public const string QuestionLabel = "System_Return_01";

    private readonly Func<bool> on;
    private readonly Lock gate = new();
    private string? question;

    /// <param name="on">Whether the question is given, read when the dialog opens.</param>
    public ReturnToTitleQuestion(Func<bool> on)
    {
        ArgumentNullException.ThrowIfNull(on);
        this.on = on;
    }

    /// <summary>Keeps the question while the quick menu's focus is on its last row; forgets it on any other focus but
    /// the dialog's opening.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Change == ScreenChange.Left
            || (change.Change == ScreenChange.Entered && change.Screen == YesNoDialogFocus.Screen))
        {
            return;
        }
        lock (gate)
        {
            question = change.Screen == QuickMenuFocus.Screen && IsLastRow(change) ? QuestionLabel : null;
        }
    }

    /// <inheritdoc/>
    public string? TakeQuestion()
    {
        string? label;
        lock (gate)
        {
            label = question;
            question = null;
        }
        return label is not null && on() ? label : null;
    }

    // Return to title is the last of the quick menu's 8 rows, or of 9 with the later first row (QuickMenuFocus).
    private static bool IsLastRow(ScreenEvent change) =>
        change.Total is 8 or 9 && change.Index == change.Total - 1;
}

/// <summary>The questions of several kinds of yes/no dialog: each is asked, so none keeps a question past the dialog,
/// and the first known one is given.</summary>
public sealed class YesNoQuestions(IReadOnlyList<IYesNoQuestion> sources) : IYesNoQuestion
{
    /// <inheritdoc/>
    public string? TakeQuestion() =>
        sources.Select(source => source.TakeQuestion()).ToList().FirstOrDefault(label => label is not null);
}
