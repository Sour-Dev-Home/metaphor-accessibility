using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Metaphor.Core;

/// <summary>A stretch of the game's module that could be read, starting <paramref name="Rva"/> bytes after the
/// module's base address.</summary>
public sealed record MemoryRun(long Rva, ReadOnlyMemory<byte> Bytes);

/// <summary>What the hook layer could read of the game's main module.</summary>
/// <param name="ModuleBytes">The module's size in memory.</param>
/// <param name="Runs">Copies of its readable stretches.</param>
/// <param name="UnreadableBytes">Committed bytes that looked readable but failed to copy.</param>
/// <param name="MillisecondsSinceGameStart">When it was read, measured from the game process's start.</param>
public sealed record ModuleMemory(
    long ModuleBytes, IReadOnlyList<MemoryRun> Runs, long UnreadableBytes, long MillisecondsSinceGameStart);

/// <summary>What one probe found.</summary>
/// <param name="Name">The probe's name.</param>
/// <param name="Matches">How many times it matched.</param>
/// <param name="FirstRvas">Where the first few matches start, relative to the module's base address.</param>
public sealed record ProbeResult(string Name, int Matches, IReadOnlyList<long> FirstRvas);

/// <summary>The scan's result, written to <see cref="SignatureScan.ReportFileName"/>. The numbers are 0 when
/// <see cref="Error"/> says the scan failed.</summary>
/// <param name="GameBuildSha256">The game build scanned.</param>
/// <param name="MillisecondsSinceGameStart">When the memory was read, measured from the game process's start.</param>
/// <param name="ModuleBytes">The main module's size in memory.</param>
/// <param name="ReadableBytes">How much of it was copied and scanned.</param>
/// <param name="UnreadableBytes">Committed bytes that looked readable but failed to copy.</param>
/// <param name="ScanMilliseconds">How long the read and the scan took.</param>
/// <param name="Probes">What each probe found.</param>
/// <param name="Error">Why the scan failed, or null.</param>
public sealed record SignatureScanReport(
    string GameBuildSha256, long MillisecondsSinceGameStart, long ModuleBytes, long ReadableBytes, long UnreadableBytes,
    long ScanMilliseconds, IReadOnlyList<ProbeResult> Probes, string? Error);

/// <summary>Scans the running game's memory for other mods' signatures, once, when asked (issue #21). The hook layer
/// reads the memory; this only searches it.</summary>
public static class SignatureScan
{
    /// <summary>The probes file in the mod's folder that asks for one scan. <c>scripts/signature-spike.ps1</c>
    /// copies it there; the scan deletes it, so the next launch doesn't scan again. The probes never ship in the mod.</summary>
    public const string RequestFileName = "signature-probes.json";

    /// <summary>The file in the mod's folder that the scan writes.</summary>
    public const string ReportFileName = "signature-scan.json";

    private const int RvasListed = 5;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>When <paramref name="modFolder"/> holds <see cref="RequestFileName"/>, reads the probes from it, deletes
    /// it, reads the module with <paramref name="readModule"/>, scans it and writes <see cref="ReportFileName"/>. Never
    /// throws: it runs inside the game.</summary>
    /// <returns>The report, for the caller to log, or null when no scan was asked for.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Any failure must be reported rather than reach the game.")]
    public static SignatureScanReport? ScanIfRequested(string modFolder, string gameBuildSha256, Func<ModuleMemory> readModule)
    {
        var clock = Stopwatch.StartNew();
        SignatureScanReport report;
        try
        {
            var request = Path.Combine(modFolder, RequestFileName);
            if (!File.Exists(request))
            {
                return null;
            }
            var probes = SignatureProbes.Parse(File.ReadAllText(request));
            File.Delete(request);
            var module = readModule();
            report = new(gameBuildSha256, module.MillisecondsSinceGameStart, module.ModuleBytes,
                module.Runs.Sum(run => (long)run.Bytes.Length), module.UnreadableBytes, clock.ElapsedMilliseconds,
                Scan(module.Runs, probes), null);
        }
        catch (Exception e)
        {
            // Only our own errors keep their message; others, such as IO errors, can name local paths.
            var error = e is InvalidDataException or InvalidOperationException ? $"{e.GetType().Name}: {e.Message}" : e.GetType().Name;
            report = new(gameBuildSha256, 0, 0, 0, 0, clock.ElapsedMilliseconds, [], error);
        }

        try
        {
            File.Delete(Path.Combine(modFolder, RequestFileName));
            AtomicFile.WriteAllText(Path.Combine(modFolder, ReportFileName), JsonSerializer.Serialize(report, Options));
        }
        catch (Exception)
        {
            // The caller logs the returned report, which is all that's left.
        }
        return report;
    }

    /// <summary>Counts each probe's matches across <paramref name="runs"/>. A match can't span two runs.</summary>
    public static IReadOnlyList<ProbeResult> Scan(IReadOnlyList<MemoryRun> runs, IReadOnlyList<SignatureProbe> probes)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(probes);
        return [.. probes.Select(probe =>
        {
            var pattern = BytePattern.Parse(probe.Pattern);
            var rvas = runs.SelectMany(run => pattern.Matches(run.Bytes.Span).Select(offset => run.Rva + offset)).ToList();
            return new ProbeResult(probe.Name, rvas.Count, [.. rvas.Take(RvasListed)]);
        })];
    }
}
