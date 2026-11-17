using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Metaphor.Core;
using Metaphor.Features.Dialogue;
using Reloaded.Hooks.Definitions;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace Metaphor.Hooks;

// The dialogue controls' input (#58): hooks on the game's own per-frame reads of a pad and of the keyboard. They change
// what the game sees, so they are written the long way (architecture rule 14's exception, IChangesBehaviour): each
// calls the original, reads the dialogue window's message pointer (DialogueWindowPointer, #232), hands both to
// DialogueControls, and while the dialogue window is open writes the state back without the bound buttons or keys.
// Each asks the loader for the pointer's signature, so it goes in only when that matched once too; without it the
// controls never see a window and leave every press to the game (#206). Every read goes through ModuleMemoryReader (rule 10); the one write is
// WriteProcessMemory on this process into the state the original has just filled, and only clears what it wrote.
// WriteProcessMemory fails instead of faulting, but it also writes through read-only pages, so it is safe only on memory
// known to be the game's own writable data, never on an arbitrary pointer. In Debug builds each change of a read is
// logged, as #58's input spike did (numbers only, no game text). The functions, their arguments and the names (ours)
// are in docs/raw-sources/input-route-2026-11-16.md; the layout of what they write is in
// docs/raw-sources/pad-and-keyboard-state-2026-11-16.md.

/// <summary>The pad read: <c>(32-bit pad number, pointer to the game's pad state) → al</c>; it writes the game's own
/// 16-bit button mask at <c>+0</c>.</summary>
[GameHook(GameBuilds.Steam18330018)]
[SuppressMessage("Design", "CA1031", Justification = "An exception must never cross back into the game.")]
internal sealed class PadReadHook : IGameHook, IChangesBehaviour
{
    // Both arguments pointer-sized (rule 9): the pad number's upper half passes through as the game left it.
    private delegate nint Function(nint pad, nint state);

    private static readonly nint CurrentProcess = -1;

    private readonly DialogueControls controls;
    private readonly MessageLog? log;
    private readonly InputStateChanges changes = new();
    private readonly DialogueWindowPointer window = new();
    private readonly Function callback;
    private IHook<Function>? hook;

    public PadReadHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        controls = services.DialogueControls;
        log = services.Log;
        callback = OnCall;
    }

    public string SignatureName => "PadRead";

    public IReadOnlyList<string> Calls => DialogueWindowPointer.Signatures;

    public void UseCalls(IReadOnlyDictionary<string, nint> calls) => window.Resolve(calls);

    public void Activate(IReloadedHooks hooks, nint address)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        // Stored before it goes live (rule 11).
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    // https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-writeprocessmemory
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(nint process, nint address, byte[] buffer, nuint size, out nuint written);

    private nint OnCall(nint pad, nint state)
    {
        var result = hook!.OriginalFunction(pad, state);
        try
        {
            var current = window.Read();
            controls.WindowRead(current);
            if (log is not null && changes.Changed("msgwindow", current?.ToString(CultureInfo.InvariantCulture) ?? "?"))
            {
                log.WindowRead(current);
            }
            if (ModuleMemoryReader.ReadInt32(state) is { } value)
            {
                var buttons = value & 0xFFFF;
                var handed = controls.Pad((int)pad, buttons);
                if (handed != buttons)
                {
                    var mask = new byte[2];
                    BinaryPrimitives.WriteUInt16LittleEndian(mask, (ushort)handed);
                    WriteProcessMemory(CurrentProcess, state, mask, (nuint)mask.Length, out _);
                }
                if (log is not null)
                {
                    var source = string.Create(CultureInfo.InvariantCulture, $"pad{(int)pad}");
                    var text = string.Create(CultureInfo.InvariantCulture,
                        $"buttons=0x{buttons:x4} handed=0x{handed:x4} returned={result & 0xFF}");
                    if (changes.Changed(source, text))
                    {
                        log.InputChanged(source, text);
                    }
                }
            }
        }
        catch (Exception)
        {
        }
        return result;
    }
}

/// <summary>The keyboard read: <c>(32-bit value, pointer to the game's 15-byte keyboard state) → al</c>; it writes a
/// count of held keys at <c>+4</c> and up to six key codes from <c>+8</c>.</summary>
[GameHook(GameBuilds.Steam18330018)]
[SuppressMessage("Design", "CA1031", Justification = "An exception must never cross back into the game.")]
internal sealed class KeyboardReadHook : IGameHook, IChangesBehaviour
{
    private delegate nint Function(nint first, nint state);

    private const int CountOffset = 4;
    private const int SlotsOffset = 8;

    private static readonly nint CurrentProcess = -1;

    private readonly DialogueControls controls;
    private readonly MessageLog? log;
    private readonly InputStateChanges changes = new();
    private readonly DialogueWindowPointer window = new();
    private readonly Function callback;
    private IHook<Function>? hook;

    public KeyboardReadHook(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        controls = services.DialogueControls;
        log = services.Log;
        callback = OnCall;
    }

    public string SignatureName => "KeyboardRead";

    public IReadOnlyList<string> Calls => DialogueWindowPointer.Signatures;

    public void UseCalls(IReadOnlyDictionary<string, nint> calls) => window.Resolve(calls);

    public void Activate(IReloadedHooks hooks, nint address)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        // Stored before it goes live (rule 11).
        hook = hooks.CreateHook(callback, address);
        hook.Activate();
    }

    // https://learn.microsoft.com/windows/win32/api/memoryapi/nf-memoryapi-writeprocessmemory
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(nint process, nint address, byte[] buffer, nuint size, out nuint written);

    private nint OnCall(nint first, nint state)
    {
        var result = hook!.OriginalFunction(first, state);
        try
        {
            // Read here too: the keyboard may be read on frames, or a thread, the pad isn't.
            controls.WindowRead(window.Read());
            var slots = new byte[GameKeyList.Slots];
            if (ModuleMemoryReader.ReadInt32(state + CountOffset) is { } count &&
                ModuleMemoryReader.ReadBytes(state + SlotsOffset, slots))
            {
                var keys = GameKeyList.Read(count, slots);
                var handed = controls.Keys(keys);
                if (handed.Count != keys.Count)
                {
                    // The count and the slots after it, in one write.
                    var written = new byte[SlotsOffset - CountOffset + GameKeyList.Slots];
                    BinaryPrimitives.WriteInt32LittleEndian(written, handed.Count);
                    GameKeyList.Write(handed).CopyTo(written, SlotsOffset - CountOffset);
                    WriteProcessMemory(CurrentProcess, state + CountOffset, written, (nuint)written.Length, out _);
                }
                if (log is not null)
                {
                    var text = string.Create(CultureInfo.InvariantCulture,
                        $"keys=0x{ModuleMemoryReader.ReadInt32(state) ?? 0:x8} held={Codes(keys)} handed={Codes(handed)} returned={result & 0xFF}");
                    if (changes.Changed("keyboard", text))
                    {
                        log.InputChanged("keyboard", text);
                    }
                }
            }
        }
        catch (Exception)
        {
        }
        return result;
    }

    private static string Codes(IReadOnlyList<int> keys) =>
        keys.Count == 0 ? "-" : string.Join(',', keys.Select(key => key.ToString("x2", CultureInfo.InvariantCulture)));
}
