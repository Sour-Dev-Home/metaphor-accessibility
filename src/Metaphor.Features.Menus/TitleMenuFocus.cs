using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the title menu (#94, #208): on entering the screen, "Main menu" and the focused item, then on each
/// focus move "item, n of total", each cutting off the last. The screen is recognised by its screen class
/// (<see cref="Screen"/>), so the title's message file compiling again during play changes nothing. Item text is looked
/// up at run time (<see cref="MessageLibrary"/>); only the label, the count and the third item's word are ours
/// (docs/raw-sources/title-menu-labels-2026-11-16.md, docs/raw-sources/title-menu-item-images-2026-11-20.md). Safe to
/// call from several threads. Its System submenu, served by the same choice, is spoken as its own two items (#210). While
/// the screen waits for a button before showing the menu, only its prompt is spoken (#555).
/// </summary>
[FeatureSetting<MenusFeature>(MenusFeature.Menus)]
public sealed class TitleMenuFocus : IScreenObserver
{
    /// <summary>The title menu: its choice's class on build 548ddc95, from its run-time type information
    /// (docs/raw-sources/screen-class-session-2026-11-17.md).</summary>
    public static Screen Screen { get; } = new("Title menu", "TitleChoice@ope@ui@app");

    // The message whose lines are the title menu's items in cursor order, and how many there are, on build 548ddc95
    // (docs/raw-sources/title-menu-labels-2026-11-16.md).
    private const string ItemsLabel = "Title_System_08";
    private const int ItemCount = 3;

    // The screen draws the items from images, not the message. The third image says "System" (it opens the Settings and
    // Credits submenu) while the message's third line says Settings, so that item is our own word, English only
    // (#537, docs/raw-sources/title-menu-item-images-2026-11-20.md).
    private const int SystemIndex = 2;
    private const string SystemWord = "System";

    // The System submenu is the same choice and cursor, told apart by the choice's mode (2 in the submenu,
    // Core.Menus.ScreenModes, #210). Its two items are images too, SETTINGS and CREDITS in the texture, and the code fills
    // them with no message, so their words are ours, English only (docs/raw-sources/title-menu-item-images-2026-11-20.md,
    // docs/findings/title-names-voice-tutorials-static.md).
    private const int SystemMenuMode = 2;
    private static readonly string[] SystemMenuItems = ["Settings", "Credits"];

    // Before a button is pressed the choice's mode is 0 and the screen shows only "Press any button": the decide handler
    // (0xe2c620) shows the items and sets 1, and back on the title menu sets 0 again. The prompt is the message
    // Title_PressAnyKey's text, ours when it is missing (#555, docs/raw-sources/title-press-any-button-mode-2026-11-20.md).
    private const int PressAnyButtonMode = 0;
    private const string PromptLabel = "Title_PressAnyKey";
    private const string PromptWords = "Press any button";

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly Lock gate = new();
    private bool promptShown;

    public TitleMenuFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
    }

    /// <summary>Speaks the focus when the title menu is entered or its focus moves, and the prompt while the screen waits
    /// for a button before showing the menu; the menu's first focus after that prompt is spoken as an entry.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Screen != Screen)
        {
            return;
        }
        bool wasPrompt;
        var isPrompt = change.Change != ScreenChange.Left && change.Mode == PressAnyButtonMode;
        lock (gate)
        {
            wasPrompt = promptShown;
            promptShown = isPrompt;
        }
        if (isPrompt)
        {
            if (!wasPrompt)
            {
                announcer.Announce(new Announcement(PromptText(), Interrupt: true));
            }
            return;
        }
        if (wasPrompt && change.Change == ScreenChange.FocusMoved)
        {
            change = change with { Change = ScreenChange.Entered };
        }

        // The System submenu has no heading: the maintainer chose "Settings, 1 of 2" alone, on entry and on return (#210).
        var said = change.Mode == SystemMenuMode
            ? ListFocus.Of(change, Screen, heading: null, SystemMenuItems.Length, index => SystemMenuItems[index])
            : ListFocus.Of(change, Screen, "Main menu", ItemCount, ItemText);
        if (said is not null)
        {
            announcer.Announce(said);
        }
    }

    // The item's text: ours for the third, else the message's line, or null when the message or line is missing.
    private string? ItemText(int index)
    {
        return index == SystemIndex ? SystemWord : MessageLines.Line(messages, cleaner, ItemsLabel, index);
    }

    // The prompt's message text, its lines joined, or our words when the message is missing or empty.
    private string PromptText()
    {
        var text = string.Join(' ', (messages.Find(PromptLabel)?.Text ?? "").Split('\n')
            .Select(cleaner.Clean).Where(line => line.Length > 0));
        return text.Length > 0 ? text : PromptWords;
    }
}
