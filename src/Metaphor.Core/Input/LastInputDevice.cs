namespace Metaphor.Core.Input;

/// <summary>The kind of device the player last pressed something on (#623).</summary>
public enum InputDevice
{
    Controller,
    Keyboard,
}

/// <summary>Tracks the last-used input device from each pad and keyboard read: a keyboard read with any key down says
/// keyboard, a pad read with any button down says controller, and before any input it's the controller. When the game's
/// own glyph device can be read and names a device (<see cref="GameGlyphDevice"/>), that wins, so the words name the
/// buttons the screen draws (#778). Any of the game's threads may read and write it.</summary>
public sealed class LastInputDevice
{
    private readonly Func<int?>? gameDevice;
    private int device = (int)InputDevice.Controller;

    /// <summary>A tracker of the mod's own reads alone.</summary>
    public LastInputDevice()
    {
    }

    /// <summary>A tracker that asks <paramref name="gameDevice"/>, the game's glyph device value or null when
    /// unreadable, first.</summary>
    public LastInputDevice(Func<int?> gameDevice)
    {
        ArgumentNullException.ThrowIfNull(gameDevice);
        this.gameDevice = gameDevice;
    }

    /// <summary>The device last pressed on; <see cref="InputDevice.Controller"/> before any input.</summary>
    public InputDevice Current =>
        GameGlyphDevice.Of(gameDevice?.Invoke()) ?? (InputDevice)Volatile.Read(ref device);

    /// <summary>Takes one pad read's buttons.</summary>
    public void Pad(int buttons)
    {
        if (buttons != 0)
        {
            Volatile.Write(ref device, (int)InputDevice.Controller);
        }
    }

    /// <summary>Takes one keyboard read's keys held down.</summary>
    public void Keys(IReadOnlyList<int> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count > 0)
        {
            Volatile.Write(ref device, (int)InputDevice.Keyboard);
        }
    }
}

/// <summary>The game's glyph device global, <c>base+0x27a8bc0</c> (u32), which picks key or pad glyphs: build 548ddc95
/// (Steam 18330018), static in docs/findings/game-over-component-and-input-device-static.md, read live on 2026-11-23
/// (its "Live check" section). Under Button Display Auto it reads 0 after pad input and 2 after keyboard input; a fixed
/// Button Display reads 1 whatever the device, so 1 names none.</summary>
public static class GameGlyphDevice
{
    /// <summary>The device a value names: 0 the controller, 2 the keyboard, anything else (1, unreadable) none.</summary>
    public static InputDevice? Of(int? value) => value switch
    {
        0 => InputDevice.Controller,
        2 => InputDevice.Keyboard,
        _ => null,
    };
}
