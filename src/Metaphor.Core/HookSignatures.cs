namespace Metaphor.Core;

/// <summary>The hooks' signatures, from <c>hook-signatures.json</c>. The offline check (<see cref="SignatureCheck"/>)
/// scans the game exe on disk for each one.</summary>
public static class HookSignatures
{
    private const string ResourceName = "Metaphor.Core.hook-signatures.json";

    /// <summary>Every signature, in file order.</summary>
    public static IReadOnlyList<HookSignature> All { get; } = Parse(JsonList.ReadResource(ResourceName));

    /// <summary>Reads and validates a signatures file.</summary>
    /// <exception cref="InvalidDataException">An entry is malformed, or a name is used twice.</exception>
    public static IReadOnlyList<HookSignature> Parse(string json) =>
        JsonList.Parse<HookSignature>(json, "signatures file", "signature", Validate, signature => signature.Name);

    private static void Validate(HookSignature signature)
    {
        JsonList.CheckNameAndPattern("signature", signature.Name, signature.Pattern);
        if (signature.GameBuilds is not { Count: > 0 })
        {
            throw new InvalidDataException($"signature {signature.Name} names no game build");
        }
        if (signature.GameBuilds.FirstOrDefault(build => !KnownGameBuilds.IsSha256(build)) is { } bad)
        {
            throw new InvalidDataException($"signature {signature.Name}: '{bad}' is not a lowercase hex SHA-256");
        }
        if (string.IsNullOrWhiteSpace(signature.Evidence))
        {
            throw new InvalidDataException($"signature {signature.Name} has no evidence");
        }
    }
}
