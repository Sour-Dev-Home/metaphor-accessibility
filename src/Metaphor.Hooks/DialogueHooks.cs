using Metaphor.Core.Messages;

namespace Metaphor.Hooks;

// Message events (#52, #56, #86): one hook per game function decodes each call once and hands the event to
// HookServices.Messages, whose observers keep compiled messages, speak requested lines and, in Debug builds, log both.
// The loader's adapter passes every call through unchanged (#88).

/// <summary>Reports each message file the game compiles. Signature and argument shape are MF.Toolkit's
/// (docs/raw-sources/game-text-hooks-community-2026-11-15.md), checked on this build
/// (docs/raw-sources/mf-toolkit-message-signatures-2026-11-15.md).</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class MessageFileHook : IGameHook, IObserves5
{
    private readonly MessageEvents events;

    public MessageFileHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        events = services.Messages;
    }

    public string SignatureName => "CompileMsgFile";

    /// <summary>MF.Toolkit MessageService.cs line 37 declares the length and param4 as int, param5 as byte and the
    /// return as uint; only the low 32 bits of the length and param4 are read.</summary>
    public void Observe(nint msgSrc, nint msgSrcLength, nint msgFile, nint param4, nint param5)
    {
        // MF.Toolkit reads the path as ANSI; the source read as UTF-8 gave readable English on this build
        // (docs/raw-sources/message-listening-run-2026-11-16.md). A file whose source can't be read is skipped.
        if (ModuleMemoryReader.ReadUtf8(msgSrc, (int)msgSrcLength) is { } source)
        {
            events.Compiled(new MessageFileCompiled(
                ModuleMemoryReader.ReadAnsiString(msgFile) ?? "(unreadable path)", (int)param4, source));
        }
    }
}

/// <summary>Reports each line the game asks for by label. The function, its arguments and the name (ours) are in docs/raw-sources/msg-request-functions-2026-11-16.md; that its pointer is the label is
/// from docs/raw-sources/msg-request-run-2026-11-16.md.</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class DialogueLineHook : IGameHook, IObserves3
{
    private readonly MessageEvents events;

    public DialogueLineHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        events = services.Messages;
    }

    public string SignatureName => "MsgRequestByName";

    public void Observe(nint kind, nint label, nint options)
    {
        // A label that can't be read is skipped.
        if (ModuleMemoryReader.ReadAnsiString(label) is { } name)
        {
            events.Requested(new LineRequested((int)kind, name));
        }
    }
}
