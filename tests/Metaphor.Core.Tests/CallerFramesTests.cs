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

    [Theory]
    [InlineData(new byte[] { 0x90, 0x90, 0xE8, 0x10, 0x20, 0x30, 0x40 }, 5)] // call rel32
    [InlineData(new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0xFF, 0xD0 }, 2)] // call rax
    [InlineData(new byte[] { 0x90, 0x90, 0x90, 0x90, 0x41, 0xFF, 0xD3 }, 3)] // call r11
    [InlineData(new byte[] { 0x90, 0x90, 0x90, 0x90, 0xFF, 0x50, 0x20 }, 3)] // call [rax+20h]: a virtual call
    [InlineData(new byte[] { 0x90, 0x90, 0x90, 0xFF, 0x54, 0x24, 0x08 }, 4)] // call [rsp+8]
    [InlineData(new byte[] { 0x90, 0x90, 0x90, 0x41, 0xFF, 0x52, 0x08 }, 4)] // call [r10+8]
    [InlineData(new byte[] { 0x90, 0xFF, 0x15, 0x00, 0x10, 0x00, 0x00 }, 6)] // call [rip+disp32]
    [InlineData(new byte[] { 0x90, 0xFF, 0x90, 0x00, 0x01, 0x00, 0x00 }, 6)] // call [rax+100h]
    [InlineData(new byte[] { 0xFF, 0x94, 0x24, 0x00, 0x01, 0x00, 0x00 }, 7)] // call [rsp+100h]
    [InlineData(new byte[] { 0x48, 0xFF, 0x15, 0x00, 0x10, 0x00, 0x00 }, 7)] // call [rip+disp32] with REX.W
    [InlineData(new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 }, 0)]
    [InlineData(new byte[] { 0x90, 0x90, 0x90, 0x90, 0xFF, 0x54, 0x24 }, 0)] // [rsp+disp8] needs its SIB byte
    public void RecognisesTheCallsBeforeAReturnAddress(byte[] before, int length) =>
        Assert.Equal(length, CallerFrames.CallLengthBefore(before));

    [Fact]
    public void BytesOfTheWrongLengthAreNoCall() => Assert.Equal(0, CallerFrames.CallLengthBefore([0xE8, 0, 0, 0, 0]));

    [Fact]
    public void TheStackGivesModuleValuesThatFollowACallInOrder()
    {
        // Three return addresses after calls, one value in the module after no call, one outside it.
        long[] values = [0x7ff0_0000_1000, ModuleBase + 0x2000, ModuleBase + 0x3000, 42, ModuleBase + 0x4000, ModuleBase + 0x5000];
        var stack = values.SelectMany(BitConverter.GetBytes).ToArray();
        var code = new Dictionary<long, byte[]>
        {
            [0x2000 - 7] = [0x90, 0x90, 0xE8, 0x00, 0xF0, 0xFF, 0xFF], // call rel32 to 0x1000
            [0x3000 - 7] = [0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90],
            [0x4000 - 7] = [0x90, 0x90, 0x90, 0x90, 0xFF, 0x50, 0x20],
            [0x5000 - 7] = [0x90, 0x90, 0x90, 0x90, 0x90, 0xFF, 0xD0],
        };

        var found = CallerFrames.ReturnAddressesOnStack(stack, ModuleBase, ModuleSize, (rva, buffer) =>
        {
            code[rva].CopyTo(buffer, 0);
            return true;
        });

        Assert.Equal([(0x2000L, (long?)0x1000), (0x4000L, null), (0x5000L, null)], found);
    }

    [Fact]
    public void TheStackGivesAtMostThreeAndSkipsUnreadableCode()
    {
        long[] values = [ModuleBase + 0x10, ModuleBase + 0x20, ModuleBase + 0x30, ModuleBase + 0x40, ModuleBase + 0x50];
        var stack = values.SelectMany(BitConverter.GetBytes).ToArray();

        var found = CallerFrames.ReturnAddressesOnStack(stack, ModuleBase, ModuleSize, (rva, buffer) =>
        {
            byte[] call = [0x90, 0x90, 0x90, 0x90, 0x90, 0xFF, 0xD0];
            call.CopyTo(buffer, 0);
            return rva != 0x10 - 7;
        });

        Assert.Equal([0x20L, 0x30L, 0x40L], found.Select(c => c.ReturnRva));
    }

    [Fact]
    public void ATraceWithSeveralCandidatesListsTheRestAfterTheFirst() => Assert.Equal(
        "caller=0x2000 call=0x1000 via=scan frames=3 more=0x4000>(unknown),0x5000>0x10",
        new CallerTrace([(0x2000, 0x1000), (0x4000, null), (0x5000, 0x10)], "scan", 3).ToString());
}
