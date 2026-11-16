using Metaphor.Core;

namespace Metaphor.Hooks;

// Issues #93 (lead 2) and #82 (discovery): development-only hooks on what MsgRequestSelect and MsgGetSelectedID run, to
// see whether a reply choice's prompt and options pass through them as labels, and which option the game reads back.
// The functions, their arguments and the names (ours) are in docs/raw-sources/msg-select-functions-2026-11-16.md.
// Every integer or pointer is observed pointer-sized (rule 9) and narrowed only for the log.

/// <summary>What <c>MsgRequestSelect</c> calls: kind (32 bits), two 64-bit values (the lead: the prompt's and the
/// choice's labels, <c>[NEEDS VERIFICATION]</c>), a 32-bit number (-1 from the script function), a pointer to a
/// 12-byte options block, and a 16-bit value on the stack.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class MsgRequestSelectByNameListener : IGameHook, IObserves6
{
    private readonly MessageLog? log;

    public MsgRequestSelectByNameListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "MsgRequestSelectByName";

    public void Observe(nint kind, nint first, nint second, nint fourth, nint options, nint sixth) =>
        log?.SelectRequested(
            (int)kind,
            ModuleMemoryReader.ReadAnsiString(first),
            ModuleMemoryReader.ReadAnsiString(second),
            (int)fourth,
            MsgRequestListening.Options(options),
            (ushort)sixth);
}

/// <summary>Where <c>MsgGetSelectedID</c>'s target jumps: one pointer (an object the target found) in, the selection
/// out as 32 bits, observed after it returns.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class MsgSelectedIdListener : IGameHook, IObserves1WithResult
{
    private readonly MessageLog? log;

    public MsgSelectedIdListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "MsgSelectedId";

    public void Observe(nint first, nint result) => log?.SelectedId((int)result);
}
