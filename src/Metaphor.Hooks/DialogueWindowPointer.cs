using Metaphor.Core;

namespace Metaphor.Hooks;

/// <summary>A reader of one game global the loader locates on its own (#300): it declares the global as data and takes
/// its address, once, before any hook goes live. Until then, or when the global wasn't located, it reads as unknown.
/// </summary>
internal interface IReadsGlobal
{
    /// <summary>The global it reads: its signature and where the load's <c>disp32</c> sits.</summary>
    GlobalSpec Global { get; }

    /// <summary>Hands it the global's address. Called at most once, only when the global was located, and before any
    /// hook goes in.</summary>
    void Locate(nint address);
}

/// <summary>Reads, without a hook of its own, the message the game's dialogue window holds (#232): <c>[W+0x60]</c>,
/// non-zero while a line or a choice is up and 0 once the window closes. It replaces the <c>.text1</c> window hooks as
/// the dialogue controls' window state, since those follow message handles, not the window (58 of 58 windows against
/// 21 of 58 in docs/raw-sources/dialogue-window-flag-session5-2026-11-17.md). The global <c>W</c> is loaded by the first
/// instruction of the <c>.ecode</c> function <c>MsgIsSelectWindowOpened</c> jumps to, <c>mov rcx,[rip+disp32]</c>
/// (7 bytes), which the loader locates by its signature and only reads, never hooks or calls
/// (docs/raw-sources/dialogue-window-flag-2026-11-16.md). Game build 548ddc95… (Steam build 18330018). Every read goes
/// through <see cref="ModuleMemoryReader"/>; nothing is written.</summary>
internal sealed class DialogueWindowPointer : IReadsGlobal
{
    private const int CurrentMessage = 0x60;

    private static readonly GlobalSpec Spec = new(
        "dialogue window", [GameBuilds.Steam18330018], "MsgIsSelectWindowOpenedBody",
        LoadOffset: 0, DisplacementOffset: 3, InstructionLength: 7, TurnsOff: "dialogue controls");

    // Set once by the loader before any hook goes live; a pointer-sized field is read and written whole.
    private nint windowGlobal;

    public GlobalSpec Global => Spec;

    public void Locate(nint address) => windowGlobal = address;

    /// <summary>The message the window holds, 0 when none, or null when the global is unknown, the window object doesn't
    /// exist yet, or a read failed.</summary>
    public long? Read()
    {
        if (windowGlobal == 0 || ModuleMemoryReader.ReadPointer(windowGlobal) is not { } window || window == 0)
        {
            return null;
        }
        return ModuleMemoryReader.ReadPointer(window + CurrentMessage);
    }
}
