using Metaphor.Core.Menus;

namespace Metaphor.Core.Battle;

/// <summary>What the battle project adds to the battle lists the menus project speaks (#692 (b), #260): a skill row's
/// cost after its name, and whether the Synthesis list speaks at all. The menus' module makes it and reads it; the
/// battle module, registered later, says where each comes from (<see cref="Add"/>), behind its own settings. Until then
/// a row has no cost and the Synthesis list is silent.</summary>
public sealed class BattleListAdditions
{
    private volatile Func<int, bool, string?>? cost;
    private volatile Func<bool>? synthesisOn;
    private volatile Func<int, int?>? quantity;

    /// <summary>The cost said after the row whose name the list fetched by <paramref name="nameSerial"/>, such as
    /// "8 MP", with its turn icons when <paramref name="turnIcons"/> (the Synthesis list); null for none.</summary>
    public string? CostOf(int nameSerial, bool turnIcons) => cost?.Invoke(nameSerial, turnIcons);

    /// <summary>Whether the Synthesis list speaks now.</summary>
    public bool SynthesisOn() => synthesisOn?.Invoke() == true;

    /// <summary>How many of the item the party holds, for the item row whose name the list fetched by
    /// <paramref name="nameSerial"/> (#937); null when it isn't known. Nothing adds a source yet: where the game keeps
    /// the count a battle item row shows is <c>[NEEDS VERIFICATION]</c> (the bag counts in
    /// docs/findings/money-follower-rank-and-shop-static.md are static only, category 0 only, and keyed by item ID, not
    /// by name serial).</summary>
    public int? QuantityOf(int nameSerial) => quantity?.Invoke(nameSerial);

    /// <summary>Says where the item rows' quantities come from (#937).</summary>
    public void AddQuantities(Func<int, int?> quantityOf)
    {
        ArgumentNullException.ThrowIfNull(quantityOf);
        quantity = quantityOf;
    }

    /// <summary>Says where the costs and the Synthesis list's setting come from.</summary>
    public void Add(Func<int, bool, string?> costOf, Func<bool> synthesis)
    {
        ArgumentNullException.ThrowIfNull(costOf);
        ArgumentNullException.ThrowIfNull(synthesis);
        cost = costOf;
        synthesisOn = synthesis;
    }
}

/// <summary>Battle screens a battle feature speaks that the menus project lists among the known screens (#258).
/// </summary>
public static class BattleScreens
{
    /// <summary>The Order menu's Party screen, the four battle members, by its owner's class on build 548ddc95
    /// (docs/findings/battle-party-swap-and-formation-static.md).</summary>
    public static Screen Party { get; } = new("Party", "BTL_OrderPartyBattleMember@ope@ui@app", OutsideMenus: true);

    /// <summary>The reserve members the Party screen swaps in (#922), by its owner's class in the maintainer's
    /// 2026-11-24 log (Debug, Steam 18330018): it opens as a battle member is picked, three rows.</summary>
    public static Screen Reserve { get; } = new("Reserve", "BTL_OrderPartyReserve@ope@ui@app", OutsideMenus: true);
}
