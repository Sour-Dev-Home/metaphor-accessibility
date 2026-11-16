using System.Globalization;
using Metaphor.Core;

namespace Metaphor.Hooks;

// Issue #58 (input spike): development-only hooks on the game's own per-frame reads of a pad and of the keyboard, to see
// which button and key bits change when the player presses something. Only changes are logged, and only bit masks, never
// game text. The functions, their arguments and the names (ours) are in docs/raw-sources/input-route-2026-11-16.md. Both
// are observed after they return (IObserves4WithResult), since they fill the struct their second argument points to;
// the two registers they don't read pass through unchanged.

/// <summary>The pad read: <c>(32-bit pad number, pointer to the game's pad state) → al</c>. It calls XInputGetState for
/// that pad and turns XInput's buttons into the game's own 16-bit mask at the start of the state.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class PadReadListener : IGameHook, IObserves4WithResult
{
    private readonly MessageLog? log;
    private readonly InputStateChanges changes = new();

    public PadReadListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "PadRead";

    public void Observe(nint pad, nint state, nint third, nint fourth, nint result)
    {
        if (log is null)
        {
            return;
        }
        var buttons = ModuleMemoryReader.ReadInt32(state) is { } value
            ? (value & 0xFFFF).ToString("x4", CultureInfo.InvariantCulture)
            : "(unreadable)";
        var source = string.Create(CultureInfo.InvariantCulture, $"pad{(int)pad}");
        var text = string.Create(CultureInfo.InvariantCulture, $"buttons=0x{buttons} returned={result & 0xFF}");
        if (changes.Changed(source, text))
        {
            log.InputChanged(source, text);
        }
    }
}

/// <summary>The keyboard read: <c>(32-bit value, pointer to the game's 15-byte keyboard state) → al</c>. It calls
/// GetKeyboardState and sets bits in the state's first 32 bits from virtual keys (Escape, the Ctrl, Shift and Alt
/// keys first); what the other 11 bytes hold is <c>[NEEDS VERIFICATION]</c>, so they are logged raw.</summary>
[GameHook(GameBuilds.Steam18330018, DevOnly = true)]
internal sealed class KeyboardReadListener : IGameHook, IObserves4WithResult
{
    private readonly MessageLog? log;
    private readonly InputStateChanges changes = new();

    public KeyboardReadListener(HookServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        log = services.Log;
    }

    public string SignatureName => "KeyboardRead";

    public void Observe(nint first, nint state, nint third, nint fourth, nint result)
    {
        if (log is null)
        {
            return;
        }
        int?[] words = [ModuleMemoryReader.ReadInt32(state), ModuleMemoryReader.ReadInt32(state + 4),
            ModuleMemoryReader.ReadInt32(state + 8), ModuleMemoryReader.ReadInt32(state + 12)];
        var text = words.Any(word => word is null)
            ? string.Create(CultureInfo.InvariantCulture, $"state=(unreadable) returned={result & 0xFF}")
            // Bytes 0x0C-0x0E are the state's last; the fourth read's top byte lies past it.
            : string.Create(CultureInfo.InvariantCulture,
                $"keys=0x{words[0]!.Value:x8} +4=0x{words[1]!.Value:x8} +8=0x{words[2]!.Value:x8} +c=0x{words[3]!.Value & 0xFFFFFF:x6} returned={result & 0xFF}");
        // The first argument's meaning is unknown, so it goes in the state, not the key: a value that changes each
        // frame then shows up in the log instead of growing the change filter.
        text = string.Create(CultureInfo.InvariantCulture, $"first={(int)first} {text}");
        if (changes.Changed("keyboard", text))
        {
            log.InputChanged("keyboard", text);
        }
    }
}
