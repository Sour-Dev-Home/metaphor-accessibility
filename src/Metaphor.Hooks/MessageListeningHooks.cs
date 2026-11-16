using System.Diagnostics.CodeAnalysis;
using Metaphor.Core;
using Reloaded.Hooks.Definitions;
// Both packages name an IReloadedHooks; the loader's controller is this one (as the template aliases it).
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Metaphor.Hooks;

// Issue #55's spike: a development-only hook that logs what one of the game's message functions sees. It passes every
// call through unchanged, and logs to HookServices.Log. Compiled message files are logged by MessageFileHook's log
// observer (#86). Signature and delegate shape are MF.Toolkit's (docs/raw-sources/game-text-hooks-community-2026-11-15.md),
// checked on this build (docs/raw-sources/mf-toolkit-message-signatures-2026-11-15.md).

/// <summary>Logs each message the game looks up, by message number and label index. MF.Toolkit only calls this
/// function; hooking it is new.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed unsafe class GetMessageByLabelIndexListener : IGameHook
{
    // MF.Toolkit MessageService.cs line 32, with its pointer types to MSG, MsgFlag and MsgConfigs kept opaque, and its
    // int label index pointer-sized (rule 9).
    private delegate nint GetMessageByLabelIndex(int* msgId, nint labelIdx, nint flags, nint configs);

    private readonly GetMessageByLabelIndex callback;
    private readonly MessageLog? log;
    private IHook<GetMessageByLabelIndex>? hook;

    public GetMessageByLabelIndexListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        callback = OnLookup;
    }

    public string SignatureName => "GetMessageByLabelIndex";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        // Stored before it goes live: the game may call it the moment it's activated.
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    [SuppressMessage("Design", "CA1031", Justification = "An exception must never cross back into the game.")]
    private nint OnLookup(int* msgId, nint labelIdx, nint flags, nint configs)
    {
        try
        {
            // This hook has never fired in the game, so what a real caller passes is unverified: read it safely.
            log?.LookedUp(ModuleMemoryReader.ReadInt32((nint)msgId) ?? -1, (int)labelIdx);
        }
        catch (Exception)
        {
        }
        return hook!.OriginalFunction(msgId, labelIdx, flags, configs);
    }
}
