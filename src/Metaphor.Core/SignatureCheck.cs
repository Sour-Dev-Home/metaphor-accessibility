using System.Diagnostics.CodeAnalysis;
using System.Reflection.PortableExecutable;
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
        return Run(gameExe, [.. signatures.Select(s => (s.Name, s.Pattern, (IReadOnlyList<string>?)s.GameBuilds))],
            knownBuilds, output);
    }

    /// <summary>The same check for other mods' signatures (issue #21), which name no build of ours.</summary>
    /// <returns>0 when every probe matches exactly once, otherwise 1.</returns>
    public static int RunProbes(
        string gameExe, IReadOnlyList<SignatureProbe> probes, IReadOnlyList<GameBuild> knownBuilds, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(probes);
        return Run(gameExe, [.. probes.Select(p => (p.Name, p.Pattern, (IReadOnlyList<string>?)null))], knownBuilds, output);
    }

    // A signature's GameBuilds is null when it names none (a probe), which skips the build note.
    private static int Run(
        string gameExe, IReadOnlyList<(string Name, string Pattern, IReadOnlyList<string>? GameBuilds)> signatures,
        IReadOnlyList<GameBuild> knownBuilds, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(knownBuilds);
        var bytes = File.ReadAllBytes(gameExe);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var known = knownBuilds.FirstOrDefault(build => build.Sha256 == sha);
        var headers = HeadersOrNone(bytes);
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
                    var unnamed = signature.GameBuilds is null || signature.GameBuilds.Contains(sha)
                        ? ""
                        : ", on a build it doesn't name yet";
                    output.WriteLine($"ok   {signature.Name}: 1 match at 0x{matches[0]:x}{Rva(headers, matches[0])}{unnamed}");
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

    /// <summary>The PE headers, when the file is a PE image; otherwise null, and no addresses are given.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A file that isn't a PE image just gets no addresses.")]
    private static PEHeaders? HeadersOrNone(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var headers = new PEHeaders(stream);
            return headers.PEHeader is null ? null : headers;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>" (rva 0x…)": where a file offset lands in memory, relative to the image base. The headers load at the
    /// image's start (SizeOfHeaders); the rest is read from the section that holds it (its raw data and virtual address
    /// fields). Empty outside both, or for a file that isn't a PE image.</summary>
    private static string Rva(PEHeaders? headers, long fileOffset)
    {
        if (headers is null)
        {
            return "";
        }
        if (fileOffset < headers.PEHeader!.SizeOfHeaders)
        {
            return $" (rva 0x{fileOffset:x})";
        }
        foreach (var section in headers.SectionHeaders)
        {
            var from = (long)section.PointerToRawData;
            if (fileOffset >= from && fileOffset < from + section.SizeOfRawData)
            {
                return $" (rva 0x{fileOffset - from + section.VirtualAddress:x})";
            }
        }
        return "";
    }
}
