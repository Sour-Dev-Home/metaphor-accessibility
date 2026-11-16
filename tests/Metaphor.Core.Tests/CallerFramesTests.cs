namespace Metaphor.Core.Tests;

/// <summary>Picking the hooked function's caller out of a stack walk (#93). Made-up addresses only.</summary>
[Trait("Category", "Unit")]
public sealed class CallerFramesTests
{
    private const long ModuleBase = 0x140000000;
    private const long ModuleSize = 0x20000000;

    [Fact]
    public void TheCallerIsTheFirstFrameInsideTheModule()
    {
        long[] frames = [0x7ff812340000, 0x7ff812350000, ModuleBase + 0x1234, ModuleBase + 0x5678];

        Assert.Equal(0x1234, CallerFrames.FirstInModule(frames, ModuleBase, ModuleSize));
    }

    [Fact]
    public void NoFrameInsideTheModuleMeansNoCaller()
    {
        long[] frames = [0x7ff812340000, ModuleBase - 1, ModuleBase + ModuleSize];

        Assert.Null(CallerFrames.FirstInModule(frames, ModuleBase, ModuleSize));
        Assert.Null(CallerFrames.FirstInModule([], ModuleBase, ModuleSize));
    }

    [Fact]
    public void TheModulesFirstByteCounts()
    {
        Assert.Equal(0, CallerFrames.FirstInModule([ModuleBase], ModuleBase, ModuleSize));
    }

    [Fact]
    public void ADirectCallBeforeTheReturnAddressNamesItsTarget()
    {
        // call rel32 with offset -0x100, returning to 0x2000: the target is 0x1f00.
        Assert.Equal(0x1f00, CallerFrames.DirectCallTarget([0xE8, 0x00, 0xFF, 0xFF, 0xFF], 0x2000));
        Assert.Equal(0x2010, CallerFrames.DirectCallTarget([0xE8, 0x10, 0x00, 0x00, 0x00], 0x2000));
    }

    [Fact]
    public void AnythingButADirectCallNamesNoTarget()
    {
        Assert.Null(CallerFrames.DirectCallTarget([0xFF, 0x15, 0x00, 0x00, 0x00], 0x2000));
        Assert.Null(CallerFrames.DirectCallTarget([0xE8, 0x00, 0x00, 0x00], 0x2000));
    }
}
