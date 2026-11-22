using Metaphor.Core.Input;

namespace Metaphor.Core.Tests.Input;

/// <summary>The last-used input device (#623).</summary>
[Trait("Category", "Unit")]
public sealed class LastInputDeviceTests
{
    [Fact]
    public void ItIsTheControllerBeforeAnyInput()
    {
        var device = new LastInputDevice();
        device.Pad(0);
        device.Keys([]);

        Assert.Equal(InputDevice.Controller, device.Current);
    }

    [Fact]
    public void AKeyDownMakesItTheKeyboardAndAButtonMakesItTheControllerAgain()
    {
        var device = new LastInputDevice();

        device.Keys([0x20]);
        Assert.Equal(InputDevice.Keyboard, device.Current);

        device.Pad(0x1);
        Assert.Equal(InputDevice.Controller, device.Current);
    }

    [Fact]
    public void AnEmptyReadKeepsTheLastDevice()
    {
        var device = new LastInputDevice();
        device.Keys([0x20]);

        device.Pad(0);

        Assert.Equal(InputDevice.Keyboard, device.Current);
    }
}
