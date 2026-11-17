using Metaphor.Features.Dialogue;

namespace Metaphor.Hooks;

// The dialogue window's state for the dialogue controls (#58): the window is open from the MsgOpen script function to
// MsgClose (#58's design). Both hooks sit on the code the script functions jump to, in .text1, and only observe. The
// functions, their arguments and the names (ours) are in docs/raw-sources/msg-window-functions-2026-11-16.md.

/// <summary>Reports the dialogue window opening: <c>MsgOpen</c>'s body, one argument (a C string, not read).</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class DialogueWindowOpenHook : IGameHook, IObserves3
{
    private readonly DialogueControls controls;

    public DialogueWindowOpenHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        controls = services.DialogueControls;
    }

    public string SignatureName => "MsgOpenBody";

    public void Observe(nint name, nint second, nint third) => controls.WindowOpened();
}

/// <summary>Reports the dialogue window closing: <c>MsgClose</c>'s body, one 32-bit argument (not read).</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class DialogueWindowCloseHook : IGameHook, IObserves3
{
    private readonly DialogueControls controls;

    public DialogueWindowCloseHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        controls = services.DialogueControls;
    }

    public string SignatureName => "MsgCloseBody";

    public void Observe(nint window, nint second, nint third) => controls.WindowClosed();
}
