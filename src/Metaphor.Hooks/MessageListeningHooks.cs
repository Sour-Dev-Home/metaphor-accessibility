using System.Diagnostics.CodeAnalysis;
using Metaphor.Core;
using Reloaded.Hooks.Definitions;
// Both packages name an IReloadedHooks; the loader's controller is this one (as the template aliases it).
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Metaphor.Hooks;

// Issue #55's spike: two development-only hooks that log what the game's message functions see, to find which one
// carries the dialogue line on screen. They pass every call through unchanged, and log to HookServices.Log. Signatures
// and delegate shapes are MF.Toolkit's (docs/raw-sources/game-text-hooks-community-2026-11-15.md), checked on this build
// (docs/raw-sources/mf-toolkit-message-signatures-2026-11-15.md).

/// <summary>Logs each message file the game compiles: its number, path and source text. MF.Toolkit hooks this same
/// function with this delegate.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class CompileMsgFileListener : IGameHook
{
    // MF.Toolkit MessageService.cs line 37, which declares the length and param4 as int, param5 as byte and the return
    // as uint. Pointer-sized here so the call passes through unchanged (rule 9); only the low bits are read for the log.
    private delegate nint CompileMsgFile(nint msgSrc, nint msgSrcLength, nint msgFile, nint param4, nint param5);

    private readonly CompileMsgFile callback;
    private readonly MessageLog? log;
    private IHook<CompileMsgFile>? hook;

    public CompileMsgFileListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
        callback = OnCompile;
    }

    public string SignatureName => "CompileMsgFile";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        // Stored before it goes live: the game may call it the moment it's activated.
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    [SuppressMessage("Design", "CA1031", Justification = "An exception must never cross back into the game.")]
    private nint OnCompile(nint msgSrc, nint msgSrcLength, nint msgFile, nint param4, nint param5)
    {
        try
        {
            // MF.Toolkit reads the path as ANSI; the source read as UTF-8 gave readable English on build 548ddc95
            // (docs/raw-sources/message-listening-run-2026-11-16.md). Both go through ReadProcessMemory: a bad pointer
            // from the game must not fault here.
            log?.Compiled(
                (int)param4,
                ModuleMemoryReader.ReadAnsiString(msgFile) ?? "(unreadable path)",
                ModuleMemoryReader.ReadUtf8(msgSrc, (int)msgSrcLength) ?? "(unreadable source)");
        }
        catch (Exception)
        {
        }
        return hook!.OriginalFunction(msgSrc, msgSrcLength, msgFile, param4, param5);
    }
}

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
