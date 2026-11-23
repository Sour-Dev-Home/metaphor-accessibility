using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace Metaphor.Core;

/// <summary>The game's main module as the hook layer offers it to the scan, read a chunk at a time through
/// <see cref="ChunkedScan"/>.</summary>
/// <param name="ModuleBytes">The module's size in memory.</param>
/// <param name="MillisecondsSinceGameStart">When the scan started, measured from the game process's start.</param>
/// <param name="Regions">Its committed, readable stretches, as start RVA and length.</param>
/// <param name="Read">Copies bytes from an RVA; false when they can't all be read.</param>
public sealed record ScannedModule(
    long ModuleBytes, long MillisecondsSinceGameStart, IReadOnlyList<(long Rva, long Length)> Regions, ChunkedScan.ReadAt Read);

/// <summary>What one probe found.</summary>
/// <param name="Name">The probe's name.</param>
/// <param name="Matches">How many times it matched.</param>
/// <param name="FirstRvas">Where the first few matches start, relative to the module's base address.</param>
public sealed record ProbeResult(string Name, int Matches, IReadOnlyList<long> FirstRvas);

/// <summary>The scan's result, written to <see cref="SignatureScan.ReportFileName"/>. The numbers are 0 when
/// <see cref="Error"/> says the scan failed.</summary>
/// <param name="GameBuildSha256">The game build scanned.</param>
/// <param name="MillisecondsSinceGameStart">When the scan started, measured from the game process's start.</param>
/// <param name="ModuleBytes">The main module's size in memory.</param>
/// <param name="ReadableBytes">How much of it was read and scanned.</param>
/// <param name="UnreadableBytes">Committed bytes that looked readable but failed to read.</param>
/// <param name="ScanMilliseconds">How long the read and the scan took.</param>
/// <param name="Probes">What each probe found.</param>
/// <param name="Error">Why the scan failed, or null.</param>
public sealed record SignatureScanReport(
    string GameBuildSha256, long MillisecondsSinceGameStart, long ModuleBytes, long ReadableBytes, long UnreadableBytes,
    long ScanMilliseconds, IReadOnlyList<ProbeResult> Probes, string? Error)
{
    /// <summary>The scan's log line: what it matched, what it read, how long it took (#824) and when it started.
    /// </summary>
    public string Summary() => Error is null
        ? string.Create(CultureInfo.InvariantCulture,
            $"{Probes.Count(probe => probe.Matches == 1)} of {Probes.Count} probes matched once; {ReadableBytes} of {ModuleBytes} bytes read, {UnreadableBytes} failed; took {ScanMilliseconds} ms, {MillisecondsSinceGameStart} ms after start ({SignatureScan.ReportFileName})")
        : string.Create(CultureInfo.InvariantCulture, $"failed after {ScanMilliseconds} ms: {Error}");
}

/// <summary>Scans the running game's memory for other mods' signatures, once, when asked (issue #21). The hook layer
/// reads the memory; this only searches it.</summary>
public static class SignatureScan
{
    /// <summary>The probes file in the mod's folder that asks for one scan. <c>scripts/dev/session.ps1 spike</c>
    /// copies it there; the scan deletes it, so the next launch doesn't scan again. The probes never ship in the mod.</summary>
    public const string RequestFileName = "signature-probes.json";

    /// <summary>The file in the mod's folder that the scan writes.</summary>
    public const string ReportFileName = "signature-scan.json";

    private const int RvasListed = 5;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>When <paramref name="modFolder"/> holds <see cref="RequestFileName"/>, reads the probes from it, deletes
    /// it, opens the module with <paramref name="openModule"/>, scans it a chunk at a time and writes
    /// <see cref="ReportFileName"/>. Never throws: it runs inside the game.</summary>
    /// <returns>The report, for the caller to log, or null when no scan was asked for.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Any failure must be reported rather than reach the game.")]
    public static SignatureScanReport? ScanIfRequested(string modFolder, string gameBuildSha256, Func<ScannedModule> openModule)
    {
        ArgumentNullException.ThrowIfNull(openModule);
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
            var module = openModule();
            var found = ChunkedScan.FindAll(module.Regions, module.Read, [.. probes.Select(probe => (probe.Name, probe.Pattern))]);
            report = new(gameBuildSha256, module.MillisecondsSinceGameStart, module.ModuleBytes, found.ReadableBytes,
                found.UnreadableBytes, clock.ElapsedMilliseconds,
                [.. probes.Select(probe => Result(probe.Name, found.Matches[probe.Name]))], null);
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

    private static ProbeResult Result(string name, IReadOnlyList<long> rvas) => new(name, rvas.Count, [.. rvas.Take(RvasListed)]);
}
