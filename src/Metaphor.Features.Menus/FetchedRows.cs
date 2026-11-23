using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus;

/// <summary>What a settled cursor input on a list told: whether it starts a new visit (a manager not seen before),
/// whether the list's view changed to another tab or opening (a new set of rows not reached by a move), whether that
/// happened on the row the focus was already on (so the menu screens tell no focus move for it), and whether the rows
/// came with the tab names (<see cref="FetchedRows.Marked"/>).</summary>
internal sealed record FetchedView(bool NewVisit, bool Switched, bool SameRow, bool Tabbed, IReadOnlyList<int> Rows);

/// <summary>A list's rows on view, by the serials the game fetches their names by (#216, #736). The game fetches the
/// view's row names top first, all of them just before the settled cursor input of an opening, a tab switch or a move,
/// or only the new row when a step scrolls the view by one; the cursor's <see cref="CursorInput.Row"/> is the view's first
/// row and <see cref="CursorInput.Top"/> the focus's line in it (docs/raw-sources/menus-by-ear-session-message-log-2026-11-22.md,
/// docs/raw-sources/item-equipment-menus-message-log-2026-11-22.md). So a batch is the names fetched since the last
/// cursor input of any screen, and a settled input on the list takes it as the view from its first row. Safe to use from
/// several threads.</summary>
internal sealed class FetchedRows(string screenClass)
{
    private readonly Lock gate = new();
    private readonly List<int> batch = [];
    private List<int> visible = [];
    private bool batchOpen = true;
    private bool batchMarked;
    private long? manager;
    private int scroll;
    private int? lastIndex;
    private int? count;
    private bool afterMove;

    /// <summary>Adds a row name the game fetched to the current batch.</summary>
    public void Fetched(int serial)
    {
        lock (gate)
        {
            Start();
            if (batch.Count < ScreenEvent.LongestList && !batch.Contains(serial))
            {
                batch.Add(serial);
            }
        }
    }

    /// <summary>Marks the current batch as one that fetched the list's tab names.</summary>
    public void Marked()
    {
        lock (gate)
        {
            Start();
            batchMarked = true;
        }
    }

    /// <summary>Takes a cursor input of any screen: one of another screen, or a move, starts the next batch; a settled one
    /// on the list takes the batch as its view and says what changed, else null.</summary>
    public FetchedView? Cursor(CursorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock (gate)
        {
            var hadBatch = !batchOpen && (batch.Count > 0 || batchMarked);
            var marked = hadBatch && batchMarked;
            batchOpen = true;
            if (!string.Equals(input.ScreenClass, screenClass, StringComparison.Ordinal))
            {
                return null;
            }
            if (input.IsMove)
            {
                afterMove = true;
                return null;
            }
            var newVisit = manager != input.Manager;
            manager = input.Manager;
            var switched = false;
            if (hadBatch)
            {
                switched = newVisit || (!afterMove && (input.Count != count || !batch.SequenceEqual(visible)));
                visible = (switched ? null : Shifted(input.Row)) ?? [.. batch];
            }
            else if (newVisit)
            {
                visible = [];
            }
            var sameRow = switched && !newVisit && lastIndex == input.Index;
            scroll = input.Row;
            count = input.Count;
            lastIndex = input.Index;
            afterMove = false;
            return new FetchedView(newVisit, switched, sameRow, marked, [.. visible]);
        }
    }

    /// <summary>The serial of row <paramref name="index"/>'s name, or null when it isn't on view.</summary>
    public int? SerialAt(int index)
    {
        lock (gate)
        {
            var line = index - scroll;
            return line >= 0 && line < visible.Count ? visible[line] : null;
        }
    }

    private void Start()
    {
        if (batchOpen)
        {
            batch.Clear();
            batchMarked = false;
            batchOpen = false;
        }
    }

    // A one-name batch on a step that scrolled the view by one row is that row: the view keeps the rest.
    private List<int>? Shifted(int newScroll)
    {
        if (batch.Count != 1 || visible.Count < 2)
        {
            return null;
        }
        if (newScroll == scroll + 1)
        {
            return [.. visible.Skip(1), batch[0]];
        }
        return newScroll == scroll - 1 ? [batch[0], .. visible.SkipLast(1)] : null;
    }
}

/// <summary>Which tab of a tabbed list is showing (#216, #736): the tab its rows' names say
/// (<paramref name="tabOfRow"/>, null for a row that says none), else, for a tab whose rows say none (the equipment
/// tabs), the one tab next to the last that hasn't shown other rows first, as a switch moves one tab either way
/// (docs/raw-sources/menus-by-ear-session-message-log-2026-11-22.md). <paramref name="named"/> are the tabs the rows
/// can say. A tab first seen with no named tab next to it, or an empty one, isn't known.</summary>
internal sealed class ListTabs(int count, IReadOnlySet<int> named, Func<int, int?> tabOfRow)
{
    private readonly Dictionary<int, int> tabOfFirstRow = [];

    /// <summary>The tab last shown, or null when it isn't known.</summary>
    public int? Current { get; private set; }

    /// <summary>Forgets the tabs seen, as on a new visit, when the rows may have changed.</summary>
    public void Reset()
    {
        tabOfFirstRow.Clear();
        Current = null;
    }

    /// <summary>The tab showing <paramref name="rows"/> after an opening or a switch, or null when it isn't known.</summary>
    public int? Showing(IReadOnlyList<int> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var tab = rows.Select(tabOfRow).FirstOrDefault(found => found is not null);
        if (tab is null && rows.Count > 0)
        {
            if (tabOfFirstRow.TryGetValue(rows[0], out var seen))
            {
                tab = seen;
            }
            else if (Current is { } from)
            {
                var next = new[] { (from + 1) % count, (from + count - 1) % count }
                    .Distinct()
                    .Where(candidate => !named.Contains(candidate) && !tabOfFirstRow.ContainsValue(candidate))
                    .ToList();
                tab = next.Count == 1 ? next[0] : null;
            }
            if (tab is { } found)
            {
                tabOfFirstRow[rows[0]] = found;
            }
        }
        Current = tab;
        return tab;
    }
}
