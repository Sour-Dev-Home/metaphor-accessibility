namespace Metaphor.Features.Menus;

/// <summary>One of Key Configuration's sub-tabs (#589): its name as a message label, and the action each grid cell binds,
/// as a message label, in cell order (the grid's index). The grid's count tells the sub-tab, so a sub-tab whose count
/// another shares can't be told and isn't listed.</summary>
public sealed record KeyConfigTab(string NameLabel, IReadOnlyList<string> ActionLabels)
{
    private const string Action = "System_Keybind_name_PC_";

    /// <summary>The sub-tabs known on game build 548ddc95… (Steam build 18330018), from the session of 2026-11-21
    /// (docs/raw-sources/key-config-actions-session-2026-11-21.md): each row's actions are pinned by the labels that
    /// resolve as it scrolls into view, and a whole view's labels resolve in row order. That a row's two actions resolve
    /// left column first is <c>[NEEDS VERIFICATION]</c> (by eye: that Common's top-left cell is <c>_PC_32</c>'s).
    /// Every visit, on whichever device tab, showed the same actions for each count. Field and Dungeon both have 14
    /// cells, so neither is listed. Which device tab (Keyboard, Mouse, Controller) is shown isn't read
    /// (<c>[NEEDS VERIFICATION]</c>).</summary>
    public static IReadOnlyList<KeyConfigTab> Steam18330018 { get; } =
    [
        new("System_mid_subject_name_key_PC_01", Actions(
            32, 33, 35, 34, 61, 62, 64, 63, 86, 87, 89, 88, 19, 18, 91, 90, 29, 30, 83, 58, 20, 25, 28)),
        new("System_mid_subject_name_key_PC_02", Actions(37, 38, 36)),
        new("System_mid_subject_name_key_PC_21", Actions(5, 4, 3, 2, 7, 1, 6, 16, 15, 10, 9, 8)),
    ];

    /// <summary>The sub-tab of <paramref name="tabs"/> with <paramref name="count"/> cells, or null when none has or the
    /// count is unread.</summary>
    public static KeyConfigTab? Of(IReadOnlyList<KeyConfigTab> tabs, int? count)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        return count is { } cells ? tabs.FirstOrDefault(tab => tab.ActionLabels.Count == cells) : null;
    }

    /// <summary>The action label of the cell at <paramref name="index"/>, or null outside the sub-tab.</summary>
    public string? ActionLabel(int index) => index >= 0 && index < ActionLabels.Count ? ActionLabels[index] : null;

    private static string[] Actions(params int[] numbers) => [.. numbers.Select(number => $"{Action}{number:D2}")];
}
