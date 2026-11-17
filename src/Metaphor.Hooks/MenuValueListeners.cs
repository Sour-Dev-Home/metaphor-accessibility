using Metaphor.Core;
using Metaphor.Core.Menus;

namespace Metaphor.Hooks;

// Issue #155: development-only, opt-in listeners on the game's own value getters, to see which ones the menus call and
// with what. Each observes its getter after it returns (rule 14) and logs numbers only. The functions, their arguments
// and the names (ours, after the script functions that jump to them) are our reading of the exe on one build
// (docs/raw-sources/menu-value-getters-2026-11-17.md); what each argument means is a lead until a run shows it. Every
// argument is observed pointer-sized (rule 9) and cut to the width the getter reads.

/// <summary>What the menu value listeners share: the gate that keeps a getter called every frame to one line per visit
/// or change, the caller lookup, and the log.</summary>
internal sealed class MenuValueListening
{
    /// <summary>The file in the mod's folder that turns the listeners on.</summary>
    public const string OptInFile = "opt-in-menu-values.txt";

    // A getter not called with the same arguments for this long counts as a new visit and is logged again.
    private const long QuietMilliseconds = 1000;

    private readonly MessageLog? log;
    private readonly ValueReadGate gate = new(QuietMilliseconds, () => Environment.TickCount64);
    private readonly CallerAddress caller = new();

    public MenuValueListening(MessageLog? log) => this.log = log;

    /// <summary>Logs <paramref name="read"/> with its caller when the gate lets it through.</summary>
    public void Saw(ValueRead read)
    {
        if (log is not null && gate.LetThrough(read))
        {
            log.ValueRead(read, caller.Find());
        }
    }
}

/// <summary>The money total's getter, which the script function <c>GetMoney</c> jumps to: <c>int ()</c>.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = MenuValueListening.OptInFile)]
internal sealed class MoneyGetterListener : IGameHook, IObserves1WithResult
{
    private readonly MenuValueListening listening;

    public MoneyGetterListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        listening = new MenuValueListening(services.Log);
    }

    public string SignatureName => "GetMoney";

    public void Observe(nint first, nint result) => listening.Saw(new ValueRead(SignatureName, [], (int)result));
}

/// <summary>An item's count in the bag, which the script function <c>GetBagItem</c> calls: <c>byte (int itemId)</c>,
/// dispatching on the item's category (<c>itemId &gt;&gt; 12</c>).</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = MenuValueListening.OptInFile)]
internal sealed class BagItemGetterListener : IGameHook, IObserves1WithResult
{
    private readonly MenuValueListening listening;

    public BagItemGetterListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        listening = new MenuValueListening(services.Log);
    }

    public string SignatureName => "GetBagItem";

    public void Observe(nint itemId, nint result) =>
        listening.Saw(new ValueRead(SignatureName, [(int)itemId], (byte)result));
}

/// <summary>A party member's unit, which the script function <c>GetPlayerUnitID</c> jumps to: <c>Unit* (int
/// index)</c>. Logged so the unit addresses in the other records can be matched to a member.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = MenuValueListening.OptInFile)]
internal sealed class PlayerUnitGetterListener : IGameHook, IObserves1WithResult
{
    private readonly MenuValueListening listening;

    public PlayerUnitGetterListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        listening = new MenuValueListening(services.Log);
    }

    public string SignatureName => "GetPlayerUnit";

    public void Observe(nint index, nint result) => listening.Saw(new ValueRead(SignatureName, [(int)index], result));
}

/// <summary>A unit parameter, which the script function <c>UnitGetParam</c> calls: <c>int (Unit* unit, int index)</c>.
/// What each index means is unknown.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = MenuValueListening.OptInFile)]
internal sealed class UnitParamGetterListener : IGameHook, IObserves4WithResult
{
    private readonly MenuValueListening listening;

    public UnitParamGetterListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        listening = new MenuValueListening(services.Log);
    }

    public string SignatureName => "UnitGetParam";

    public void Observe(nint unit, nint index, nint third, nint fourth, nint result) =>
        listening.Saw(new ValueRead(SignatureName, [unit, (int)index], (int)result));
}

/// <summary>A unit's affinity to an element under an archetype, which the script functions <c>UnitGetAffinity</c> and
/// <c>UnitGetAffinityType</c> reach: <c>short (Unit* unit, ushort archetypeId, int element, int flag)</c>; the script
/// path passes the unit's current archetype and a flag of 0.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = MenuValueListening.OptInFile)]
internal sealed class UnitAffinityGetterListener : IGameHook, IObserves4WithResult
{
    private readonly MenuValueListening listening;

    public UnitAffinityGetterListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        listening = new MenuValueListening(services.Log);
    }

    public string SignatureName => "UnitAffinity";

    public void Observe(nint unit, nint archetype, nint element, nint flag, nint result) =>
        listening.Saw(new ValueRead(SignatureName, [unit, (ushort)archetype, (int)element, (int)flag], (short)result));
}
