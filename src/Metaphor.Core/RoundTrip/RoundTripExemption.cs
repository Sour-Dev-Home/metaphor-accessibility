namespace Metaphor.Core.RoundTrip;

/// <summary>Lets a format's round trip pass when the written bytes differ but read back to the same meaning. Meaning is
/// judged on the models, so bytes the model never holds (such as trailing data) can be lost unnoticed: grant an
/// exemption only when the reason covers that.</summary>
/// <typeparam name="TModel">The format's model.</typeparam>
public sealed class RoundTripExemption<TModel>
{
    /// <param name="reason">Why byte-for-byte output isn't possible or needed for this format. Required.</param>
    /// <param name="sameMeaning">Decides whether two models mean the same thing.</param>
    public RoundTripExemption(string reason, IEqualityComparer<TModel> sameMeaning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(sameMeaning);

        Reason = reason;
        SameMeaning = sameMeaning;
    }

    /// <summary>Why byte-for-byte output isn't possible or needed for this format.</summary>
    public string Reason { get; }

    /// <summary>Decides whether two models mean the same thing.</summary>
    public IEqualityComparer<TModel> SameMeaning { get; }
}
