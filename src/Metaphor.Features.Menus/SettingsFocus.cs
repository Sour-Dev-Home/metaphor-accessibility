using System.Globalization;
using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Settings;

namespace Metaphor.Features.Menus;

/// <summary>Speaks the Settings category list and names its pages (#559, parent #96): on opening the list, "Settings"
/// and the focused category, then each category's text from the game's message file and "n of 5"; on opening the System, Sound, Graphics or Display page, that page's
/// category text and the focused row, then on each move the row: its name when <see cref="SettingsPageRows"/> knows it,
/// then its value where <see cref="SettingsPageRows"/> knows its setting, then its position (#560, #96), or "OK, button"
/// on the button after the last row, and when the focused row's value changes, only the new value; on Key Configuration, its category text
/// on opening, then each grid cell's action where <see cref="KeyConfigTab"/> knows it, its column and row (#563), the
/// sub-tab's name when it changes, and "OK, button" on its OK button (#589). Each
/// announcement cuts off the last. The text is looked up at run time (<see cref="MessageLibrary"/>), so nothing here is
/// game content. Safe to call from several threads.</summary>
/// <remarks>Labels, classes and which row opens which page on build 548ddc95:
/// docs/raw-sources/settings-category-labels-session1-2026-11-20.md.</remarks>
[FeatureSetting(Feature.Menus)]
public sealed class SettingsFocus : IScreenObserver
{
    // The category list, opened from the title or the field's quick menu, and from the main menu in play: one list.
    private static readonly Screen List = new("Settings", "?$ConfigTopChoicePc@VConfigTopChoicePcLayoutInit@ui@app@@@ope@ui@app");
    private static readonly Screen ListInPlay = new("Settings", "?$ConfigTopChoicePc@VConfigTopChoicePcLayout@ui@app@@@ope@ui@app");

    // The pages the list's first four rows open, in row order.
    private static readonly Screen[] Pages =
    [
        new("System settings", SettingsPages.Classes[0]),
        new("Sound settings", SettingsPages.Classes[1]),
        new("Graphics settings", SettingsPages.Classes[2]),
        new("Display settings", SettingsPages.Classes[3]),
    ];

    // The list's rows in cursor order, and the message labels that name them; a page is named by its row's label.
    private const int CategoryCount = 5;
    private const string CategoryLabel = "System_category_name_PC_0";

    // The list's name, said when it opens. The game draws no heading from its message files there, so the word is ours,
    // English only: the game's own word for the item that opens the list. Approved by the maintainer on 2026-11-20 (#559).
    private const string ListName = "Settings";

    // The button after a page's last row, which closes the page; the cursor is off the list there (ScreenEvent.OffList,
    // docs/raw-sources/settings-ok-and-rows-by-ear-2026-11-21.md). The word is ours, English only, as for ListName:
    // which message names it (System_Option_88 is a candidate) is [NEEDS VERIFICATION].
    private const string OkButton = "OK, button";

    // Key Configuration, the list's fifth row, on the grid cursor (#563): docs/findings/settings-screens-inventory-static.md.
    private static readonly Screen KeyConfig = new("Key Configuration", SettingsPages.KeyConfigClass);
    private const int KeyConfigCategory = 4;

    /// <summary>The category list (both its classes), the four pages and Key Configuration, for the menu-screens
    /// module.</summary>
    public static IReadOnlyList<Screen> Screens { get; } = [List, ListInPlay, .. Pages, KeyConfig];

    private readonly IMessageLookup messages;
    private readonly IAnnouncer announcer;
    private readonly TextCleaner cleaner;
    private readonly IReadOnlyList<SettingsPageRows> rows;
    private readonly IReadOnlyList<KeyConfigTab> tabs;

    // Key Configuration's count at its last change, so a sub-tab is said when it changes (#589).
    private readonly Lock gate = new();
    private int? keyConfigCount;

    // The focused row's value as last said, so a change says only a new value (#96).
    private int? lastValue;

    // A changed value waits until it has been still for this long, then is said once: a held key repeats about 100 ms
    // apart and would otherwise say every step (#735, the maintainer's decision of 2026-11-22). Any other event drops it.
    private static readonly TimeSpan SettleWait = TimeSpan.FromMilliseconds(200);
    private readonly ITimer settle;
    private string? pendingValue;

    /// <summary>Speaks the pages' rows and Key Configuration's actions known on game build 548ddc95
    /// (<see cref="SettingsPageRows.Steam18330018"/>, <see cref="KeyConfigTab.Steam18330018"/>).</summary>
    public SettingsFocus(IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner)
        : this(messages, announcer, cleaner, SettingsPageRows.Steam18330018)
    {
    }

    /// <summary>Speaks the pages' rows as <paramref name="rows"/> names them; any other row by its position.</summary>
    public SettingsFocus(
        IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner, IReadOnlyList<SettingsPageRows> rows)
        : this(messages, announcer, cleaner, rows, KeyConfigTab.Steam18330018)
    {
    }

    /// <summary>Speaks the pages' rows as <paramref name="rows"/> names them and Key Configuration's cells as
    /// <paramref name="tabs"/> names them; any other row or cell by its position.</summary>
    public SettingsFocus(
        IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner, IReadOnlyList<SettingsPageRows> rows,
        IReadOnlyList<KeyConfigTab> tabs)
        : this(messages, announcer, cleaner, rows, tabs, TimeProvider.System)
    {
    }

    /// <summary>As above, with <paramref name="clock"/> timing the wait for a changed value to be still (#735).</summary>
    public SettingsFocus(
        IMessageLookup messages, IAnnouncer announcer, TextCleaner cleaner, IReadOnlyList<SettingsPageRows> rows,
        IReadOnlyList<KeyConfigTab> tabs, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(tabs);
        this.messages = messages;
        this.announcer = announcer;
        this.cleaner = cleaner;
        this.rows = rows;
        this.tabs = tabs;
        settle = clock.CreateTimer(_ => SaySettled(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Speaks the focus when the list or a page is entered or its focus moves.</summary>
    public void Observe(ScreenEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Change != ScreenChange.ValueChanged)
        {
            lock (gate)
            {
                pendingValue = null;
            }
        }
        if (change.Change == ScreenChange.Left || change.Index < 0 || Said(change) is not { } text)
        {
            return;
        }
        announcer.Announce(new Announcement(text, Interrupt: true));
    }

    private string? Said(ScreenEvent change)
    {
        if (change.Screen == List || change.Screen == ListInPlay)
        {
            if (change.Index >= CategoryCount)
            {
                return null;
            }
            var item = Join(CategoryText(change.Index), $"{change.Index + 1} of {CategoryCount}");
            return change.Change == ScreenChange.Entered ? $"{ListName}, {item}" : item;
        }
        if (change.Screen == KeyConfig)
        {
            var said = KeyConfigFocus(change);
            return change.Change == ScreenChange.Entered ? Join(CategoryText(KeyConfigCategory), said) : said;
        }
        var page = Array.IndexOf(Pages, change.Screen);
        if (page < 0)
        {
            return null;
        }
        if (change.Change == ScreenChange.ValueChanged)
        {
            return change.OffList ? null : ChangedValue(page, change);
        }
        var row = change.OffList ? OkButton : Row(page, change);
        return change.Change == ScreenChange.Entered ? Join(CategoryText(page), row) : row;
    }

    // Key Configuration's focus (#589): "OK, button" on its OK button, else the cell's action when its sub-tab is known
    // and the cell's position; the sub-tab's name first on opening and when the grid's count changes, as it does with
    // the sub-tab. The sub-tab is told by the count (KeyConfigTab), so only on the grid.
    private string KeyConfigFocus(ScreenEvent change)
    {
        bool tabChanged;
        lock (gate)
        {
            tabChanged = change.Change == ScreenChange.Entered || change.Count != keyConfigCount;
            keyConfigCount = change.Count;
        }
        if (change.OffList)
        {
            return OkButton;
        }
        var tab = change.Grid is null ? null : KeyConfigTab.Of(tabs, change.Count);
        var cell = Join(Text(tab?.ActionLabel(change.Index)), Cell(change));
        return tabChanged ? Join(Text(tab?.NameLabel), cell) : cell;
    }

    // "<name>, <value>, n of total" (#96), each part left out when it isn't known.
    private string Row(int page, ScreenEvent change)
    {
        var position = change.Total is { } total ? $"{change.Index + 1} of {total}" : $"{change.Index + 1}";
        var pageRows = PageRows(page, change.Total);
        lock (gate)
        {
            lastValue = Value(pageRows, change);
        }
        return Join(RowText(pageRows, change.Index), Join(ValueText(pageRows, change.Index, lastValue), position));
    }

    // Only the focused row's new value (#96's "on change"), said once it has been still (#735), so always null here; a
    // change to another setting's value, or none, leaves the row's.
    private string? ChangedValue(int page, ScreenEvent change)
    {
        var value = Value(PageRows(page, change.Total), change);
        lock (gate)
        {
            if (value is null || value == lastValue)
            {
                return null;
            }
            lastValue = value;
            pendingValue = ValueText(PageRows(page, change.Total), change.Index, value);
        }
        settle.Change(SettleWait, Timeout.InfiniteTimeSpan);
        return null;
    }

    private void SaySettled()
    {
        string? value;
        lock (gate)
        {
            value = pendingValue;
            pendingValue = null;
        }
        if (value is not null)
        {
            announcer.Announce(new Announcement(value, Interrupt: true));
        }
    }

    // The page's rows at this count, or null when they aren't known.
    private SettingsPageRows? PageRows(int page, int? total) =>
        total is { } count ? rows.FirstOrDefault(known => known.Page == page && known.Total == count) : null;

    // The value as said (#734): the row's choice name where the page names the row's choices, else the number; null when
    // there is no value. A name is the game's message text, or the table's fixed text, and the number when neither is
    // found (docs/findings/settings-value-names.md).
    private string? ValueText(SettingsPageRows? pageRows, int index, int? value)
    {
        if (value is not { } number)
        {
            return null;
        }
        if (pageRows?.Choice(index, number) is { } choice)
        {
            if (choice.Label is { } label && messages.Find(label) is { } message
                && cleaner.Clean(message.Text) is { Length: > 0 } text)
            {
                return text;
            }
            if (choice.Text is { Length: > 0 } fixedText)
            {
                return fixedText;
            }
        }
        return number.ToString(CultureInfo.InvariantCulture);
    }

    // The focused row's value as a number, or null when its setting ID or the page's values aren't known.
    private static int? Value(SettingsPageRows? pageRows, ScreenEvent change) =>
        pageRows?.ValueId(change.Index) is { } id ? change.Values?.Value(id) : null;

    // The row's name, cleaned, or null when the page's rows aren't known, the row's label isn't, or its message is
    // missing or empty.
    private string? RowText(SettingsPageRows? pageRows, int index) =>
        pageRows?.NameLabel(index) is { } label && messages.Find(label) is { } message
            && cleaner.Clean(message.Text) is { Length: > 0 } text
            ? text
            : null;

    // A Key Configuration cell by its column and its row from the top of the list, with the rows' total when the
    // grid's count is known and it is on the first page (how a later page's cells number against the count isn't
    // known). The words are ours, English only, approved by the maintainer on 2026-11-21 until the game's own column
    // headings are pinned; each cell is one action, two to a row
    // (docs/raw-sources/key-config-actions-session-2026-11-21.md). Without the grid's fields, its position as any
    // page's row.
    private static string Cell(ScreenEvent change)
    {
        if (change.Grid is not { } grid)
        {
            return change.Total is { } total ? $"{change.Index + 1} of {total}" : $"{change.Index + 1}";
        }
        var row = $"row {grid.Row + 1}";
        if (grid.Page == 0 && change.Total is { } cells)
        {
            row += $" of {(cells + grid.Columns - 1) / grid.Columns}";
        }
        return $"column {grid.Column + 1}, {row}";
    }

    private static string Join(string? name, string position) => name is null ? position : $"{name}, {position}";

    // The message's text, cleaned, or null when the label is null or its message is missing or empty.
    private string? Text(string? label) =>
        label is not null && messages.Find(label) is { } message && cleaner.Clean(message.Text) is { Length: > 0 } text
            ? text
            : null;

    // The category's text, cleaned, or null when the message is missing or empty.
    private string? CategoryText(int index) =>
        messages.Find(CategoryLabel + (index + 1)) is { } message && cleaner.Clean(message.Text) is { Length: > 0 } text
            ? text
            : null;
}
