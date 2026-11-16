namespace Metaphor.Core;

/// <summary>Reads a probes file: other mods' signatures to compare the game on disk with the game running (issue #21).
/// The committed list is <c>tools/Metaphor.SignatureCheck/signature-probes.json</c>; it is dev tooling, never built into
/// the mod.</summary>
public static class SignatureProbes
{
    /// <summary>Reads and validates a probes file.</summary>
    /// <exception cref="InvalidDataException">An entry is malformed, or a name is used twice.</exception>
    public static IReadOnlyList<SignatureProbe> Parse(string json) =>
        JsonList.Parse<SignatureProbe>(json, "probes file", "probe", Validate, probe => probe.Name);

    private static void Validate(SignatureProbe probe)
    {
        JsonList.CheckNameAndPattern("probe", probe.Name, probe.Pattern);
        if (string.IsNullOrWhiteSpace(probe.Source))
        {
            throw new InvalidDataException($"probe {probe.Name} has no source");
        }
    }
}
