namespace Metaphor.Core.Menus;

/// <summary>A choice's fields that a Debug build logs when they change, from <paramref name="Offset"/> on, so a session
/// can check a static reading of them before a feature reads them (#806, #624, #219, #851).</summary>
public sealed record OwnerStateSpan(int Offset, int Length);

/// <summary>The choices whose fields a Debug build logs with each cursor input, as hex, only when they changed: each a
/// lead from static reading only, on game build 548ddc95…, that the next session's log checks. Numbers only, no game
/// text. The hook layer reads each span once per input through its memory reader; a Release build reads none.</summary>
public static class OwnerStates
{
    /// <summary>The longest span read, so no entry can make the hook read far.</summary>
    public const int MaxLength = 0x120;

    private static readonly OwnerStateSpan[] None = [];

    // The System tutorial list's row IDs (count +0x1480, IDs from +0x1488) and its categories (+0x17c0, count +0x17c8,
    // and +0x180c), #806 (docs/findings/battle-item-list-tab-index-static.md).
    private static readonly OwnerStateSpan[] Tutorials = [new(0x1478, 0x110), new(0x17c0, 0x50)];

    // The level-up point screen's member, level, five values, caps, flags and points (+0x3e0 to +0x494), #624
    // (docs/findings/level-up-point-distribution-choice-static.md).
    private static readonly OwnerStateSpan[] LevelUp = [new(0x3e0, 0xb8)];

    // Archetype details' tab kinds (+0x1700), tab index (+0x1738) and the three managers (+0x1740 to +0x1768), #219
    // (docs/findings/archetype-details-tabs-static.md).
    private static readonly OwnerStateSpan[] ArchetypeDetails = [new(0x1700, 0x70)];

    // Battle's item list's last item (+0x4440), tab list (+0x4450), tabs with items (+0x4498) and tab index (+0x44b8),
    // #851 (docs/findings/battle-item-list-tab-index-static.md).
    private static readonly OwnerStateSpan[] BattleItems = [new(0x4440, 0x80)];

    /// <summary>The spans to log for <paramref name="screenClass"/>, none for a class with none.</summary>
    public static IReadOnlyList<OwnerStateSpan> Of(string? screenClass) => screenClass switch
    {
        "SystemTutorialChoice@ope@ui@app" => Tutorials,
        "ResultDistributionPointChoice@ope@ui@app" => LevelUp,
        "EquipClassDetailChoice@ope@ui@app" or "StatusMenuClassDetailChoice@ope@ui@app"
            or "AcademiaClassDetailChoice@ope@ui@app" => ArchetypeDetails,
        "BTL_ItemSelect@ope@ui@app" => BattleItems,
        _ => None,
    };
}
