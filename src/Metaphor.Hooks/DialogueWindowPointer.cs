namespace Metaphor.Hooks;

/// <summary>Reads, without a hook of its own, the message the game's dialogue window holds (#232): <c>[W+0x60]</c>,
/// non-zero while a line or a choice is up and 0 once the window closes. It replaces the <c>.text1</c> window hooks as
/// the dialogue controls' window state, since those follow message handles, not the window (58 of 58 windows against
/// 21 of 58 in docs/raw-sources/dialogue-window-flag-session5-2026-11-17.md). The global <c>W</c> is found from the first
/// instruction of the <c>.ecode</c> function <c>MsgIsSelectWindowOpened</c> jumps to, <c>mov rcx,[rip+disp32]</c>
/// (7 bytes), which is located by its signature and only read, never hooked or called
/// (docs/raw-sources/dialogue-window-flag-2026-11-16.md). Game build 548ddc95… (Steam build 18330018), the build the
/// input hooks that carry it name. Every read goes through <see cref="ModuleMemoryReader"/>; nothing is written.
/// </summary>
internal sealed class DialogueWindowPointer
{
    /// <summary>The body of the script function <c>MsgIsSelectWindowOpened</c>.</summary>
    public const string WindowReader = "MsgIsSelectWindowOpenedBody";

    private const int DisplacementAt = 3;
    private const int InstructionLength = 7;
    private const int CurrentMessage = 0x60;

    private nint windowGlobal;

    /// <summary>The signatures a hook carrying the reader asks the loader for.</summary>
    public static IReadOnlyList<string> Signatures => [WindowReader];

    /// <summary>Finds the window's global from the function the loader located. Leaves it unknown when the instruction
    /// can't be read, and every <see cref="Read"/> then gives null.</summary>
    public void Resolve(IReadOnlyDictionary<string, nint> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);
        var instruction = functions[WindowReader];
        windowGlobal = ModuleMemoryReader.ReadInt32(instruction + DisplacementAt) is { } displacement
            ? instruction + InstructionLength + displacement
            : 0;
    }

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
