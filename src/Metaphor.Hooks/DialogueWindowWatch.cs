using System.Globalization;

namespace Metaphor.Hooks;

/// <summary>Reads, without a hook of its own, whether the game's message window holds a message (#58): an alternative
/// to hooking <c>MsgOpen</c>/<c>MsgClose</c> in <c>.text1</c>. Two game functions in <c>.ecode</c> start by loading a
/// global pointer rip-relatively; their signatures find them, and the global's address is read from that instruction.
/// The fields read and what their values mean are in docs/raw-sources/dialogue-window-flag-2026-11-16.md, mostly
/// <c>[NEEDS VERIFICATION]</c> until a session logs them. Numbers and addresses only, no game text.</summary>
/// <remarks>Carried by <c>PadReadHook</c> (<c>InputHooks.cs</c>), Debug only, when <see cref="OptInFile"/> is in the
/// mod's folder (#206).</remarks>
internal sealed class DialogueWindowWatch
{
    /// <summary>The file in the mod's folder that turns the watch on.</summary>
    public const string OptInFile = "opt-in-dialogue-window-flag.txt";

    /// <summary>The body of the script function <c>MsgIsSelectWindowOpened</c>: <c>mov rcx,[rip+disp32]</c> (7 bytes)
    /// loads the message window object.</summary>
    public const string WindowReader = "MsgIsSelectWindowOpenedBody";

    /// <summary>The function the script function <c>MsgCheckEnd</c> calls: <c>cmp qword [rip+disp32],0</c> (8 bytes) tests the
    /// message manager that <c>MsgOpen</c> and <c>MsgClose</c> update.</summary>
    public const string ManagerReader = "MsgCheckEndBody";

    private const int CurrentMessage = 0x60;
    private const int WindowState = 0x560;
    private const int MessageField48 = 0x48;
    private const int HandlesById = 0x58;
    private const int HandlesByName = 0x18;

    private nint windowGlobal;
    private nint managerGlobal;

    public static IReadOnlyList<string> Signatures => [WindowReader, ManagerReader];

    /// <summary>Finds both globals from the functions the loader located. Leaves a global unknown (0) when its
    /// instruction can't be read.</summary>
    public void Resolve(IReadOnlyDictionary<string, nint> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);
        windowGlobal = RipRelativeTarget(functions[WindowReader], operandAt: 3, instructionLength: 7);
        managerGlobal = RipRelativeTarget(functions[ManagerReader], operandAt: 3, instructionLength: 8);
    }

    /// <summary>The state as one log field list, for a change filter: the window's current message pointer, its
    /// state number, the message's field at <c>+0x48</c>, and the manager's two handle counts.</summary>
    public string Describe()
    {
        var window = windowGlobal == 0 ? null : ModuleMemoryReader.ReadPointer(windowGlobal);
        var text = window switch
        {
            null => "window=(unreadable)",
            0 => "window=none",
            { } w => DescribeWindow(w),
        };
        var manager = managerGlobal == 0 ? null : ModuleMemoryReader.ReadPointer(managerGlobal);
        var handles = manager switch
        {
            null => "handles=(unreadable)",
            0 => "handles=none",
            { } m => string.Create(CultureInfo.InvariantCulture,
                $"handles={Number(ModuleMemoryReader.ReadPointer(m + HandlesById))}+{Number(ModuleMemoryReader.ReadPointer(m + HandlesByName))}"),
        };
        return text + " " + handles;
    }

    private static string DescribeWindow(nint window)
    {
        var current = ModuleMemoryReader.ReadPointer(window + CurrentMessage);
        var state = ModuleMemoryReader.ReadInt32(window + WindowState);
        var field48 = current is { } message and not 0 ? Number(ModuleMemoryReader.ReadInt32(message + MessageField48)) : "-";
        return string.Create(CultureInfo.InvariantCulture,
            $"current={(current is { } c ? $"0x{c:x}" : "?")} state={Number(state)} msg+48={field48}");
    }

    private static string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "?";

    /// <summary>The address a rip-relative operand names: the instruction's end plus its signed 32-bit displacement.</summary>
    private static nint RipRelativeTarget(nint instruction, int operandAt, int instructionLength) =>
        ModuleMemoryReader.ReadInt32(instruction + operandAt) is { } displacement
            ? instruction + instructionLength + displacement
            : 0;
}
