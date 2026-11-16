using System.Diagnostics.CodeAnalysis;
using Metaphor.Core.Messages;
using Metaphor.Features.Dialogue;
using Reloaded.Hooks.Definitions;
// Both packages name an IReloadedHooks; the loader's controller is this one (as the template aliases it).
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Metaphor.Hooks;

// Spoken story dialogue (#52, #56): one hook keeps each compiled message file's messages (MessageLibrary says why),
// the other speaks the line the game asks for. Both pass every call through unchanged, and share HookServices'
// message library.

/// <summary>Keeps the messages of each message file the game compiles. Signature and delegate shape are MF.Toolkit's
/// (docs/raw-sources/game-text-hooks-community-2026-11-15.md), checked on this build
/// (docs/raw-sources/mf-toolkit-message-signatures-2026-11-15.md).</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class MessageFileHook : IGameHook
{
    // MF.Toolkit MessageService.cs line 37, pointer-sized (rule 9); only the length's low 32 bits are read.
    private delegate nint CompileMsgFile(nint msgSrc, nint msgSrcLength, nint msgFile, nint param4, nint param5);

    private readonly CompileMsgFile callback;
    private readonly MessageLibrary messages;
    private IHook<CompileMsgFile>? hook;

    public MessageFileHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        messages = services.Messages;
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
            // The source read as UTF-8 gave readable English on this build
            // (docs/raw-sources/message-listening-run-2026-11-16.md).
            if (ModuleMemoryReader.ReadUtf8(msgSrc, (int)msgSrcLength) is { } source)
            {
                messages.Compiled(source);
            }
        }
        catch (Exception)
        {
        }
        return hook!.OriginalFunction(msgSrc, msgSrcLength, msgFile, param4, param5);
    }
}

/// <summary>Speaks each line the game asks for by label. The function, its arguments and the name (ours) are in
/// docs/raw-sources/msg-request-functions-2026-11-16.md; that its pointer is the label is from
/// docs/raw-sources/msg-request-run-2026-11-16.md.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class DialogueLineHook : IGameHook
{
    private delegate nint Request(nint kind, nint label, nint options);

    private readonly Request callback;
    private readonly DialogueLines lines;
    private IHook<Request>? hook;

    public DialogueLineHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        lines = services.Lines;
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
            // Only kind 0, the kind a spoken event line was seen with. Kinds 1 and 2 come from the system message,
            // subtitle and narration wrappers (msg-request-functions-2026-11-16.md); what they show is unverified.
            if ((int)kind == 0 && ModuleMemoryReader.ReadAnsiString(label) is { } name)
            {
                lines.Requested(name);
            }
        }
        catch (Exception)
        {
        }
        return hook!.OriginalFunction(kind, label, options);
    }
}
