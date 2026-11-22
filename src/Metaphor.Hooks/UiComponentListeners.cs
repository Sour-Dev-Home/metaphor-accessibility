using Metaphor.Core;

namespace Metaphor.Hooks;

// Issue #691: development-only listeners that log UI components and layouts as they open, so a session can tell which
// one shows the item-get banner (#542), the autosave notice (#543) and the Memorandum notice (#544). The functions and
// their arguments are our reading of game build 548ddc95… (Steam build 18330018) in
// docs/findings/ui-component-open-points-static.md; the names are ours. Both only read and log: the loader's adapter
// guards Observe and calls the original unchanged (rule 14). Every argument is observed pointer-sized (rule 9).

/// <summary>The UI layouts' shared start-an-animation, <c>0xa70010</c>:
/// <c>void (Layout* layout, ss::Player* player, const u32* animation, ?, u8)</c>. The open functions (the 21st
/// virtual function) of 92 layouts reach it through a 3-argument wrapper (<c>0xa6ffd0</c>), among them <c>AutoSavingLayout</c>, <c>SavingLayout</c> and
/// <c>SystemSavingLayout</c>, so each call logs the layout's class, its address and the animation's number. Other
/// animations a layout starts land here too. A repeat of the same animation on the same layout within a second is
/// dropped, so a known pair costs a pointer read, a dictionary lookup and no allocation.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class LayoutAnimationListener : IGameHook, IObserves5
{
    private const long QuietMilliseconds = 1000;

    private readonly MessageLog? log;
    private readonly ClassNames names = new();
    private readonly RepeatGate repeats = new(QuietMilliseconds, () => Environment.TickCount64);

    public LayoutAnimationListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "LayoutAnimationPlay";

    /// <summary>Observed before the animation starts; the fourth and fifth arguments are passed on unread.</summary>
    public void Observe(nint first, nint second, nint third, nint fourth, nint fifth)
    {
        if (log is null)
        {
            return;
        }
        var animation = ModuleMemoryReader.ReadInt32(third);
        if (repeats.LetThrough(first, animation ?? -1))
        {
            log.UiAnimated(names.Of(first), first, animation);
        }
    }
}

/// <summary>The item-get banner's open, <c>UUIC_Common_EventItemGet</c>'s <c>0xf560d0</c>:
/// <c>void (UUIC_Common_EventItemGet* this, request* items)</c>, called by the <c>ItemGetTreasure</c> and
/// <c>ItemGetWindowEventOpen</c> script functions. Its layout (<c>ItemGetNotice</c>) plays its animation without the
/// shared start-an-animation, so it gets its own record. Runs once a pickup.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class EventItemGetOpenListener : IGameHook, IObserves3
{
    private readonly MessageLog? log;
    private readonly ClassNames names = new();

    public EventItemGetOpenListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "EventItemGetOpen";

    /// <summary>Observed before the banner opens; only <paramref name="component"/> and <paramref name="items"/> are
    /// arguments, the third is whatever the register held.</summary>
    public void Observe(nint component, nint items, nint third) => log?.UiOpened(names.Of(component), component);
}
