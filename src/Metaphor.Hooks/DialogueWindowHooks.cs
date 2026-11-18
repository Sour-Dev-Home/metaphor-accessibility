using Metaphor.Core;

namespace Metaphor.Hooks;

// Debug evidence beside the window pointer (#232): hooks on the code the MsgOpen and MsgClose script functions jump to,
// in .text1, that only log that they ran. They follow message handles, not the dialogue window, so they no longer
// drive the dialogue controls (DialogueWindowPointer does): docs/raw-sources/dialogue-window-flag-session5-2026-11-17.md.
// The functions, their arguments and the names (ours) are in docs/raw-sources/msg-window-functions-2026-11-16.md.
// .text1 hooks stay opt-in in every build (maintainer, 2026-11-16); these are also development-only, so a Release build
// never installs them.

/// <summary>The file in the mod's folder that turns on the dialogue window hooks.</summary>
internal static class DialogueWindowOptIn
{
    public const string File = "opt-in-dialogue-window.txt";

    /// <summary>What the load message calls it (#223).</summary>
    public const string Name = "dialogue window";
}

/// <summary>Logs <c>MsgOpen</c>'s body running: one argument (a C string, not read).</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = DialogueWindowOptIn.File,
    OptInName = DialogueWindowOptIn.Name)]
internal sealed class DialogueWindowOpenHook : IGameHook, IObserves3
{
    private readonly MessageLog? log;

    public DialogueWindowOpenHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "MsgOpenBody";

    public void Observe(nint name, nint second, nint third) => log?.WindowHook("open");
}

/// <summary>Logs <c>MsgClose</c>'s body running: one 32-bit argument (not read).</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = DialogueWindowOptIn.File,
    OptInName = DialogueWindowOptIn.Name)]
internal sealed class DialogueWindowCloseHook : IGameHook, IObserves3
{
    private readonly MessageLog? log;

    public DialogueWindowCloseHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "MsgCloseBody";

    public void Observe(nint window, nint second, nint third) => log?.WindowHook("close");
}
