namespace Metaphor.Core.Menus;

/// <summary>Where the grid cursor (<c>MultiLineCursorManager</c>, the cursor class of Key Configuration and the
/// calendar) sits, read from its own fields (#563): cells to a row, the column, the row within the view, the rows
/// scrolled, and a page and its stride. Its index is its class's own getter's, not the shared cursor's top plus row
/// (docs/raw-sources/grid-cursor-fields-static-2026-11-21.md, game build 548ddc95…, Steam build 18330018).</summary>
public sealed record GridCursor(int Columns, int Column, int Line, int Scroll, int Page, int PageStride)
{
    /// <summary>The grid cursor class's vtable as a module offset on game build 548ddc95…
    /// (docs/raw-sources/ui-focus-candidates-2026-11-16.md).</summary>
    public const long ClassRva = 0x22046a8;

    /// <summary>The most cells to a row taken as a grid; a wider one is a misread.</summary>
    public const int MostColumns = 64;

    /// <summary>The row from the top of the list, from 0.</summary>
    public int Row => Scroll + Line;

    /// <summary>The cell's index, as the class's getter (<c>0xe9c7d0</c>) returns it.</summary>
    public int Index => (Row * Columns) + (Page * PageStride) + Column;

    /// <summary>The grid at these fields, or null when they can't be one: no columns or more than
    /// <see cref="MostColumns"/>, a column outside the row, or a negative line, scroll, page or stride.</summary>
    public static GridCursor? Of(int columns, int column, int line, int scroll, int page, int pageStride) =>
        columns is >= 1 and <= MostColumns && column >= 0 && column < columns && line >= 0 && scroll >= 0 && page >= 0
            && pageStride >= 0
            ? new GridCursor(columns, column, line, scroll, page, pageStride)
            : null;
}
