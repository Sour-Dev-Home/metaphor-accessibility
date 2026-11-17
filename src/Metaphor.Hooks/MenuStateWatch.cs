using Metaphor.Core;
using Metaphor.Core.Field;
using Metaphor.Core.Menus;

namespace Metaphor.Hooks;

/// <summary>Reads, without a hook of its own, whether the camp menu is open as the game itself tests it (#250, under
/// #211): the test behind the script function <c>CampStatus_IsClosed</c>, replayed by reading
/// (<see cref="MenuOpenState"/>). Two game functions in <c>.ecode</c> load the globals rip-relatively; their signatures
/// find them. Nothing is called or hooked. Numbers and class names only.</summary>
/// <remarks>Carried by <c>PadReadHook</c> (<c>InputHooks.cs</c>), Debug only, under <see cref="FieldWatch.OptInFile"/>.
/// </remarks>
internal sealed class MenuStateWatch
{
    /// <summary>The function <c>CampStatus_IsClosed</c> reaches: <c>mov rcx,[rip+disp32]</c> (7 bytes, at <c>+9</c>)
    /// loads the camp handle.</summary>
    public const string HandleReader = "CampStatusIsOpenBody";

    /// <summary>The function that tests a handle against the live lists: <c>mov rax,[rip+disp32]</c> at <c>+0x16</c>
    /// (kind 2) and <c>+0x43</c> (kind 3), <c>cmove rax,[rip+disp32]</c> (8 bytes) at <c>+0x4D</c> (kind 1).</summary>
    public const string AliveTest = "TaskHandleAlive";

    private const long ReadMilliseconds = 100;

    private readonly MessageLog log;
    private readonly CallerAddress module = new();
    private readonly InputStateChanges changes = new();
    private readonly IntervalGate gate = new(ReadMilliseconds, () => Environment.TickCount64);
    private readonly Dictionary<int, nint> headByKind = [];
    private nint handleGlobal;

    public MenuStateWatch(MessageLog log) => this.log = log;

    public static IReadOnlyList<string> Signatures => [HandleReader, AliveTest];

    /// <summary>Finds the handle's global and the three list heads from the functions the loader located.</summary>
    public void Resolve(IReadOnlyDictionary<string, nint> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);
        handleGlobal = RipRelativeTarget(functions[HandleReader] + 9, operandAt: 3, length: 7);
        var alive = functions[AliveTest];
        headByKind[2] = RipRelativeTarget(alive + 0x16, operandAt: 3, length: 7);
        headByKind[3] = RipRelativeTarget(alive + 0x43, operandAt: 3, length: 7);
        headByKind[1] = RipRelativeTarget(alive + 0x4D, operandAt: 4, length: 8);
    }

    /// <summary>Called on every pad read: reads and logs the state when due and changed.</summary>
    public void Tick()
    {
        if (!gate.Due())
        {
            return;
        }
        var state = Read();
        if (changes.Changed("menu-state", state.ToString()))
        {
            log.MenuStateRead(state);
        }
    }

    private MenuOpenState Read()
    {
        if (handleGlobal == 0 || ModuleMemoryReader.ReadPointer(handleGlobal) is not { } handle || handle == 0)
        {
            return new MenuOpenState(0, null, null, null, null, null);
        }
        var kind = ModuleMemoryReader.ReadInt32(handle + MenuOpenState.KindOffset);
        var id = ModuleMemoryReader.ReadPointer(handle + MenuOpenState.IdOffset);
        bool? alive = null;
        string? nodeClass = null;
        if (kind is { } k && id is { } i)
        {
            if (MenuOpenState.NextOffset(k) is not { } next)
            {
                alive = false;
            }
            else if (headByKind.TryGetValue(k, out var headGlobal) && headGlobal != 0 &&
                ModuleMemoryReader.ReadPointer(headGlobal) is { } head &&
                MenuOpenState.FindNode(head, next, i, address => ModuleMemoryReader.ReadPointer((nint)address)) is { } node)
            {
                alive = node != 0;
                nodeClass = node != 0 ? ClassNameOf((nint)node) : null;
            }
        }
        return new MenuOpenState(handle, ClassNameOf(handle), kind, id, alive, nodeClass);
    }

    private string? ClassNameOf(nint instance) => RttiName.Of(
        instance,
        module.ModuleBase,
        module.ModuleSize,
        address => ModuleMemoryReader.ReadPointer((nint)address),
        address => ModuleMemoryReader.ReadInt32((nint)address),
        address => ModuleMemoryReader.ReadAnsiString((nint)address, 256));

    private static nint RipRelativeTarget(nint instruction, int operandAt, int length) =>
        ModuleMemoryReader.ReadInt32(instruction + operandAt) is { } displacement ? instruction + length + displacement : 0;
}
