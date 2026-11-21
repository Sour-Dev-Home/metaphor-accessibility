using System.Globalization;
using Metaphor.Core;
using Metaphor.Core.Battle;

namespace Metaphor.Hooks;

/// <summary>Reports each target a resolved battle action hits (#247): the battle GUI's <c>BattleHitResult</c>,
/// <c>(request*, gui*, result*, target*, ushort index, int mode)</c> in <c>.ecode</c>, which runs once per target and
/// writes into its request the HP change and the HP the target has left before it sends the damage popup. Observed after
/// it returns, so those are written: the request's <c>+0x30</c> (HP change) and <c>+0x40</c> (HP left, clamped to
/// 0..max), and the result's kind at <c>+0x304</c>, a short. Function, arguments and offsets are our reading of the exe
/// on build 548ddc95… (docs/findings/battle-hit-result-hp-left-static.md); none has been checked in memory yet. A call
/// whose numbers can't be read is skipped. In a Debug build the raw values go to the message log for the by-ear check.
/// </summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class BattleHitResultHook : IGameHook, IObserves6WithResult
{
    private const int HpChangeAt = 0x30;
    private const int MpChangeAt = 0x34;
    private const int HpMaxAt = 0x38;
    private const int HpLeftAt = 0x40;
    private const int MpLeftAt = 0x44;
    private const int FlagsAt = 0x300;
    private const int KindAt = 0x304;

    private readonly IBattleHitObserver hits;
    private readonly MessageLog? log;

    public BattleHitResultHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        hits = services.BattleHits;
        log = services.Log;
    }

    public string SignatureName => "BattleHitResult";

    /// <summary>Six arguments, the last two on the stack, so the shape passes them all on to the original.</summary>
    public void Observe(nint request, nint gui, nint result, nint target, nint index, nint mode, nint unused)
    {
        if (ModuleMemoryReader.ReadInt32(request + HpChangeAt) is not { } hpChange ||
            ModuleMemoryReader.ReadInt32(request + HpLeftAt) is not { } hpLeft ||
            ModuleMemoryReader.ReadInt32(result + KindAt) is not { } kindAndSub)
        {
            return;
        }
        // The kind is the low short; the high short is the sub-kind at +0x306, which picks the popup's style.
        var kind = kindAndSub & 0xFFFF;
        log?.Requested(SignatureName, string.Create(CultureInfo.InvariantCulture,
            $"target=0x{target:x} index={index & 0xFFFF} mode={(int)mode} kind=0x{kind:x} sub=0x{(kindAndSub >> 16) & 0xFFFF:x} flags=0x{ModuleMemoryReader.ReadInt32(result + FlagsAt) ?? 0:x} hp={hpChange} hpLeft={hpLeft} hpMax={ModuleMemoryReader.ReadInt32(request + HpMaxAt)} mp={ModuleMemoryReader.ReadInt32(request + MpChangeAt)} mpLeft={ModuleMemoryReader.ReadInt32(request + MpLeftAt)}"));
        hits.Observe(new BattleHit(target, hpChange, hpLeft, kind));
    }
}
