using System.Diagnostics.CodeAnalysis;
using Metaphor.Core;
using Reloaded.Hooks.Definitions;
// Both packages name an IReloadedHooks; the loader's controller is this one (as the template aliases it).
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Metaphor.Hooks;

/// <summary>Issue #55's spike: two development-only hooks that log what the game's message functions see, to find which
/// one carries the dialogue line on screen. They pass every call through unchanged. Signatures and delegate shapes are
/// MF.Toolkit's (docs/raw-sources/game-text-hooks-community-2026-11-15.md), checked on this build
/// (docs/raw-sources/mf-toolkit-message-signatures-2026-11-15.md).</summary>
internal static class MessageListening
{
    /// <summary>Where the hooks log; set by the entry point in Debug builds before hooks are installed.</summary>
    public static MessageLog? Log { get; set; }
}

/// <summary>Logs each message file the game compiles: its number, path and source text. MF.Toolkit hooks this same
/// function with this delegate.</summary>
[GameHook("548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504", DevOnly = true)]
internal sealed class CompileMsgFileListener : IGameHook
{
    // MF.Toolkit MessageService.cs line 37.
    private delegate uint CompileMsgFile(nint msgSrc, int msgSrcLength, nint msgFile, int param4, byte param5);

    private readonly CompileMsgFile callback;
    private IHook<CompileMsgFile>? hook;

    public CompileMsgFileListener() => callback = OnCompile;

    public string SignatureName => "CompileMsgFile";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        // Stored before it goes live: the game may call it the moment it's activated.
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    [SuppressMessage("Design", "CA1031", Justification = "An exception must never cross back into the game.")]
    private uint OnCompile(nint msgSrc, int msgSrcLength, nint msgFile, int param4, byte param5)
    {
        try
        {
            // MF.Toolkit reads the path as ANSI; the source read as UTF-8 gave readable English on build 548ddc95
            // (docs/raw-sources/message-listening-run-2026-11-16.md). Both go through ReadProcessMemory: a bad pointer
            // from the game must not fault here.
            MessageListening.Log?.Compiled(
                param4,
                ModuleMemoryReader.ReadAnsiString(msgFile) ?? "(unreadable path)",
                ModuleMemoryReader.ReadUtf8(msgSrc, msgSrcLength) ?? "(unreadable source)");
        }
        catch (Exception)
        {
        }
        return hook!.OriginalFunction(msgSrc, msgSrcLength, msgFile, param4, param5);
    }
}

/// <summary>Logs each message the game looks up, by message number and label index. MF.Toolkit only calls this
/// function; hooking it is new.</summary>
[GameHook("548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504", DevOnly = true)]
internal sealed unsafe class GetMessageByLabelIndexListener : IGameHook
{
    // MF.Toolkit MessageService.cs line 32, with its pointer types to MSG, MsgFlag and MsgConfigs kept opaque.
    private delegate nint GetMessageByLabelIndex(int* msgId, int labelIdx, nint flags, nint configs);

    private readonly GetMessageByLabelIndex callback;
    private IHook<GetMessageByLabelIndex>? hook;

    public GetMessageByLabelIndexListener() => callback = OnLookup;

    public string SignatureName => "GetMessageByLabelIndex";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        // Stored before it goes live: the game may call it the moment it's activated.
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    [SuppressMessage("Design", "CA1031", Justification = "An exception must never cross back into the game.")]
    private nint OnLookup(int* msgId, int labelIdx, nint flags, nint configs)
    {
        try
        {
            // This hook has never fired in the game, so what a real caller passes is unverified: read it safely.
            MessageListening.Log?.LookedUp(ModuleMemoryReader.ReadInt32((nint)msgId) ?? -1, labelIdx);
        }
        catch (Exception)
        {
        }
        return hook!.OriginalFunction(msgId, labelIdx, flags, configs);
    }
}
