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

    // #778: the game drew key glyphs while the mod's last device still said the controller ("Press A" on a keyboard).
    [Fact]
    public void TheGamesGlyphDeviceTwoMakesItTheKeyboardWhateverTheModLastSaw()
    {
        var device = new LastInputDevice(() => 2);
        device.Pad(0x1);

        Assert.Equal(InputDevice.Keyboard, device.Current);
    }

    [Fact]
    public void TheGamesGlyphDeviceZeroMakesItTheController()
    {
        var device = new LastInputDevice(() => 0);
        device.Keys([0x20]);

        Assert.Equal(InputDevice.Controller, device.Current);
    }

    // A fixed Button Display reads 1 whatever the device, and an unread global gives null: the mod's reads decide.
    [Theory]
    [InlineData(1)]
    [InlineData(null)]
    [InlineData(7)]
    public void AGlyphDeviceThatNamesNoneLeavesTheModsLastDevice(int? value)
    {
        var device = new LastInputDevice(() => value);
        device.Keys([0x20]);

        Assert.Equal(InputDevice.Keyboard, device.Current);
    }

    [Fact]
    public void TheGlyphDeviceIsReadEachTimeItIsAsked()
    {
        int? value = 0;
        var device = new LastInputDevice(() => value);
        Assert.Equal(InputDevice.Controller, device.Current);

        value = 2;

        Assert.Equal(InputDevice.Keyboard, device.Current);
    }
}
