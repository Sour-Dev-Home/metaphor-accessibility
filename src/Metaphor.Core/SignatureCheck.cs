using System.Collections.Immutable;
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
        var sections = SectionsOrNone(bytes);
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
                    output.WriteLine($"ok   {signature.Name}: 1 match at 0x{matches[0]:x}{Rva(sections, matches[0])}{unnamed}");
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

    /// <summary>The section table, when the file is a PE image; otherwise empty, and no addresses are given.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A file that isn't a PE image just gets no addresses.")]
    private static ImmutableArray<SectionHeader> SectionsOrNone(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            return new PEHeaders(stream).SectionHeaders;
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>" (rva 0x…)": where a file offset lands in memory, relative to the image base, read from the section
    /// that holds it (the PE format's raw data and virtual address fields). Empty outside every section.</summary>
    private static string Rva(ImmutableArray<SectionHeader> sections, long fileOffset)
    {
        foreach (var section in sections)
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
