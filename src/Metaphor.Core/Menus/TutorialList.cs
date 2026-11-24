namespace Metaphor.Core.Menus;

/// <summary>The System &gt; Tutorials list's rows (#806): its choice keeps the tutorial number of each row of the tab
/// shown, a 32-bit count at <c>+0x1480</c> and 32-bit numbers from <c>+0x1488</c>, row 0 first. Read from a Debug
/// build's owner-state log in the by-ear session of 2026-11-24 (mod 2fdf9f63, game build 548ddc95…, Steam build
/// 18330018; issue #806's comment of that day): count 33 matched the cursor's, rows 0 to 11 held 11, 12, 13, 14, 17,
/// 18, 19, 20, 22, 23, 24, 25, and rows 8 to 10 opened tutorials 22 to 24 in the 2026-11-23 session. Each tab change
/// rewrote the list with its own count, leaving the previous tab's numbers past it. The static lead is
/// docs/findings/battle-item-list-tab-index-static.md.</summary>
public static class TutorialList
{
    /// <summary>The list's choice, by its run-time type name.</summary>
    public const string ScreenClass = "SystemTutorialChoice@ope@ui@app";

    /// <summary>The rows' count, from the choice.</summary>
    public const int CountOffset = 0x1480;

    /// <summary>The first row's tutorial number, from the choice; each next row 4 bytes on.</summary>
    public const int NumbersOffset = 0x1488;

    /// <summary>The most rows read: the game has 206 tutorials, and its longest tab seen held 33.</summary>
    public const int MaxRows = 256;

    /// <summary>Where row <paramref name="row"/>'s number is, from the choice, when the list's
    /// <paramref name="count"/> holds it; null when the count is unknown or out of range, or the row past it, so a stale
    /// number from another tab is never read.</summary>
    public static int? NumberOffset(int row, int? count) =>
        count is { } rows && rows > 0 && rows <= MaxRows && row >= 0 && row < rows ? NumbersOffset + row * 4 : null;
}
