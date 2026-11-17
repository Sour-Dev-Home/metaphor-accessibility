using Metaphor.Features.Dialogue;

namespace Metaphor.Hooks;

// The dialogue window's state for the dialogue controls (#58): the window is open from the MsgOpen script function to
// MsgClose (#58's design). Both hooks sit on the code the script functions jump to, in .text1, and only observe. The
// functions, their arguments and the names (ours) are in docs/raw-sources/msg-window-functions-2026-11-16.md.
// .text1 hooks stay opt-in (maintainer, 2026-11-16), in every build: without the file neither goes in, the controls
// never learn the window's state, and their presses act at any time and are never hidden from the game.

/// <summary>The file in the mod's folder that turns on the dialogue window hooks.</summary>
internal static class DialogueWindowOptIn
{
    public const string File = "opt-in-dialogue-window.txt";
}

/// <summary>Reports the dialogue window opening: <c>MsgOpen</c>'s body, one argument (a C string, not read). Created
/// only when it's opted in and approved for the running build, and then tells the controls the window will be reported.
/// </summary>
[GameHook(GameBuilds.Steam18330018, OptInFile = DialogueWindowOptIn.File)]
internal sealed class DialogueWindowOpenHook : IGameHook, IObserves3
{
    private readonly DialogueControls controls;

    public DialogueWindowOpenHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        controls = services.DialogueControls;
        controls.TrackWindow();
    }

    public string SignatureName => "MsgOpenBody";

    public void Observe(nint name, nint second, nint third) => controls.WindowOpened();
}

/// <summary>Reports the dialogue window closing: <c>MsgClose</c>'s body, one 32-bit argument (not read).</summary>
[GameHook(GameBuilds.Steam18330018, OptInFile = DialogueWindowOptIn.File)]
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
