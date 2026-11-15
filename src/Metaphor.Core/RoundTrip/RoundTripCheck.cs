namespace Metaphor.Core.RoundTrip;

/// <summary>How a round trip's written file compared with the original.</summary>
public enum RoundTripOutcome
{
    /// <summary>The written file matches the original byte for byte.</summary>
    ByteIdentical,

    /// <summary>The bytes differ, but the format's exemption judges the two files to mean the same thing.</summary>
    SameMeaning,

    /// <summary>The round trip lost or changed something.</summary>
    Different,
}

/// <summary>The result of one round trip.</summary>
/// <param name="Outcome">How the written file compared with the original.</param>
/// <param name="Detail">What a person needs to follow up: where the bytes first differ, or the exemption relied on.</param>
public sealed record RoundTripResult(RoundTripOutcome Outcome, string Detail)
{
    /// <summary>True unless the round trip lost or changed something.</summary>
    public bool Passed => Outcome != RoundTripOutcome.Different;
}

/// <summary>Checks a round trip: reads a file with a codec, writes it back, and compares the result with the
/// original.</summary>
public static class RoundTripCheck
{
    public static RoundTripResult Run<TModel>(ICodec<TModel> codec, ReadOnlySpan<byte> original)
    {
        ArgumentNullException.ThrowIfNull(codec);

        var model = codec.Read(original);
        var written = codec.Write(model);
        if (original.SequenceEqual(written))
        {
            return new RoundTripResult(RoundTripOutcome.ByteIdentical, $"{written.Length} bytes identical");
        }

        var difference = DescribeDifference(original, written);
        var exemption = codec.Exemption;
        if (exemption is not null && exemption.SameMeaning.Equals(model, codec.Read(written)))
        {
            return new RoundTripResult(RoundTripOutcome.SameMeaning, $"{difference}; same meaning, exempt: {exemption.Reason}");
        }

        return new RoundTripResult(RoundTripOutcome.Different, difference);
    }

    private static string DescribeDifference(ReadOnlySpan<byte> original, ReadOnlySpan<byte> written)
    {
        var offset = original.CommonPrefixLength(written);
        var sizes = $"original {original.Length} bytes, written {written.Length} bytes";
        return offset < original.Length && offset < written.Length
            ? $"first difference at offset 0x{offset:X}: original 0x{original[offset]:X2}, written 0x{written[offset]:X2} ({sizes})"
            : $"one file is a prefix of the other ({sizes})";
    }
}
