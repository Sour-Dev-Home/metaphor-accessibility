namespace Metaphor.Core.RoundTrip;

/// <summary>Reads one file format into a model and writes the model back out.</summary>
/// <typeparam name="TModel">Our model of the format.</typeparam>
public interface ICodec<TModel>
{
    /// <summary>The written exemption that lets this format's round trip compare meaning instead of bytes, or null when
    /// the round trip must reproduce the original byte for byte.</summary>
    RoundTripExemption<TModel>? Exemption => null;

    /// <summary>Reads a whole file into the model.</summary>
    TModel Read(ReadOnlySpan<byte> bytes);

    /// <summary>Writes the model out as a whole file.</summary>
    byte[] Write(TModel model);
}
