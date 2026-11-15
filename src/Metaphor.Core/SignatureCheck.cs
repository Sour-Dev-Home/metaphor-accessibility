using System.Security.Cryptography;

namespace Metaphor.Core;

/// <summary>The offline signature check (stage 4 of the feedback loop in issue #1): confirms each hook's signature
/// matches exactly once in the game executable on disk, without launching the game.</summary>
public static class SignatureCheck
{
    // Enough to tell a few near-duplicates apart without printing thousands of offsets for a common pattern.
    private const int OffsetsListed = 5;

    /// <summary>Scans <paramref name="gameExe"/> for each signature and writes one line per signature.</summary>
    /// <returns>0 when every signature matches exactly once, otherwise 1.</returns>
    public static int Run(
        string gameExe, IReadOnlyList<HookSignature> signatures, IReadOnlyList<GameBuild> knownBuilds, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(signatures);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(knownBuilds);
        var bytes = File.ReadAllBytes(gameExe);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var known = knownBuilds.FirstOrDefault(build => build.Sha256 == sha);
        output.WriteLine($"game build {sha} ({(known is null ? "not a known build" : $"Steam build {known.SteamBuildId}")})");
        var failed = 0;
        foreach (var signature in signatures)
        {
            var matches = BytePattern.Parse(signature.Pattern).Matches(bytes);
            switch (matches.Count)
            {
                case 0:
                    output.WriteLine($"FAIL {signature.Name}: no match");
                    failed++;
                    break;
                case 1:
                    // Ground rule 2: a match on a build the signature doesn't name isn't a verified hook yet.
                    var unnamed = signature.GameBuilds.Contains(sha) ? "" : ", on a build it doesn't name yet";
                    output.WriteLine($"ok   {signature.Name}: 1 match at 0x{matches[0]:x}{unnamed}");
                    break;
                default:
                    var listed = string.Join(", ", matches.Take(OffsetsListed).Select(at => $"0x{at:x}"));
                    var more = matches.Count > OffsetsListed ? $" and {matches.Count - OffsetsListed} more" : "";
                    output.WriteLine($"FAIL {signature.Name}: {matches.Count} matches, at {listed}{more}");
                    failed++;
                    break;
            }
        }
        output.WriteLine(signatures.Count == 0
            ? "no signatures to check"
            : $"{signatures.Count - failed} of {signatures.Count} signatures matched exactly once");
        return failed == 0 ? 0 : 1;
    }
}
