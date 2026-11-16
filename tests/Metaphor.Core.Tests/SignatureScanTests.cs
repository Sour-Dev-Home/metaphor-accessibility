namespace Metaphor.Core.Tests;

/// <summary>The in-game signature scan's logic, run over a read function on a byte array that stands in for the game's
/// readable memory, as <see cref="ChunkedScanTests"/> does.</summary>
[Trait("Category", "Unit")]
public sealed class SignatureScanTests : IDisposable
{
    private const string Build = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";

    private readonly string modFolder = Directory.CreateTempSubdirectory("metaphor-scan-").FullName;

    public void Dispose() => Directory.Delete(modFolder, recursive: true);

    private string ReportPath => Path.Combine(modFolder, SignatureScan.ReportFileName);

    private string RequestPath => Path.Combine(modFolder, SignatureScan.RequestFileName);

    private void RequestScan(string probes = """[{ "name": "a", "pattern": "20", "source": "s" }]""") =>
        File.WriteAllText(RequestPath, probes);

    private static string Probes(params (string Name, string Pattern)[] probes) =>
        "[" + string.Join(",", probes.Select(p => $$"""{ "name": "{{p.Name}}", "pattern": "{{p.Pattern}}", "source": "s" }""")) + "]";

    // `memory` read from RVA 0 over `regions`; reads that touch an `unreadable` RVA fail.
    private static ScannedModule Module(
        byte[] memory, IReadOnlyList<(long Rva, long Length)>? regions = null, params long[] unreadable) =>
        new(0x10_0000, 1500, regions ?? [(0, memory.Length)], (rva, buffer) =>
        {
            var length = buffer.Length;
            if (unreadable.Any(bad => bad >= rva && bad < rva + length))
            {
                return false;
            }
            memory.AsSpan((int)rva, buffer.Length).CopyTo(buffer);
            return true;
        });

    private SignatureScanReport Scan(ScannedModule module, string probes)
    {
        RequestScan(probes);
        return SignatureScan.ScanIfRequested(modFolder, Build, () => module)!;
    }

    [Fact]
    public void DoesNothingUnlessAskedWithAProbesFile()
    {
        var read = false;

        var report = SignatureScan.ScanIfRequested(modFolder, Build, () => { read = true; return Module([]); });

        Assert.Null(report);
        Assert.False(read);
        Assert.False(File.Exists(ReportPath));
    }

    [Fact]
    public void WritesWhatEachProbeFoundWhenAsked()
    {
        var memory = new byte[0x1002];
        memory[0x1001] = 0x20;
        RequestScan();

        var report = SignatureScan.ScanIfRequested(modFolder, Build, () => Module(memory, [(0x1000, 2)]));

        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ReportPath));
        var root = json.RootElement;
        Assert.Equal(Build, root.GetProperty("gameBuildSha256").GetString());
        Assert.Equal(1500, root.GetProperty("millisecondsSinceGameStart").GetInt64());
        Assert.Equal(0x10_0000, root.GetProperty("moduleBytes").GetInt64());
        Assert.Equal(2, root.GetProperty("readableBytes").GetInt64());
        Assert.Equal(0, root.GetProperty("unreadableBytes").GetInt64());
        var probe = Assert.Single(root.GetProperty("probes").EnumerateArray());
        Assert.Equal("a", probe.GetProperty("name").GetString());
        Assert.Equal(1, probe.GetProperty("matches").GetInt32());
        Assert.Equal(0x1001, Assert.Single(probe.GetProperty("firstRvas").EnumerateArray()).GetInt64());
        Assert.Null(report!.Error);
    }

    [Fact]
    public void ReportsTheBytesThatFailedToRead()
    {
        // The whole region fits in one chunk, so one unreadable byte fails all four.
        var report = Scan(Module(new byte[4], unreadable: 2), Probes(("a", "20")));

        Assert.Equal(0, report.ReadableBytes);
        Assert.Equal(4, report.UnreadableBytes);
    }

    [Fact]
    public void ScansOnlyOncePerRequest()
    {
        RequestScan();

        SignatureScan.ScanIfRequested(modFolder, Build, () => Module([]));

        Assert.False(File.Exists(RequestPath));
        Assert.Null(SignatureScan.ScanIfRequested(modFolder, Build, () => Module([])));
    }

    [Fact]
    public void ReportsAFailedReadInsteadOfThrowing()
    {
        RequestScan();

        var report = SignatureScan.ScanIfRequested(modFolder, Build, () => throw new InvalidOperationException("no main module"));

        Assert.Equal("InvalidOperationException: no main module", report!.Error);
        Assert.Contains("InvalidOperationException", File.ReadAllText(ReportPath), StringComparison.Ordinal);
    }

    [Fact]
    public void LeavesOutMessagesThatCanHoldLocalPaths()
    {
        RequestScan();

        var report = SignatureScan.ScanIfRequested(
            modFolder, Build, () => throw new IOException(@"could not read D:\games\mods\x"));

        Assert.Equal("IOException", report!.Error);
    }

    [Fact]
    public void ReportsABadProbesFileWithoutReadingMemory()
    {
        RequestScan("""[{ "name": "a", "pattern": "zz", "source": "s" }]""");
        var read = false;

        var report = SignatureScan.ScanIfRequested(modFolder, Build, () => { read = true; return Module([]); });

        Assert.Contains("bad pattern", report!.Error, StringComparison.Ordinal);
        Assert.False(read);
    }

    [Fact]
    public void CountsMatchesInEveryRegionAndListsTheFirstFive()
    {
        var memory = new byte[0x9003];
        memory.AsSpan(0x1000, 4).Fill(0xCC);
        memory[0x9000] = memory[0x9002] = 0xCC;

        var result = Assert.Single(Scan(Module(memory, [(0x1000, 4), (0x9000, 3)]), Probes(("int3", "CC"))).Probes);

        Assert.Equal("int3", result.Name);
        Assert.Equal(6, result.Matches);
        Assert.Equal([0x1000L, 0x1001, 0x1002, 0x1003, 0x9000], result.FirstRvas);
    }

    [Fact]
    public void ReportsAProbeThatMatchesNowhere()
    {
        var results = Scan(Module([0x10, 0x20]), Probes(("a", "20"), ("b", "30"))).Probes;

        Assert.Equal(["a", "b"], results.Select(result => result.Name));
        Assert.Equal(0, results[1].Matches);
        Assert.Empty(results[1].FirstRvas);
    }
}
