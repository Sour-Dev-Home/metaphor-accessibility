using System.Buffers.Binary;
using CsCheck;
using Metaphor.Core.RoundTrip;

namespace Metaphor.Core.Tests.RoundTrip;

/// <summary>A made-up format that proves the harness: the magic <c>TOY1</c>, a flags byte, a little-endian 16-bit entry
/// count, then each entry as a length byte and that many bytes.</summary>
internal sealed record ToyFile(byte Flags, IReadOnlyList<byte[]> Entries)
{
    public static readonly Gen<ToyFile> Gen =
        CsCheck.Gen.Select(CsCheck.Gen.Byte, CsCheck.Gen.Byte.Array[0, 255].Array[0, 20], (flags, entries) =>
            new ToyFile(flags, entries));

    /// <summary>Same entries, flags ignored: the meaning the lossy codec's exemption keeps.</summary>
    public static readonly IEqualityComparer<ToyFile> SameEntries = new EntriesComparer();

    private sealed class EntriesComparer : IEqualityComparer<ToyFile>
    {
        public bool Equals(ToyFile? x, ToyFile? y) =>
            x is not null && y is not null && x.Entries.Count == y.Entries.Count &&
            x.Entries.Zip(y.Entries).All(pair => pair.First.AsSpan().SequenceEqual(pair.Second));

        public int GetHashCode(ToyFile obj) => obj.Entries.Count;
    }
}

/// <summary>Reads and writes every byte of the toy format.</summary>
internal class ToyCodec : ICodec<ToyFile>
{
    private static readonly byte[] Magic = "TOY1"u8.ToArray();

    public ToyFile Read(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.StartsWith(Magic))
        {
            throw new InvalidDataException("not a toy file");
        }

        var flags = bytes[4];
        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[5..]);
        var entries = new List<byte[]>(count);
        var offset = 7;
        for (var i = 0; i < count; i++)
        {
            var length = bytes[offset];
            entries.Add(bytes.Slice(offset + 1, length).ToArray());
            offset += 1 + length;
        }

        return new ToyFile(flags, entries);
    }

    public byte[] Write(ToyFile model)
    {
        ArgumentNullException.ThrowIfNull(model);

        using var stream = new MemoryStream();
        stream.Write(Magic);
        stream.WriteByte(WrittenFlags(model));
        Span<byte> count = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(count, checked((ushort)model.Entries.Count));
        stream.Write(count);
        foreach (var entry in model.Entries)
        {
            stream.WriteByte(checked((byte)entry.Length));
            stream.Write(entry);
        }

        return stream.ToArray();
    }

    protected virtual byte WrittenFlags(ToyFile model) => model.Flags;
}

/// <summary>Deliberately lossy: always writes the flags byte as zero.</summary>
internal class LossyToyCodec : ToyCodec
{
    protected override byte WrittenFlags(ToyFile model) => 0;
}

/// <summary>The lossy codec with a written exemption: the flags carry no meaning, so only the entries must survive.</summary>
internal sealed class ExemptLossyToyCodec : LossyToyCodec, ICodec<ToyFile>
{
    public RoundTripExemption<ToyFile> Exemption { get; } =
        new("The flags byte is reserved and never read, so writing it as zero loses nothing.", ToyFile.SameEntries);

    RoundTripExemption<ToyFile>? ICodec<ToyFile>.Exemption => Exemption;
}
