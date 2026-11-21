namespace Metaphor.Features.Menus;

/// <summary>The rows of one settings page in cursor order (#560): <paramref name="Page"/> is the page's place on the
/// category list (0 System, 1 Sound, 2 Graphics, 3 Display), <paramref name="Total"/> the row count these rows are for,
/// and <paramref name="NameLabels"/> each row's name as a message label, or null for a row whose name isn't known. A page
/// showing another count isn't in this order: System has 20 rows from the title and 24 in play
/// (docs/findings/settings-screens-inventory-static.md).</summary>
public sealed record SettingsPageRows(int Page, int Total, IReadOnlyList<string?> NameLabels)
{
    /// <summary>The rows known on game build 548ddc95… (Steam build 18330018): only those pinned by a row-by-row walk,
    /// where each move resolves the focused row's name and then its help line
    /// (docs/raw-sources/settings-ok-and-rows-by-ear-2026-11-21.md). The order a page fetches its labels in as it is
    /// built doesn't give the rows' order (docs/raw-sources/settings-pages-resolves-by-ear-2026-11-20.md), so a row only
    /// seen that way is left unnamed and says its position. Whether these names are the text drawn on screen is
    /// <c>[NEEDS VERIFICATION]</c>.</summary>
    /// <remarks>Not yet pinned, fetch order only (<c>[NEEDS VERIFICATION]</c>), with O_ for System_Option_ and S_ for
    /// System_subject_name_PC_: in-play System rows 0–6 (O_14, O_16, O_17, O_18, O_19, O_20, O_103; rows 0–2 likely
    /// match the title page's); Sound S_29, S_30, S_31, O_93, S_28, S_32; Graphics O_26, S_35, S_36, S_63, S_60, S_39,
    /// S_40 (6 rows with V-Sync on); Display S_42, S_43, S_44, S_45.</remarks>
    public static IReadOnlyList<SettingsPageRows> Steam18330018 { get; } =
    [
        // System from the title: rows 0–6 by the up walk, 7–19 by both walks.
        new(Page: 0, Total: 20, NameLabels:
            ["System_Option_14", "System_Option_16", "System_Option_17", "System_Option_103", .. SharedSystemRows]),

        // System in play: rows 7–23 by the down walk, the title's rows 4–19 with System_Option_28 at row 18.
        new(Page: 0, Total: 24, NameLabels:
        [
            null, null, null, null, null, null, null, .. SharedSystemRows[..11], "System_Option_28",
            .. SharedSystemRows[11..],
        ]),
    ];

    // The System rows both pages show in the same order: the title's rows 4–19.
    private static string[] SharedSystemRows =>
    [
        "System_Option_89", "System_Option_21", "System_Option_22", "System_Option_71", "System_Option_27",
        "System_Option_23", "System_Option_112", "System_Option_72", "System_Option_24", "System_Option_73",
        "System_Option_25", "System_Option_91", "System_subject_name_PC_04", "System_subject_name_PC_05",
        "System_subject_name_PC_07", "System_subject_name_PC_64",
    ];

    /// <summary>The name label of the row at <paramref name="index"/>, or null when it isn't known.</summary>
    public string? NameLabel(int index) => index >= 0 && index < NameLabels.Count ? NameLabels[index] : null;
}
