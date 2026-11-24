namespace Metaphor.Core.Menus;

// Issue #210: the hook layer reads the field this names, kept here so it is tested without the game.

/// <summary>Where a choice that serves more than one list with one cursor keeps which list is shown: the offset of a
/// 32-bit field in the choice object, which the hook layer reads with each cursor input
/// (<see cref="CursorInput.Mode"/>).</summary>
public static class ScreenModes
{
    /// <summary><c>TitleChoice</c> serves the title menu and its System submenu (Settings, Credits) with one cursor
    /// manager. Its field at <c>+0x4a8</c> is 1 on the title menu and 2 in the submenu, on game build 548ddc95…: the
    /// decide handler (<c>0xe2c620</c>) writes 2 at <c>0xe2cd46</c> before handing the submenu's list to the layout, and 1
    /// at <c>0xe2c8b9</c> before handing back the title's (docs/findings/title-names-voice-tutorials-static.md). Those
    /// values in a running game are <c>[NEEDS VERIFICATION]</c>: the by-ear check of #210.</summary>
    public const int TitleChoiceModeOffset = 0x4a8;

    /// <summary><c>BTL_ItemSelect</c>, battle's item list, serves its tabs with one cursor manager. Its field at
    /// <c>+0x44b8</c> is the tab shown, from 0, on game build 548ddc95…: its setup writes it and steps it modulo the tab
    /// count (docs/findings/battle-item-list-tab-index-static.md, #851). Its values in a running game are
    /// <c>[NEEDS VERIFICATION]</c>: the cursor records' <c>mode=</c> in a Debug session.</summary>
    public const int BattleItemTabOffset = 0x44b8;

    /// <summary>The offset of <paramref name="screenClass"/>'s mode field, or null when the class has none known.
    /// </summary>
    public static int? FieldOffset(string? screenClass) => screenClass switch
    {
        "TitleChoice@ope@ui@app" => TitleChoiceModeOffset,
        "BTL_ItemSelect@ope@ui@app" => BattleItemTabOffset,
        _ => null,
    };
}
