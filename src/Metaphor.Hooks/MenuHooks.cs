using System.Diagnostics;
using Metaphor.Core;
using Metaphor.Core.Menus;

namespace Metaphor.Hooks;

// Menu cursor events (#94): one hook decodes each input a menu's cursor manager handles and hands it to
// HookServices.Cursors, whose observers are MenuScreens (#211), which turns them into the screen changes the menu
// features speak, and the dialogue's ChoiceFocus, plus the report and the log when they are on. The loader's adapter
// passes every call through unchanged (#88).

/// <summary>Reads whether the game has its menus open (#268): the global <c>CampStatus_IsClosed</c> tests is non-null
/// exactly while a field menu, or the title screen, is open (docs/raw-sources/menus-open-session6-2026-11-17.md, game
/// build 548ddc95…, Steam build 18330018). One pointer read through <see cref="ModuleMemoryReader"/> per call, no list
/// walk. The global's address comes from the code that loads it: the <c>mov rcx,[rip+disp32]</c> (7 bytes) at
/// <c>+9</c> in <c>CampStatusIsOpenBody</c>, in <c>.ecode</c>, which the loader locates on its own (#300, after the
/// build check) and only reads, never hooks or calls. Until then, when it wasn't located, and when a read fails, the
/// menus read as closed, so the menu controls hand their keys to the game.</summary>
internal sealed class MenusOpenRead : IReadsGlobal
{
    private static readonly GlobalSpec Spec = new(
        "menus-open flag", [GameBuilds.Steam18330018], "CampStatusIsOpenBody",
        LoadOffset: 9, DisplacementOffset: 3, InstructionLength: 7, TurnsOff: "menu controls");

    // Set once by the loader before any hook goes live. A pointer-sized field is read and written whole, so another
    // thread sees 0, as closed, or the address.
    private nint global;

    public GlobalSpec Global => Spec;

    public void Locate(nint address) => global = address;

    /// <summary>Whether the menus are open now: false when the global is unknown or unreadable.</summary>
    public bool IsOpen() =>
        global is var at && at != 0 && ModuleMemoryReader.ReadPointer(at) is { } handle && handle != 0;
}

/// <summary>Reports each input the game's shared menu cursor handler takes: <c>int (CursorManagerBase* this, const
/// int* command)</c>, shared by four cursor manager classes, which moves the cursor for commands <c>0x14</c>–<c>0x17</c>
/// and puts it back if the move is refused. The function, its arguments and the field offsets are our reading of the
/// exe (docs/raw-sources/ui-focus-candidates-2026-11-16.md); that the index follows the menu's focus is from
/// docs/raw-sources/spike-session-2-2026-11-16.md. Observed after it returns, so the cursor is where the input left it.
/// Each input carries its screen class from <see cref="ScreenClassLookup"/> (#208), its list's item count (#289) and its
/// owner's mode for a choice that serves two lists (#210), each null when unreadable, and on the grid cursor class its
/// cell (#563), whose index is that class's own getter's, on Key Configuration whether its OK button has the focus
/// (#589), and on a settings page its values (#96).</summary>
[GameHook(GameBuilds.Steam18330018)]
internal sealed class MenuCursorHook : IGameHook, IObserves4WithResult
{
    // The cursor's top (scroll) and row, whose sum the class's index getter returns, and the flags the handler tests.
    private const int TopOffset = 0x60;
    private const int RowOffset = 0x64;
    private const int FlagsOffset = 0xAC;

    // The list's item count (#289), matched to the rows on screen on six screens; read per input, since a manager can be
    // reused by a later screen (docs/raw-sources/list-count-session6-2026-11-17.md, game build 548ddc95…).
    private const int CountOffset = 0x58;

    // The grid cursor's own fields (#563), read only on a manager of its class: cells to a row, column, row in view,
    // rows scrolled, page and page stride (docs/raw-sources/grid-cursor-fields-static-2026-11-21.md, game build
    // 548ddc95…).
    private const int GridColumnsOffset = 0xE0;
    private const int GridColumnOffset = 0xEC;
    private const int GridLineOffset = 0xF0;
    private const int GridScrollOffset = 0x64;
    private const int GridPageOffset = 0xF4;
    private const int GridPageStrideOffset = 0x54;

    private readonly CursorEvents events;
    private readonly ScreenClassLookup screenClasses;
    private readonly nint? moduleBase;
    private readonly MessageLog? log;
    private readonly ChangedBytes settingsCopies = new();
    private readonly ChangedBytes keyConfigStates = new();
    private readonly ChangedBytes ownerStates = new();

    public MenuCursorHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        events = services.Cursors;
        screenClasses = services.ScreenClasses;
        log = services.Log;
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
        var screenClass = screenClasses.Of(manager);
        var grid = classRva == GridCursor.ClassRva;
        var copy = ReadSettingsCopy(manager, screenClass);
        events.Handled(new CursorInput(
            manager, classRva, commandValue, (int)result, top, row, ModuleMemoryReader.ReadInt32(manager + FlagsOffset) ?? 0,
            screenClass, ModuleMemoryReader.ReadInt32(manager + CountOffset), screenClasses.ModeOf(manager, screenClass),
            grid ? ReadGrid(manager) : null,
            grid && screenClass == SettingsPages.KeyConfigClass ? KeyConfigOnButton(manager) : null,
            copy is { } read ? SettingValues.Of(read.Bytes) : null));
        LogSettingsCopy(copy);
        LogKeyConfigState(manager, screenClass);
        LogOwnerStates(manager, screenClass);
    }

    // Debug builds only (the log is null in Release): the fields OwnerStates names for the screen's choice, each span
    // when it changed since the choice's last input, so a session checks a static reading before a feature reads it
    // (#806, #624, #219, #851). One bounded read per span through ModuleMemoryReader, capped at OwnerStates.MaxLength;
    // nothing when the owner is unknown or a read fails.
    private void LogOwnerStates(nint manager, string? screenClass)
    {
        if (log is null || screenClass is null || OwnerStates.Of(screenClass) is not { Count: > 0 } spans
            || screenClasses.Owners.OwnerOf(manager) is not { } owner)
        {
            return;
        }
        foreach (var span in spans)
        {
            // An array, not stackalloc: a stack buffer's loads are raw-memory reads outside the reader (rule 10).
            var bytes = new byte[Math.Min(span.Length, OwnerStates.MaxLength)];
            if (ModuleMemoryReader.ReadBytes((nint)(owner + span.Offset), bytes)
                && ownerStates.Changed(owner + span.Offset, bytes) is { } hex)
            {
                log.OwnerState(screenClass, owner, span.Offset, hex);
            }
        }
    }

    // A settings page's working copy (#560), read so the page's rows can say their values (#96): only on one of the four
    // pages' classes, null on any other, when the owner is unknown or the read fails. One bounded read through
    // ModuleMemoryReader, which fails instead of faulting.
    private (long Owner, byte[] Bytes)? ReadSettingsCopy(nint manager, string? screenClass)
    {
        if (!SettingsPages.IsPage(screenClass) || screenClasses.Owners.OwnerOf(manager) is not { } owner)
        {
            return null;
        }
        // An array, not stackalloc: a stack buffer's loads are raw-memory reads outside the reader (architecture rule 10).
        var bytes = new byte[SettingsPages.WorkingCopyLength];
        return ModuleMemoryReader.ReadBytes((nint)(owner + SettingsPages.WorkingCopyOffset), bytes) ? (owner, bytes) : null;
    }

    // The grid cursor's cell, or null when a field is unreadable or they don't read as a grid. Six bounded reads
    // through ModuleMemoryReader, which fails instead of faulting.
    private static GridCursor? ReadGrid(nint manager) =>
        ModuleMemoryReader.ReadInt32(manager + GridColumnsOffset) is { } columns
            && ModuleMemoryReader.ReadInt32(manager + GridColumnOffset) is { } column
            && ModuleMemoryReader.ReadInt32(manager + GridLineOffset) is { } line
            && ModuleMemoryReader.ReadInt32(manager + GridScrollOffset) is { } scroll
            && ModuleMemoryReader.ReadInt32(manager + GridPageOffset) is { } page
            && ModuleMemoryReader.ReadInt32(manager + GridPageStrideOffset) is { } stride
            ? GridCursor.Of(columns, column, line, scroll, page, stride)
            : null;

    // Whether Key Configuration's OK button has the focus (#589): its choice's mode, where the flags' item bit isn't
    // reliable (SettingsPages.KeyConfigModeOffset). One bounded read through ModuleMemoryReader; an unknown owner or a
    // failed read counts as a cell.
    private bool KeyConfigOnButton(nint manager) =>
        SettingsPages.KeyConfigOnButton(screenClasses.Owners.OwnerOf(manager) is { } owner
            ? ModuleMemoryReader.ReadInt32((nint)(owner + SettingsPages.KeyConfigModeOffset))
            : null);

    // Debug builds only (the log is null in Release): Key Configuration's choice fields when they changed since its
    // last input, so a session shows which marks "waiting for a key" (#564, #558's check 7). One bounded read through
    // ModuleMemoryReader; nothing when the owner is unknown or the read fails.
    private void LogKeyConfigState(nint manager, string? screenClass)
    {
        if (log is null || screenClass != SettingsPages.KeyConfigClass
            || screenClasses.Owners.OwnerOf(manager) is not { } owner)
        {
            return;
        }
        // An array, not stackalloc: a stack buffer's loads are raw-memory reads outside the reader (architecture rule 10).
        var bytes = new byte[SettingsPages.KeyConfigStateLength];
        if (ModuleMemoryReader.ReadBytes((nint)(owner + SettingsPages.KeyConfigStateOffset), bytes)
            && keyConfigStates.Changed(owner, bytes) is { } hex)
        {
            log.KeyConfigState(owner, SettingsPages.KeyConfigStateOffset, hex);
        }
    }

    // Debug builds only (the log is null in Release): a settings page's working copy, when it changed since the page's
    // last input, so a session shows which bytes a value change moves (#560, #558's check 2); nothing when it wasn't read.
    private void LogSettingsCopy((long Owner, byte[] Bytes)? copy)
    {
        if (log is not null && copy is { } read && settingsCopies.Changed(read.Owner, read.Bytes) is { } hex)
        {
            log.SettingsCopy(read.Owner, hex);
        }
    }
}
