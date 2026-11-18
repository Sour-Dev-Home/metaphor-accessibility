using System.Diagnostics;
using Metaphor.Core;
using Metaphor.Core.Battle;

namespace Metaphor.Hooks;

// Battle events (#251, spec #247): one hook on the battle package's per-frame update, in .ecode, hands each frame to
// HookServices.Battle; the battle-state reader below reads the package through ModuleMemoryReader, never writing or
// calling anything. Function, offsets and phases: docs/raw-sources/battle-state-leads-2026-11-17.md and
// docs/raw-sources/battle-state-session6-2026-11-17.md (build 548ddc95). The development kit's listener on the same
// function (capture-kit-2) becomes an observer of these frames if it comes to main (architecture rule 13).

/// <summary>The battle package's per-frame update, <c>void (Package* this, float seconds)</c>, called once a frame
/// while a command battle runs. Observed before the original runs; each call hands the package to the battle
/// events.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class BattlePackageUpdateHook : IGameHook, IObserves2WithDouble
{
    private readonly BattleEvents battle;

    public BattlePackageUpdateHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        battle = services.Battle;
    }

    public string SignatureName => "BtlPackageUpdate";

    public void Observe(nint first, double second) => battle.Frame(first);
}

/// <summary>Reads the battle package the update hook reports (<see cref="IBattleState"/>): the phase by its class name,
/// read once per phase class and remembered, and the package's enemy count and money. Each frame costs two pointer
/// reads, plus one for the enemy count until the battle has started.</summary>
internal sealed class GameBattleState : IBattleState
{
    // Package fields: the current phase (the object of the smart pointer at +0x48), the enemies still standing and the
    // money paid out.
    private const int PhaseOffset = 0x60;
    private const int EnemyCountOffset = 0x140;
    private const int MoneyOffset = 0x3E4;

    // More phase classes than a battle has (21); a full cache starts afresh rather than grow.
    private const int PhasesRemembered = 64;

    private readonly long moduleBase;
    private readonly long moduleSize;
    private readonly Dictionary<long, BattlePhase> phaseByVtable = [];

    public GameBattleState()
    {
        using var process = Process.GetCurrentProcess();
        // Without the main module every phase is Unknown: battles still start, and never end.
        moduleBase = process.MainModule?.BaseAddress ?? 0;
        moduleSize = process.MainModule?.ModuleMemorySize ?? 0;
    }

    public BattlePhase Phase(long package)
    {
        if (ModuleMemoryReader.ReadPointer((nint)package + PhaseOffset) is not { } phase || phase == 0 ||
            ModuleMemoryReader.ReadPointer(phase) is not { } vtable)
        {
            return BattlePhase.Unknown;
        }
        if (phaseByVtable.TryGetValue(vtable, out var known))
        {
            return known;
        }
        var named = BattlePhases.FromClassName(RttiName.Of(
            phase,
            moduleBase,
            moduleSize,
            address => ModuleMemoryReader.ReadPointer((nint)address),
            address => ModuleMemoryReader.ReadInt32((nint)address),
            address => ModuleMemoryReader.ReadAnsiString((nint)address, 256)));
        if (phaseByVtable.Count >= PhasesRemembered)
        {
            phaseByVtable.Clear();
        }
        phaseByVtable[vtable] = named;
        return named;
    }

    public int? EnemyCount(long package) => ModuleMemoryReader.ReadInt32((nint)package + EnemyCountOffset);

    public int? MoneyWon(long package) => ModuleMemoryReader.ReadInt32((nint)package + MoneyOffset);
}
