using System.Diagnostics;
using Metaphor.Core.Menus;

namespace Metaphor.Hooks;

// Menu cursor events (#94): one hook decodes each input a menu's cursor manager handles and hands it to
// HookServices.Cursors, whose observers speak the title menu's focus and, in Debug builds, log it. The loader's adapter
// passes every call through unchanged (#88).

/// <summary>Reads whether the game has its menus open (#268): the global <c>CampStatus_IsClosed</c> tests is non-null
/// exactly while a field menu, or the title screen, is open (docs/raw-sources/menus-open-session6-2026-11-17.md, game
/// build 548ddc95…, Steam build 18330018). One pointer read through <see cref="ModuleMemoryReader"/> per call, no list
/// walk. The global's address comes from the code of the function that loads it, <c>CampStatusIsOpenBody</c> in
/// <c>.ecode</c>, which the loader locates for <see cref="PadReadHook"/> (<see cref="IGameHook.Calls"/>, so only after
/// the build check); that function is read, never hooked or called. Until then, and when a read fails, the menus read
/// as closed, so the menu controls hand their keys to the game.</summary>
internal sealed class MenusOpenRead
{
    /// <summary>The signature of the function whose <c>mov rcx,[rip+disp32]</c> at <c>+9</c> loads the global.</summary>
    public const string HandleReader = "CampStatusIsOpenBody";

    private const int LoadOffset = 9;
    private const int DisplacementOffset = 3;
    private const int LoadLength = 7;

    // Written once, before the pad read goes live. A pointer-sized field is read whole, so another thread sees 0, as
    // closed, or the address.
    private nint global;

    /// <summary>Finds the global from the address the loader located for <see cref="HandleReader"/>. Called once,
    /// before the hook that carries it goes live.</summary>
    public void Resolve(IReadOnlyDictionary<string, nint> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);
        if (!functions.TryGetValue(HandleReader, out var function))
        {
            return;
        }
        var load = function + LoadOffset;
        if (ModuleMemoryReader.ReadInt32(load + DisplacementOffset) is { } displacement)
        {
            global = load + LoadLength + displacement;
        }
    }

    /// <summary>Whether the menus are open now: false when the global is unknown or unreadable.</summary>
    public bool IsOpen() =>
        global is var at && at != 0 && ModuleMemoryReader.ReadPointer(at) is { } handle && handle != 0;
}

/// <summary>Reports each input the game's shared menu cursor handler takes: <c>int (CursorManagerBase* this, const
/// int* command)</c>, shared by four cursor manager classes, which moves the cursor for commands <c>0x14</c>–<c>0x17</c>
/// and puts it back if the move is refused. The function, its arguments and the field offsets are our reading of the
/// exe (docs/raw-sources/ui-focus-candidates-2026-11-16.md); that the index follows the menu's focus is from
/// docs/raw-sources/spike-session-2-2026-11-16.md. Observed after it returns, so the cursor is where the input left it.
/// Each input carries its screen class from <see cref="ScreenClassLookup"/> (#208).</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class MenuCursorHook : IGameHook, IObserves4WithResult
{
    // The cursor's top (scroll) and row, whose sum the class's index getter returns, and the flags the handler tests.
    private const int TopOffset = 0x60;
    private const int RowOffset = 0x64;
    private const int FlagsOffset = 0xAC;

    private readonly CursorEvents events;
    private readonly ScreenClassLookup screenClasses;
    private readonly nint? moduleBase;

    public MenuCursorHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        events = services.Cursors;
        screenClasses = services.ScreenClasses;
        using var process = Process.GetCurrentProcess();
        // Without the main module the class is logged as unreadable; the loader isn't stopped.
        moduleBase = process.MainModule?.BaseAddress;
    }

    public string SignatureName => "CursorManagerInput";

    /// <summary>Only the low 32 bits of the result are read. An input whose command or cursor can't be read is
    /// skipped.</summary>
    public void Observe(nint manager, nint command, nint third, nint fourth, nint result)
    {
        if (ModuleMemoryReader.ReadInt32(command) is not { } commandValue ||
            ModuleMemoryReader.ReadInt32(manager + TopOffset) is not { } top ||
            ModuleMemoryReader.ReadInt32(manager + RowOffset) is not { } row)
        {
            return;
        }
        long? classRva = moduleBase is { } module && ModuleMemoryReader.ReadPointer(manager) is { } vtable
            ? vtable - module
            : null;
        events.Handled(new CursorInput(
            manager, classRva, commandValue, (int)result, top, row, ModuleMemoryReader.ReadInt32(manager + FlagsOffset) ?? 0,
            screenClasses.Of(manager)));
    }
}
