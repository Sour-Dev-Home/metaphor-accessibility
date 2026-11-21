using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue;

/// <summary>Speaks the focus as the player moves through a reply choice (#95). The first menu cursor whose input the game
/// handles after a choice opens is taken as the choice's (decided in #95 that choices use the shared cursor; which
/// cursor the game uses is <c>[NEEDS VERIFICATION]</c>, docs/raw-sources/spike-session-2-2026-11-16.md). Each option it
/// settles on is said as "option, n of total": the first after "Choose: …" (#82) finishes, each later one cutting off
/// the last. Options are the choice message's lines, as #82 reads them. The choice ends at the next dialogue line.
/// Safe to call from several threads.</summary>
public sealed class ChoiceFocus : IChoiceRequestObserver, ILineRequestObserver, ICursorObserver
{
    // The yes/no dialog's screen class on build 548ddc95, as the menus' YesNoDialogFocus names it
    // (docs/findings/difficulty-select-shared-cursor.md).
    private const string YesNoDialogClass = "YesNoChoiceSample@ope@ui@app";

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Lock gate = new();
    private IReadOnlyList<string> options = [];
    private long? choiceCursor;
    private int? lastIndex;

    public ChoiceFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>A choice opened: the next cursor to become active is its own. One whose options have no message is not
    /// followed.</summary>
    public void Observe(ChoiceRequested choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var text = choice.ChoiceLabel is { } label ? messages.Find(label)?.Text : null;
        var found = ChoiceOptions.From(text, cleaner);
        lock (gate)
        {
            options = found;
            choiceCursor = null;
            lastIndex = null;
        }
    }

    /// <summary>A dialogue line follows the choice, so it has closed.</summary>
    public void Observe(LineRequested line)
    {
        ArgumentNullException.ThrowIfNull(line);
        lock (gate)
        {
            options = [];
            choiceCursor = null;
            lastIndex = null;
        }
    }

    /// <summary>Speaks the option where the choice's cursor settled, when that changed. A move is skipped: the game can
    /// refuse one and put the cursor back on its next call.</summary>
    public void Observe(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.IsMove)
        {
            return;
        }
        if (input.ScreenClass == YesNoDialogClass)
        {
            // A scripted yes/no (the save prompt) shows the yes/no dialog, whose focus the menus speak (#569): leave
            // it to them, and close the choice so no later cursor is taken for it.
            lock (gate)
            {
                options = [];
                choiceCursor = null;
                lastIndex = null;
            }
            return;
        }
        bool first;
        string option;
        int total;
        lock (gate)
        {
            total = options.Count;
            if (input.Index < 0 || input.Index >= total
                || (choiceCursor is { } cursor && cursor != input.Manager) || lastIndex == input.Index)
            {
                return;
            }
            first = choiceCursor is null;
            choiceCursor = input.Manager;
            lastIndex = input.Index;
            option = options[input.Index];
        }
        announcer.Announce(new Announcement($"{option}, {input.Index + 1} of {total}", Interrupt: !first));
    }
}
