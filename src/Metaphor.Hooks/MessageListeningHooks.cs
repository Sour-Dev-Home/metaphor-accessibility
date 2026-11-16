using Metaphor.Core;

namespace Metaphor.Hooks;

// Issue #55's spike: a development-only hook that logs what one of the game's message functions sees, to
// HookServices.Log. The loader's adapter passes every call through unchanged (#88). Compiled message files are logged
// by MessageFileHook's log observer (#86). Signature and argument shape are MF.Toolkit's
// (docs/raw-sources/game-text-hooks-community-2026-11-15.md), checked on this build
// (docs/raw-sources/mf-toolkit-message-signatures-2026-11-15.md).

/// <summary>Logs each message the game looks up, by message number and label index. MF.Toolkit only calls this
/// function; hooking it is new.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class GetMessageByLabelIndexListener : IGameHook, IObserves4
{
    private readonly MessageLog? log;

    public GetMessageByLabelIndexListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "GetMessageByLabelIndex";

    /// <summary>MF.Toolkit MessageService.cs line 32, with its pointer types to MSG, MsgFlag and MsgConfigs kept
    /// opaque, and its int label index pointer-sized (rule 9).</summary>
    public void Observe(nint msgId, nint labelIdx, nint flags, nint configs) =>
        // This hook has never fired in the game, so what a real caller passes is unverified: read it safely.
        log?.LookedUp(ModuleMemoryReader.ReadInt32(msgId) ?? -1, (int)labelIdx);
}
