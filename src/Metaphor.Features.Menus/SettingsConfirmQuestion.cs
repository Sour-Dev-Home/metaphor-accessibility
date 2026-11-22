using Metaphor.Core.Input;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus;

/// <summary>Knows which question a settings page's yes/no dialog asks (#562), from the press that opened it: B on a
/// page, or A on its OK button, opens the save-changes question; Start opens the Restore Defaults one. Both dialogs are
/// alike in every cursor record, so the press is all that tells them apart. A press is a button going down on a pad
/// read the game made (#225); the question is kept until the dialog opens, and forgotten when the focus moves or
/// another screen opens first, as when B leaves a page with nothing changed. Safe to call from several threads.
/// </summary>
/// <remarks>On build 548ddc95 (docs/raw-sources/settings-confirm-dialogs-session-2026-11-21.md): every dialog that opened
/// on a page followed one of those presses, B and A-on-OK going on to the category list and Start back to the page. The
/// labels are the system file's <c>System_Option_63</c> and <c>System_Option_64</c>; that the dialogs draw them is
/// <c>[NEEDS VERIFICATION]</c> (by ear). Keyboard presses aren't watched: which keys open the dialogs is
/// <c>[NEEDS VERIFICATION]</c>. A dialog opened from a screen inside a page, such as the brightness screen's, gets no
/// question: what it asks is unknown. On the Key Configuration page Start opens its Restore Defaults dialog too, given
/// the same label; whether that dialog draws <c>System_Option_64</c> or the key configuration's own
/// <c>System_message_PC_19</c> is <c>[NEEDS VERIFICATION]</c>
/// (docs/raw-sources/key-config-restore-defaults-session-2026-11-21.md).</remarks>
public sealed class SettingsConfirmQuestion : IScreenObserver, IPadWatcher, IYesNoQuestion
{
    // The game's pad mask bits on build 548ddc95 (docs/raw-sources/pad-and-keyboard-state-2026-11-16.md).
    private const int AButton = 0x4000;
    private const int BButton = 0x2000;
    private const int StartButton = 0x0008;

    // The Key Configuration page's class on build 548ddc95
    // (docs/raw-sources/settings-category-labels-session1-2026-11-20.md). Only Start opens a question there (docs/raw-sources/key-config-restore-defaults-session-2026-11-21.md). PR #684
    // names it as SettingsPages.KeyConfigClass; use that once it merges.
    private const string KeyConfigClass = "ConfigMenuChoiceKeyConfig@ope@ui@app";

    private const string SaveLabel = "System_Option_63";
    private const string DefaultsLabel = "System_Option_64";

    private readonly Lock gate = new();
    private readonly Dictionary<int, int> held = [];

    // The focus while a page holds it, else null.
    private ScreenEvent? page;

    // The label of the question the next yes/no dialog asks.
    private string? question;

    /// <summary>Follows the focus: on a page or not, and forgets the question on any change but the dialog's opening
    /// and the page being left for it.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (gate)
        {
            if (change.Change == ScreenChange.Left)
            {
                page = null;
                return;
            }
            if (change.Change == ScreenChange.Entered && change.Screen == YesNoDialogFocus.Screen)
            {
                page = null;
                return;
            }
            question = null;
            page = SettingsPages.IsPage(change.ScreenClass) || change.ScreenClass == KeyConfigClass ? change : null;
        }
    }

    /// <summary>Takes one pad read and keeps the question its press on a page opens.</summary>
    public void Pad(int pad, int buttons, bool made)
    {
        if (!made)
        {
            return;
        }
        lock (gate)
        {
            var pressed = buttons & ~held.GetValueOrDefault(pad);
            held[pad] = buttons;
            if (pressed == 0)
            {
                return;
            }
            question = page is null ? null
                : page.ScreenClass == KeyConfigClass ? ((pressed & StartButton) != 0 ? DefaultsLabel : null)
                : (pressed & BButton) != 0 || ((pressed & AButton) != 0 && page.OffList) ? SaveLabel
                : (pressed & StartButton) != 0 ? DefaultsLabel
                : null;
        }
    }

    /// <inheritdoc/>
    public string? TakeQuestion()
    {
        lock (gate)
        {
            var label = question;
            question = null;
            return label;
        }
    }
}
