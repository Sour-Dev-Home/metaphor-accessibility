namespace Metaphor.Core.Input;

/// <summary>The kind of device the player last pressed something on (#623).</summary>
public enum InputDevice
{
    Controller,
    Keyboard,
}

/// <summary>Tracks the last-used input device from each pad and keyboard read: a keyboard read with any key down says
/// keyboard, a pad read with any button down says controller, and before any input it's the controller. Any of the
/// game's threads may read and write it.</summary>
public sealed class LastInputDevice
{
    private int device = (int)InputDevice.Controller;

    /// <summary>The device last pressed on; <see cref="InputDevice.Controller"/> before any input.</summary>
    public InputDevice Current => (InputDevice)Volatile.Read(ref device);

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
