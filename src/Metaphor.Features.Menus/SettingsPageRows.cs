namespace Metaphor.Features.Menus;

/// <summary>The rows of one settings page in cursor order (#560): <paramref name="Page"/> is the page's place on the
/// category list (0 System, 1 Sound, 2 Graphics, 3 Display), <paramref name="Total"/> the row count these rows are for,
/// and <paramref name="NameLabels"/> each row's name as a message label, or null for a row whose name isn't known. A
/// page showing another count isn't in this order: System has 20 rows from the title and 24 in play
/// (docs/findings/settings-screens-inventory-static.md).</summary>
public sealed record SettingsPageRows(int Page, int Total, IReadOnlyList<string?> NameLabels)
{
    /// <summary>Each row's setting ID in the page's values (<see cref="Core.Menus.SettingValues"/>, #96), or null for a
    /// row whose value isn't known; empty when none was given.</summary>
    public IReadOnlyList<int?> ValueIds { get; init; } = [];

    /// <summary>Each row's choices in value order (value 0 first), or null for a row whose values are said as numbers
    /// (#734); empty when none was given. A value outside its row's choices is said as its number.</summary>
    public IReadOnlyList<IReadOnlyList<ValueName>?> ValueChoices { get; init; } = [];

    /// <summary>The rows known on game build 548ddc95… (Steam build 18330018): only those pinned by a row-by-row walk,
    /// where each move resolves the focused row's name and then its help line
    /// (docs/raw-sources/settings-ok-and-rows-by-ear-2026-11-21.md). The order a page fetches its labels in as it is
    /// built doesn't give the rows' order (docs/raw-sources/settings-pages-resolves-by-ear-2026-11-20.md), so a row only
    /// seen that way is left unnamed and says its position. Whether these names are the text drawn on screen is
    /// <c>[NEEDS VERIFICATION]</c>. A row's value ID is pinned by the one dword that moved as the row was changed
    /// (docs/raw-sources/settings-values-sessions-2026-11-21.md); every other row says no value.</summary>
    /// <remarks>Not yet pinned, fetch order only (<c>[NEEDS VERIFICATION]</c>), with O_ for System_Option_ and S_ for
    /// System_subject_name_PC_: in-play System rows 0–6 (O_14, O_16, O_17, O_18, O_19, O_20, O_103; rows 0–2 likely
    /// match the title page's); Sound S_29, S_30, S_31, O_93, S_28, S_32; Graphics O_26, S_35, S_36, S_63, S_60, S_39,
    /// S_40 (6 rows with V-Sync on); Display S_42, S_43, S_44, S_45.</remarks>
    public static IReadOnlyList<SettingsPageRows> Steam18330018 { get; } =
    [
        // System from the title: rows 0–6 by the up walk, 7–19 by both walks.
        new(Page: 0, Total: 20, NameLabels:
            ["System_Option_14", "System_Option_16", "System_Option_17", "System_Option_103", .. SharedSystemRows])
        {
            ValueIds = [null, null, null, null, .. SharedSystemValues],
            ValueChoices = [OnOff, OnOff, OnOff, OnOff, .. SharedSystemChoices],
        },

        // System in play: rows 7–23 by the down walk, the title's rows 4–19 with System_Option_28 at row 18.
        new(Page: 0, Total: 24, NameLabels:
        [
            null, null, null, null, null, null, null, .. SharedSystemRows[..11], "System_Option_28",
            .. SharedSystemRows[11..],
        ])
        {
            ValueIds =
            [
                null, null, null, null, null, null, null, .. SharedSystemValues[..11], null, .. SharedSystemValues[11..],
            ],
            ValueChoices =
            [
                null, null, null, null, null, null, null, .. SharedSystemChoices[..11], null, .. SharedSystemChoices[11..],
            ],
        },
    ];

    // The System rows both pages show in the same order: the title's rows 4–19.
    private static string[] SharedSystemRows =>
    [
        "System_Option_89", "System_Option_21", "System_Option_22", "System_Option_71", "System_Option_27",
        "System_Option_23", "System_Option_112", "System_Option_72", "System_Option_24", "System_Option_73",
        "System_Option_25", "System_Option_91", "System_subject_name_PC_04", "System_subject_name_PC_05",
        "System_subject_name_PC_07", "System_subject_name_PC_64",
    ];

    // Those rows' setting IDs where pinned: Message Speed (O_72) 17, Skip Selections (O_24) 18, Mouse Sensitivity
    // (S_04) 27 and Controller Sensitivity (S_05) 28.
    private static int?[] SharedSystemValues =>
        [null, null, null, null, null, null, null, 17, 18, null, null, null, 27, 28, null, null];

    // Those rows' choices, in the same order as SharedSystemValues. Message Speed 0, 1, 2 are Normal, Fast, Display All,
    // the messages System_Option_82, _83, _84 that resolve together with the page (the log of the 2026-11-22 session,
    // build 548ddc95); Skip Selections 0 is Off and 1 is On, with no message found for them. Both orders were confirmed
    // by the maintainer on 2026-11-22 (issue #734, comment 7083706421; docs/findings/settings-value-names.md).
    // The other rows' words are the maintainer's, by ear on 2026-11-22 (issue #770's latest comments): the on/off rows
    // (Toggle Autolock, both Invert Camera rows, Camera Assist, Camera Shake, Dash Effects, Skip Special Animations and
    // Network Functions; on the title page also Vibration, Auto Advance, Anime Subtitles and Confirmations, the rows
    // whose learned values were only 0 and 1 that session) say Off and On, and Button Display Auto and Type 1 to 4. That
    // 0 is Off on every on/off row, as on Skip Selections, and that Button Display's values run in that order, are
    // [NEEDS VERIFICATION] by ear. Camera Speed, Language Selection, both sensitivities and Dash keep their numbers.
    private static IReadOnlyList<ValueName>?[] SharedSystemChoices =>
    [
        OnOff, OnOff, OnOff, OnOff, null, OnOff, OnOff,
        [new("System_Option_82"), new("System_Option_83"), new("System_Option_84")],
        OnOff, OnOff, OnOff, null, null, null,
        [new(null, "Auto"), new(null, "Type 1"), new(null, "Type 2"), new(null, "Type 3"), new(null, "Type 4")],
        null,
    ];

    private static IReadOnlyList<ValueName> OnOff => [new(null, "Off"), new(null, "On")];

    /// <summary>The name label of the row at <paramref name="index"/>, or null when it isn't known.</summary>
    public string? NameLabel(int index) => index >= 0 && index < NameLabels.Count ? NameLabels[index] : null;

    /// <summary>The setting ID of the row at <paramref name="index"/>, or null when it isn't known.</summary>
    public int? ValueId(int index) => index >= 0 && index < ValueIds.Count ? ValueIds[index] : null;

    /// <summary>The choice the row at <paramref name="index"/> names for <paramref name="value"/>, or null when the row
    /// has no named choices or the value is outside them.</summary>
    public ValueName? Choice(int index, int value) =>
        index >= 0 && index < ValueChoices.Count && ValueChoices[index] is { } choices && value >= 0
            && value < choices.Count
            ? choices[value]
            : null;
}

/// <summary>One choice's name (#734): a message <paramref name="Label"/> the game's own text is read from, or fixed
/// <paramref name="Text"/> where no message is known. The label wins; the text is the fallback.</summary>
public sealed record ValueName(string? Label, string? Text = null);
