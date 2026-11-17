namespace Metaphor.Core;

/// <summary>Reads narrower values out of the pointer-sized and <c>double</c> arguments hooks observe (rule 9).</summary>
public static class RegisterBits
{
    /// <summary>The <c>float</c> in the low 32 bits of an xmm register a hook observed as a <c>double</c>. Windows x64
    /// passes a <c>float</c> argument in an xmm register's low 32 bits and leaves the rest undefined
    /// (https://learn.microsoft.com/cpp/build/x64-calling-convention), so the upper half is ignored.</summary>
    public static float LowSingle(double register) =>
        BitConverter.Int32BitsToSingle(unchecked((int)BitConverter.DoubleToInt64Bits(register)));
}
