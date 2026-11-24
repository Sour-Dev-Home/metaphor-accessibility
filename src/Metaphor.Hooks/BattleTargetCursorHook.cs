using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Metaphor.Core;
using Metaphor.Core.Battle;
using Reloaded.Hooks.Definitions;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Metaphor.Hooks;

/// <summary>The battle target cursor's focus (#253): <c>void (UUIC_Battle_TargetCursor* this, uint slot)</c>, slot 4 of
/// the cursor's vtable, at <c>0xf26ec0</c> in <c>.ecode</c> on build 548ddc95… (Steam build 18330018). It is thirteen
/// bytes, too short to detour, and reached only through the vtable, so this hook swaps the vtable's slot instead (the
/// maintainer's decision of 2026-11-23 on #253): a behaviour-changing hook written the long way (ADR 0007,
/// architecture rule 14's exception). The vtable is at rva <c>0x2250280</c> in <c>.udata</c>, read-only data; its
/// constructor isn't in analysed code, so the rva is this build's, and nothing is written unless the loader's build
/// check passed, the slot-4 signature matched once, slot 4 holds that signature's address and the vtable's RTTI names
/// <c>UUIC_Battle_TargetCursor@ui@app</c> (docs/findings/battle-target-cursor-index-static.md). The one write is the
/// slot's 8 bytes, through <c>WriteProcessMemory</c>, which fails instead of faulting and writes through the read-only
/// page; it is read back, and the original pointer goes back on any failure and when the process exits
/// (<see cref="VtableSwap"/>). The mod can't be unloaded (<c>CanUnload</c> is false). Every call hands the slot to the
/// feature and then calls the original function, always. Nothing in <c>.text1</c> is read as code or touched.</summary>
[GameHook(GameBuilds.Steam18330018)]
[SuppressMessage("Design", "CA1031", Justification = "An exception must never cross back into the game.")]
internal sealed class BattleTargetCursorHook : IGameHook, IChangesBehaviour
{
    // Both pointer-sized (rule 9): the slot's upper half passes on as the game left it.
    private delegate nint Function(nint cursor, nint slot);

    private const long VtableRva = 0x2250280;
    private const int FocusSlot = 4;
    private const string CursorClass = "UUIC_Battle_TargetCursor@ui@app";
    private const int LongestClassName = 128;
    private const int LayoutAt = 0x18;
    private const int MeshesAt = 0x68;

    private static readonly nint CurrentProcess = -1;

    private readonly IBattleTargetObserver observer;
    private readonly MessageLog? log;
    private readonly Function callback;
    private readonly CallerAddress module = new();
    private Function? original;
    private IReverseWrapper<Function>? wrapper;
    private VtableSwap? swap;
    private nint lastCursor;
    private int lastSlot = -1;

    public BattleTargetCursorHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        observer = services.BattleTarget;
        log = services.Log;
        callback = OnCall;
    }

    public string SignatureName => "BattleTargetCursorFocus";

    public void Activate(IReloadedHooks hooks, nint address)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        // The original and the wrapper the slot will point at are stored before the slot changes (rule 11).
        original = hooks.CreateWrapper<Function>(address, out _);
        wrapper = hooks.CreateReverseWrapper(callback);
        swap = new VtableSwap(
            CursorClass, module.ModuleBase, module.ModuleSize,
            at => ModuleMemoryReader.ReadPointer((nint)at),
            at => ModuleMemoryReader.ReadInt32((nint)at),
            at => ModuleMemoryReader.ReadAnsiString((nint)at, LongestClassName),
            WritePointer);
        var result = swap.Swap(module.ModuleBase + VtableRva, FocusSlot, address, wrapper.WrapperPointer);
        if (result != VtableSwapResult.Swapped)
        {
            // The loader logs the hook as not installed.
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture, $"target cursor vtable slot not swapped: {result}"));
        }
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Restore();
    }

    private void Restore()
    {
        try
        {
            swap?.Restore();
        }
        catch (Exception)
        {
        }
    }

    // https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-writeprocessmemory
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(nint process, nint address, byte[] buffer, nuint size, out nuint written);

    // One aligned pointer into the vtable the swap has checked, never an address from the game.
    private static bool WritePointer(long address, long value)
    {
        var bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        return WriteProcessMemory(CurrentProcess, (nint)address, bytes, (nuint)bytes.Length, out var written) &&
            written == (nuint)bytes.Length;
    }

    private nint OnCall(nint cursor, nint slot)
    {
        try
        {
            var index = (int)(uint)slot;
            observer.Focused(cursor, index);
            if (log is not null)
            {
                Log(cursor, index);
            }
        }
        catch (Exception)
        {
        }
        return original!(cursor, slot);
    }

    // Debug builds: each change of cursor or slot, with how many meshes the cursor's layout has (6 or 10), for the
    // by-ear check of which enemy a slot is.
    private void Log(nint cursor, int index)
    {
        if (cursor == lastCursor && index == lastSlot)
        {
            return;
        }
        (lastCursor, lastSlot) = (cursor, index);
        var meshes = ModuleMemoryReader.ReadPointer(cursor + LayoutAt) is { } layout && layout != 0
            ? ModuleMemoryReader.ReadInt32(layout + MeshesAt)
            : null;
        log!.Requested(SignatureName, string.Create(CultureInfo.InvariantCulture,
            $"cursor=0x{cursor:x} slot={index} meshes={(meshes is { } m ? m.ToString(CultureInfo.InvariantCulture) : "?")}"));
    }
}
