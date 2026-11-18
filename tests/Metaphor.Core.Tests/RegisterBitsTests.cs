namespace Metaphor.Core.Tests;

/// <summary>A <c>float</c> argument read from an xmm register a hook observes as a <c>double</c> (#103).</summary>
[Trait("Category", "Unit")]
public sealed class RegisterBitsTests
{
    [Theory]
    [InlineData(0x3F000000u, 0.5f)]
    [InlineData(0x00000000u, 0f)]
    [InlineData(0x3F800000u, 1f)]
    public void TheFloatIsTheLowHalfWhateverTheUpperHalfHolds(uint floatBits, float expected)
    {
        // The upper half is whatever the register held before; only the low 32 bits are the float.
        var register = BitConverter.Int64BitsToDouble(unchecked((long)0xDEADBEEF_00000000UL) | floatBits);

        Assert.Equal(expected, RegisterBits.LowSingle(register));
    }
}
