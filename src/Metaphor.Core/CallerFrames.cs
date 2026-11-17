using System.Buffers.Binary;
using System.Globalization;

namespace Metaphor.Core;

/// <summary>Finding which game code called a hooked function, from inside the hook (#93). The hook layer captures
/// return addresses, by a stack walk or by reading its own thread's stack; this picks the caller's out of them and
/// checks it, with no native calls.</summary>
public static class CallerFrames
{
    /// <summary>The length of a direct <c>call rel32</c> (<c>E8</c> and a 32-bit offset).</summary>
    public const int DirectCallLength = 5;

    /// <summary>How many bytes before a return address <see cref="CallLengthBefore"/> looks at: the longest call it
    /// knows.</summary>
    public const int LongestCall = 7;

    /// <summary>The most candidates <see cref="ReturnAddressesOnStack"/> gives.</summary>
    public const int MaxStackCandidates = 3;

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

    /// <summary>The length of the x64 call instruction that ends just before a return address, from the
    /// <see cref="LongestCall"/> bytes before it; 0 when they don't end in a call this knows. It knows a direct
    /// <c>call rel32</c> and the indirect forms compilers emit for virtual calls and function pointers: through a
    /// register, through <c>[reg+disp8]</c> or <c>[reg+disp32]</c> (with or without a REX prefix or a SIB byte), and
    /// <c>[rip+disp32]</c>. Encodings from the Intel SDM, volume 2, <c>CALL</c> (opcodes <c>E8</c> and <c>FF /2</c>).
    /// A heuristic: any bytes can look like a call.</summary>
    public static int CallLengthBefore(ReadOnlySpan<byte> before)
    {
        if (before.Length != LongestCall)
        {
            return 0;
        }
        static bool Reg(byte modrm) => modrm is >= 0xD0 and <= 0xD7;
        static bool Disp8(byte modrm) => modrm is >= 0x50 and <= 0x57 and not 0x54;
        static bool Disp32(byte modrm) => modrm is >= 0x90 and <= 0x97 and not 0x94;
        static bool Rex(byte b) => b is >= 0x40 and <= 0x4F;

        // Longest first, so a REX prefix counts as part of its call.
        if ((before[0] == 0xFF && before[1] == 0x94) ||
            (Rex(before[0]) && before[1] == 0xFF && (before[2] == 0x15 || Disp32(before[2]))))
        {
            return 7;
        }
        if (before[1] == 0xFF && (before[2] == 0x15 || Disp32(before[2])))
        {
            return 6;
        }
        if (before[2] == 0xE8)
        {
            return 5;
        }
        if ((before[3] == 0xFF && before[4] == 0x54) || (Rex(before[3]) && before[4] == 0xFF && Disp8(before[5])))
        {
            return 4;
        }
        if ((Rex(before[4]) && before[5] == 0xFF && Reg(before[6])) || (before[4] == 0xFF && Disp8(before[5])))
        {
            return 3;
        }
        return before[5] == 0xFF && Reg(before[6]) ? 2 : 0;
    }

    /// <summary>Return addresses into the module found by reading a thread's stack upward from inside the hook: each
    /// 8-byte value, in order, that lies in the module and follows a call (<see cref="CallLengthBefore"/>), at most
    /// <see cref="MaxStackCandidates"/>. The first is usually the hooked function's caller, but a stale value left in
    /// the mod's own frames can come first, so each comes with its direct call's target when it has one: the right one
    /// names the hooked function (or a thunk to it). <paramref name="readBefore"/> fills the bytes before a module
    /// offset, false when it can't.</summary>
    public static IReadOnlyList<(long ReturnRva, long? CallTargetRva)> ReturnAddressesOnStack(
        ReadOnlySpan<byte> stack, long moduleBase, long moduleSize, Func<long, byte[], bool> readBefore)
    {
        ArgumentNullException.ThrowIfNull(readBefore);
        var found = new List<(long, long?)>();
        var before = new byte[LongestCall];
        for (var at = 0; at + sizeof(long) <= stack.Length && found.Count < MaxStackCandidates; at += sizeof(long))
        {
            var value = BinaryPrimitives.ReadInt64LittleEndian(stack[at..]);
            if (value < moduleBase || value - moduleBase >= moduleSize || value - moduleBase < LongestCall)
            {
                continue;
            }
            var rva = value - moduleBase;
            if (!readBefore(rva - LongestCall, before) || CallLengthBefore(before) == 0)
            {
                continue;
            }
            found.Add((rva, DirectCallTarget(before.AsSpan(LongestCall - DirectCallLength), rva)));
        }
        return found;
    }
}

/// <summary>Who called a hooked function, as the hook layer found it (#93): the candidates (return address and, for a
/// direct call, its target, as module offsets), how they were found (<c>walk</c>: the first module frame of a stack
/// walk; <c>scan</c>: <see cref="CallerFrames.ReturnAddressesOnStack"/>, after a walk found none; <c>none</c>), and how
/// many frames the walk captured, so a log shows where a walk stopped.</summary>
public sealed record CallerTrace(IReadOnlyList<(long ReturnRva, long? CallTargetRva)> Candidates, string Method, int FramesWalked)
{
    /// <summary>No caller found and no walk taken.</summary>
    public static CallerTrace Unknown { get; } = new([], "none", 0);

    /// <summary>The log's form: <c>caller=0x… call=0x…</c> for the first candidate (<c>(unknown)</c> for none), then
    /// <c>via=… frames=…</c>, then any further candidates as <c>more=0xRET&gt;0xTARGET,…</c>.</summary>
    public override string ToString()
    {
        long? caller = Candidates.Count > 0 ? Candidates[0].ReturnRva : null;
        var target = Candidates.Count > 0 ? Candidates[0].CallTargetRva : null;
        var text = string.Create(
            CultureInfo.InvariantCulture, $"caller={Hex(caller)} call={Hex(target)} via={Method} frames={FramesWalked}");
        return Candidates.Count > 1
            ? text + " more=" + string.Join(",", Candidates.Skip(1).Select(c => $"{Hex(c.ReturnRva)}>{Hex(c.CallTargetRva)}"))
            : text;
    }

    private static string Hex(long? value) =>
        value is { } v ? string.Create(CultureInfo.InvariantCulture, $"0x{v:x}") : "(unknown)";
}
