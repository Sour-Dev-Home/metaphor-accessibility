using System.Text.Json;
using System.Text.Json.Serialization;

namespace Metaphor.Core;

/// <summary>The hooks' signatures, from <c>hook-signatures.json</c>. The offline check (<see cref="SignatureCheck"/>)
/// scans the game exe on disk for each one.</summary>
public static class HookSignatures
{
    private const string ResourceName = "Metaphor.Core.hook-signatures.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Every signature, in file order.</summary>
    public static IReadOnlyList<HookSignature> All { get; } = Load();

    /// <summary>Reads and validates a signatures file.</summary>
    /// <exception cref="InvalidDataException">An entry is malformed, or a name is used twice.</exception>
    public static IReadOnlyList<HookSignature> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        List<HookSignature> signatures;
        try
        {
            signatures = JsonSerializer.Deserialize<List<HookSignature>>(json, Options) ?? [];
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"signatures file is malformed: {e.Message}", e);
        }

        foreach (var signature in signatures)
        {
            Validate(signature);
        }

        var duplicate = signatures.GroupBy(signature => signature.Name).FirstOrDefault(group => group.Count() > 1);
        return duplicate is null ? signatures : throw new InvalidDataException($"signature {duplicate.Key} is listed twice");
    }

    private static void Validate(HookSignature? signature)
    {
        if (signature is null)
        {
            throw new InvalidDataException("signatures file has an empty entry");
        }
        if (string.IsNullOrWhiteSpace(signature.Name))
        {
            throw new InvalidDataException("a signature has no name");
        }
        try
        {
            BytePattern.Parse(signature.Pattern ?? "");
        }
        catch (FormatException e)
        {
            throw new InvalidDataException($"signature {signature.Name} has a bad pattern: {e.Message}", e);
        }
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

    private static List<HookSignature> Load()
    {
        using var stream = typeof(HookSignatures).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"missing embedded resource {ResourceName}");
        using var reader = new StreamReader(stream);
        return [.. Parse(reader.ReadToEnd())];
    }
}
