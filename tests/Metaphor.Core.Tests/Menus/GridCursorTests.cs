using Metaphor.Core.Menus;

namespace Metaphor.Core.Tests.Menus;

/// <summary>The grid cursor (<c>MultiLineCursorManager</c>, #563): its index is its own getter's, a row of cells plus
/// its column, not the shared cursor's top plus row; an input carrying one reports that index. Worked examples, not the
/// formula restated.</summary>
[Trait("Category", "Unit")]
public sealed class GridCursorTests
{
    [Fact]
    public void TheIndexCountsCellsRowByRow()
    {
        // Two cells to a row, scrolled one row down, on the view's third line, second column: row 3, cell 7.
        var grid = GridCursor.Of(columns: 2, column: 1, line: 2, scroll: 1, page: 0, pageStride: 0);

        Assert.NotNull(grid);
        Assert.Equal(3, grid.Row);
        Assert.Equal(7, grid.Index);
    }

    [Fact]
    public void APageAddsItsStride()
    {
        var grid = GridCursor.Of(columns: 2, column: 0, line: 1, scroll: 0, page: 1, pageStride: 24);

        Assert.Equal(26, grid?.Index);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    [InlineData(2, -1)]
    [InlineData(4096, 0)]
    public void ImplausibleFieldsAreNoGrid(int columns, int column)
    {
        Assert.Null(GridCursor.Of(columns, column, line: 0, scroll: 0, page: 0, pageStride: 0));
    }

    [Fact]
    public void ANegativeLineScrollOrPageIsNoGrid()
    {
        Assert.Null(GridCursor.Of(columns: 2, column: 0, line: -1, scroll: 0, page: 0, pageStride: 0));
        Assert.Null(GridCursor.Of(columns: 2, column: 0, line: 0, scroll: -1, page: 0, pageStride: 0));
        Assert.Null(GridCursor.Of(columns: 2, column: 0, line: 0, scroll: 0, page: -1, pageStride: 0));
    }

    [Fact]
    public void AnInputWithAGridReportsTheGridsIndex()
    {
        // The hook logs the grid's 0x60 as top and its scroll as row; their sum isn't the grid's index once it scrolls.
        var input = new CursorInput(0xa0, ClassRva: GridCursor.ClassRva, Command: 0x1, Result: 1, Top: 5, Row: 1,
            Flags: 0x46, Grid: GridCursor.Of(columns: 2, column: 1, line: 2, scroll: 1, page: 0, pageStride: 0));

        Assert.Equal(7, input.Index);
    }

    [Fact]
    public void AnInputWithoutAGridKeepsTopPlusRow()
    {
        var input = new CursorInput(0xa0, ClassRva: 0x100, Command: 0x1, Result: 1, Top: 5, Row: 1, Flags: 0x46);

        Assert.Equal(6, input.Index);
    }
}
