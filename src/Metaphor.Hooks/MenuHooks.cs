using System.Diagnostics;
using Metaphor.Core.Menus;

namespace Metaphor.Hooks;

// Menu cursor events (#94): one hook decodes each input a menu's cursor manager handles and hands it to
// HookServices.Cursors, whose observers speak the title menu's focus and, in Debug builds, log it. The loader's adapter
// passes every call through unchanged (#88).

/// <summary>Reports each input the game's shared menu cursor handler takes: <c>int (CursorManagerBase* this, const
/// int* command)</c>, shared by four cursor manager classes, which moves the cursor for commands <c>0x14</c>–<c>0x17</c>
/// and puts it back if the move is refused. The function, its arguments and the field offsets are our reading of the
/// exe (docs/raw-sources/ui-focus-candidates-2026-11-16.md); that the index follows the menu's focus is from
/// docs/raw-sources/spike-session-2-2026-11-16.md. Observed after it returns, so the cursor is where the input left it.
/// </summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class MenuCursorHook : IGameHook, IObserves4WithResult
{
    // The cursor's top (scroll) and row, whose sum the class's index getter returns, and the flags the handler tests.
    private const int TopOffset = 0x60;
    private const int RowOffset = 0x64;
    private const int FlagsOffset = 0xAC;

    private readonly CursorEvents events;
    private readonly nint? moduleBase;

    public MenuCursorHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        events = services.Cursors;
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
            manager, classRva, commandValue, (int)result, top, row, ModuleMemoryReader.ReadInt32(manager + FlagsOffset) ?? 0));
    }
}
