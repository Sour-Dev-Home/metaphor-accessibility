using System.Globalization;
using Metaphor.Core;

namespace Metaphor.Hooks;

// Issue #93, second sitting: development-only hooks to find where menu focus lives. The functions, their arguments and
// the field offsets are our reading of the exe on one build (docs/raw-sources/ui-focus-candidates-2026-11-16.md); the
// names are the game's own class names from its run-time type information, with the function's role ours. Every
// argument is observed pointer-sized (rule 9) and narrowed only for the log.

/// <summary>The input handler shared by four of the game's menu cursor classes (<c>app::ui::ope::CursorManagerBase</c>
/// and the <c>CursorManager</c>, <c>MultiLineCursorManager</c> and <c>ComplexCursorManagerNoScroll</c> derived from it):
/// <c>int (CursorManagerBase* this, const int* command)</c>, which moves the cursor for commands <c>0x14</c>–<c>0x17</c>
/// and puts it back if the move is refused. Observed after it returns, so the cursor logged is where the input left
/// it. A call is logged only when the command, result or cursor differs from that manager's last one.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class CursorManagerInputListener : IGameHook, IObserves4WithResult
{
    // Offsets read from the handler and the class's index getter (the raw source above).
    private const int TopOffset = 0x60;
    private const int RowOffset = 0x64;
    private const int FlagsOffset = 0xAC;

    private readonly MessageLog? log;
    private readonly CallerAddress caller = new();
    private readonly ChangeGate gate = new();

    public CursorManagerInputListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "CursorManagerInput";

    public void Observe(nint manager, nint command, nint third, nint fourth, nint result)
    {
        if (log is null)
        {
            return;
        }
        var commandValue = ModuleMemoryReader.ReadInt32(command);
        var top = ModuleMemoryReader.ReadInt32(manager + TopOffset);
        var row = ModuleMemoryReader.ReadInt32(manager + RowOffset);
        var state = string.Create(CultureInfo.InvariantCulture, $"{commandValue}|{(int)result}|{top}|{row}");
        if (!gate.Changed(manager, System.Text.Encoding.ASCII.GetBytes(state)))
        {
            return;
        }
        var vtable = ModuleMemoryReader.ReadPointer(manager);
        long? classRva = vtable is { } v && v >= caller.ModuleBase && v - caller.ModuleBase < caller.ModuleSize
            ? v - caller.ModuleBase
            : null;
        log.CursorInputHandled(new CursorInput(
            manager, classRva, commandValue, (int)result, top, row,
            ModuleMemoryReader.ReadInt32(manager + FlagsOffset), caller.Find()));
    }
}

/// <summary>The draw of the game's UI text pane (<c>app::ui::TextPane</c>, its 22nd virtual function):
/// <c>void (TextPane* this)</c>. It reads the pane's colour, position and text element and hands them to the text
/// element's draw, which another pane class (the title's licence text) shares. It runs for every visible text pane
/// every frame, so a pane is logged only when its draw state changes, with any text a probe finds behind it, to tell a
/// highlighted item by its colour (the lead from Persona-4-Golden-Access's <c>UiTextSpy</c>,
/// docs/raw-sources/persona-4-golden-access-2026-11-16.md). Opt-in: a hook on a per-frame draw is the riskiest of the
/// sitting's, and its log holds game text, which stays in the local log.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true, OptInFile = "opt-in-ui-text-panes.txt")]
internal sealed class TextPaneDrawListener : IGameHook, IObserves3
{
    // How much of the pane and of its text element the text probe looks through.
    private const int ProbeBytes = 0x100;

    private readonly MessageLog? log;
    private readonly CallerAddress module = new();
    private readonly ChangeGate gate = new();

    public TextPaneDrawListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "TextPaneDraw";

    public void Observe(nint pane, nint second, nint third)
    {
        if (log is null)
        {
            return;
        }
        var bytes = new byte[PaneDrawn.Length];
        if (!ModuleMemoryReader.ReadBytes(pane, bytes) || !gate.Changed(pane, PaneDrawn.StateKey(bytes)) ||
            PaneDrawn.Read(pane, module.ModuleBase, module.ModuleSize, bytes) is not { } drawn)
        {
            return;
        }
        var found = Probe(pane, "pane");
        if (found.Text is null && drawn.Element != 0)
        {
            found = Probe((nint)drawn.Element, "elem");
        }
        log.PaneDrawn(drawn, found.At, found.Text);
    }

    private static (string? At, string? Text) Probe(nint target, string name)
    {
        var fields = new byte[ProbeBytes];
        if (!ModuleMemoryReader.ReadBytes(target, fields) ||
            TextProbe.FirstString(fields, address => ModuleMemoryReader.ReadAnsiString((nint)address)) is not { } found)
        {
            return (null, null);
        }
        return (string.Create(CultureInfo.InvariantCulture, $"{name}+0x{found.Offset:x}"), found.Text);
    }
}
