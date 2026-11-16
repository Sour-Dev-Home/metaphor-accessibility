using System.Buffers.Binary;

namespace Metaphor.Core;

/// <summary>Finding which game code called a hooked function, from a stack walk taken inside the hook (#93). The hook
/// layer captures the return addresses; this picks the caller's out of them and checks it, with no native calls.</summary>
public static class CallerFrames
{
    /// <summary>The length of a direct <c>call rel32</c> (<c>E8</c> and a 32-bit offset).</summary>
    public const int DirectCallLength = 5;

    /// <summary>The first return address inside the module, as an offset from <paramref name="moduleBase"/>, or null if
    /// none is. Frames run innermost first, so the frames before it are the mod's own (managed code, the runtime's
    /// thunks, the hooking library's stub), and the first one in the module is where the hooked function returns to:
    /// its caller. That the walk reaches that frame at all is unverified until a run shows it.</summary>
    public static long? FirstInModule(ReadOnlySpan<long> frames, long moduleBase, long moduleSize)
    {
        foreach (var frame in frames)
        {
            if (frame >= moduleBase && frame - moduleBase < moduleSize)
            {
                return frame - moduleBase;
            }
        }
        return null;
    }

    /// <summary>Where the call that returns to <paramref name="returnRva"/> went, if the
    /// <see cref="DirectCallLength"/> bytes before it are a direct <c>call rel32</c>; otherwise null (an indirect call,
    /// or not a call at all, which would mean the walk found no real return address). Only a candidate: any byte can
    /// look like an opcode.</summary>
    public static long? DirectCallTarget(ReadOnlySpan<byte> bytesBeforeReturn, long returnRva) =>
        bytesBeforeReturn.Length == DirectCallLength && bytesBeforeReturn[0] == 0xE8
            ? returnRva + BinaryPrimitives.ReadInt32LittleEndian(bytesBeforeReturn[1..])
            : null;
}
