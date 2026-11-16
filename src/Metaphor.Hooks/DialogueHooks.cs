using System.Diagnostics.CodeAnalysis;
using Metaphor.Core.Messages;
using Reloaded.Hooks.Definitions;
// Both packages name an IReloadedHooks; the loader's controller is this one (as the template aliases it).
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Metaphor.Hooks;

// Message events (#52, #56, #86): one hook per game function decodes each call once and hands the event to
// HookServices.Messages, whose observers keep compiled messages, speak requested lines and, in Debug builds, log both.
// Both hooks pass every call through unchanged.

/// <summary>Reports each message file the game compiles. Signature and delegate shape are MF.Toolkit's
/// (docs/raw-sources/game-text-hooks-community-2026-11-15.md), checked on this build
/// (docs/raw-sources/mf-toolkit-message-signatures-2026-11-15.md).</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class MessageFileHook : IGameHook
{
    // MF.Toolkit MessageService.cs line 37, which declares the length and param4 as int, param5 as byte and the return
    // as uint. Pointer-sized here so the call passes through unchanged (rule 9); only the low 32 bits are read.
    private delegate nint CompileMsgFile(nint msgSrc, nint msgSrcLength, nint msgFile, nint param4, nint param5);

    private readonly CompileMsgFile callback;
    private readonly MessageEvents events;
    private IHook<CompileMsgFile>? hook;

    public MessageFileHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        events = services.Messages;
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
            // MF.Toolkit reads the path as ANSI; the source read as UTF-8 gave readable English on this build
            // (docs/raw-sources/message-listening-run-2026-11-16.md). A file whose source can't be read is skipped.
            if (ModuleMemoryReader.ReadUtf8(msgSrc, (int)msgSrcLength) is { } source)
            {
                events.Compiled(new MessageFileCompiled(
                    ModuleMemoryReader.ReadAnsiString(msgFile) ?? "(unreadable path)", (int)param4, source));
            }
        }
        catch (Exception)
        {
        }
        return hook!.OriginalFunction(msgSrc, msgSrcLength, msgFile, param4, param5);
    }
}

/// <summary>Reports each line the game asks for by label. The function, its arguments and the name (ours) are in
/// docs/raw-sources/msg-request-functions-2026-11-16.md; that its pointer is the label is from
/// docs/raw-sources/msg-request-run-2026-11-16.md.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class DialogueLineHook : IGameHook
{
    private delegate nint Request(nint kind, nint label, nint options);

    private readonly Request callback;
    private readonly MessageEvents events;
    private IHook<Request>? hook;

    public DialogueLineHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        events = services.Messages;
        callback = OnRequest;
    }

    public string SignatureName => "MsgRequestByName";

    public void Install(IReloadedHooks hooks, nint address, IReadOnlyDictionary<string, nint> calls)
    {
        // Stored before it goes live: the game may call it the moment it's activated.
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    [SuppressMessage("Design", "CA1031", Justification = "An exception must never cross back into the game.")]
    private nint OnRequest(nint kind, nint label, nint options)
    {
        try
        {
            // A label that can't be read is skipped.
            if (ModuleMemoryReader.ReadAnsiString(label) is { } name)
            {
                events.Requested(new LineRequested((int)kind, name));
            }
        }
        catch (Exception)
        {
        }
        return hook!.OriginalFunction(kind, label, options);
    }
}
