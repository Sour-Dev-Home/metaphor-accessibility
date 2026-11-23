using System.Globalization;
using Metaphor.Core;

namespace Metaphor.Hooks;

/// <summary>Issue #197's probe, development only: logs the shop's quantity window after each up or down press, so a
/// session can tell where the count lives before the quantity is spoken. <c>0xe70900</c>, which
/// <c>PurchaseNumSelectChoice</c>'s up/down event handler (<c>0xe7af70</c>) jumps to with <c>rcx</c> = its lambda
/// <c>+ 0x8</c>, the slot holding the choice, and <c>rdx</c> the event; it steps the count at choice <c>+0x418</c>
/// (<c>int</c>), between 1 and the maximum at <c>+0x41c</c>, and the unit price is at <c>+0x424</c> (<c>int</c>). Our
/// reading of game build 548ddc95… (Steam build 18330018) in
/// docs/findings/shop-rows-quantity-and-rank-up-notice-static.md and Ghidra 12.2 (<c>disasm 0xe7af70</c>,
/// <c>0xe70900</c>, 2026-11-23); live values <c>[NEEDS VERIFICATION]</c>. Observed after the original, so the count is
/// the stepped one. Only reads and logs: the loader's adapter guards Observe and calls the original unchanged (rule
/// 14); every argument is observed pointer-sized (rule 9).</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class ShopQuantityListener : IGameHook, IObserves4WithResult
{
    private const int Count = 0x418;
    private const int UnitPrice = 0x424;

    private readonly MessageLog? log;
    private readonly ClassNames names = new();

    public ShopQuantityListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "PurchaseCountStep";

    /// <summary>Observed after the step; <paramref name="first"/> holds the choice's address, the rest are passed on
    /// unread.</summary>
    public void Observe(nint first, nint second, nint third, nint fourth, nint result)
    {
        if (log is null || ModuleMemoryReader.ReadPointer(first) is not { } choice || choice == 0)
        {
            return;
        }
        var count = ModuleMemoryReader.ReadInt32(choice + Count);
        var price = ModuleMemoryReader.ReadInt32(choice + UnitPrice);
        log.MenuNote(string.Create(CultureInfo.InvariantCulture,
            $"quantity this=0x{choice:x} class={names.Of(choice) ?? "?"} count={Text(count)} price={Text(price)}"));
    }

    private static string Text(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "?";
}
