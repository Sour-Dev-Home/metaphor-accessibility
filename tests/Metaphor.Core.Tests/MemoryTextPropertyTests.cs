using System.Text;
using FsCheck;
using FsCheck.Fluent;

namespace Metaphor.Core.Tests;

/// <summary>Properties of <see cref="MemoryText"/> (ADR 0015): for any memory, any readable window and any address,
/// a read never throws and gives bounded text or nothing. The read function stands in for <c>ReadProcessMemory</c>,
/// and on a failed read it scribbles over the buffer, as a careless one might.</summary>
[Trait("Category", "Unit")]
[Trait("Category", "Property")]
public sealed class MemoryTextPropertyTests
{
    /// <summary>Bytes at <see cref="Start"/>, readable from <see cref="Low"/> up to but not including
    /// <see cref="High"/>.</summary>
    public sealed record Memory(long Start, byte[] Bytes, long Low, long High)
    {
        public bool Readable(long address) =>
            address >= Low && address < High && address >= Start && address - Start < Bytes.Length;

        public bool Read(long address, Span<byte> buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                if (!Readable(unchecked(address + i)))
                {
                    buffer.Fill(0xee);
                    return false;
                }
            }
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Bytes[unchecked(address + i) - Start];
            }
            return true;
        }
    }

    public sealed record ReadCase(Memory Memory, long Address, int Limit);

    // Mostly short runs of bytes with many terminators, sometimes thousands with almost none, so a read reaches its
    // limit; the start sometimes sits near the very top of the address space.
    private static readonly Gen<Memory> Memories =
        from start in Gen.OneOf(Gen.Constant(0x10000L), Gen.Constant(long.MaxValue - 10_000), Gen.Constant(0L))
        from bytes in Gen.Frequency(
            (5, Gen.ArrayOf(Bytes(terminatorsIn: 7))),
            (1, Gen.Choose(400, 6000).SelectMany(n => Gen.ArrayOf(Bytes(terminatorsIn: 2000), n))))
        from low in Gen.Choose(-5, bytes.Length + 5)
        from high in Gen.Choose(low, bytes.Length + 5)
        select new Memory(start, bytes, start + low, start + high);

    private static readonly Arbitrary<ReadCase> Cases = (
        from memory in Memories
        from address in Gen.OneOf(
            Gen.Choose(-40, memory.Bytes.Length + 40).Select(offset => unchecked(memory.Start + offset)),
            Gen.Elements(long.MinValue, -1L, long.MaxValue))
        from limit in Gen.Frequency(
            (4, Gen.Choose(0, 300)), (1, Gen.Elements(511, 512, 513, 4096)), (1, Gen.Choose(300, 8000)))
        select new ReadCase(memory, address, limit)).ToArbitrary();

    private static Gen<byte> Bytes(int terminatorsIn) =>
        Gen.Frequency((1, Gen.Constant((byte)0)), (terminatorsIn - 1, Gen.Choose(1, 255).Select(b => (byte)b)));

    [Fact]
    public void AnsiIsTheReadableBytesUpToTheTerminatorOrTheLimitOrNothing() =>
        PropertyRun.Check(Prop.ForAll(Cases, c =>
        {
            var text = MemoryText.ReadAnsi(c.Memory.Read, c.Address, c.Limit);

            var expected = new StringBuilder();
            for (var i = 0; i < c.Limit; i++)
            {
                var at = unchecked(c.Address + i);
                if (!c.Memory.Readable(at))
                {
                    if (i == 0)
                    {
                        Assert.Null(text);
                        return;
                    }
                    break;
                }
                var b = c.Memory.Bytes[at - c.Memory.Start];
                if (b == 0)
                {
                    break;
                }
                expected.Append((char)b);
            }
            Assert.Equal(expected.ToString(), text);
            Assert.True(text!.Length <= c.Limit);
        }));

    [Fact]
    public void Utf8IsTheWholeRangeWithinItsCapOrNothing() =>
        PropertyRun.Check(Prop.ForAll(Cases, Arb.From(Gen.Frequency((4, Gen.Choose(-10, 300)), (1, Gen.Choose(300, 7000)))), (c, length) =>
        {
            var text = MemoryText.ReadUtf8(c.Memory.Read, c.Address, length, maxBytes: c.Limit);

            var whole = length >= 0 && length <= c.Limit &&
                Enumerable.Range(0, length).All(i => c.Memory.Readable(unchecked(c.Address + i)));
            if (!whole)
            {
                Assert.Null(text);
                return;
            }
            var bytes = Enumerable.Range(0, length).Select(i => c.Memory.Bytes[unchecked(c.Address + i) - c.Memory.Start]);
            Assert.Equal(Encoding.UTF8.GetString([.. bytes]), text);
            Assert.True(text!.Length <= length);
        }));
}
