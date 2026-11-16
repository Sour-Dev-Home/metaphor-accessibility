namespace Metaphor.Core.Tests;

/// <summary>The in-game signature scan's logic, run on byte arrays that stand in for the game's readable memory.</summary>
[Trait("Category", "Unit")]
public sealed class SignatureScanTests : IDisposable
{
    private const string Build = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";

    private readonly string modFolder = Directory.CreateTempSubdirectory("metaphor-scan-").FullName;

    public void Dispose() => Directory.Delete(modFolder, recursive: true);

    private static SignatureProbe Probe(string name, string pattern) => new(name, pattern, "test");

    private string ReportPath => Path.Combine(modFolder, SignatureScan.ReportFileName);

    private string RequestPath => Path.Combine(modFolder, SignatureScan.RequestFileName);

    private void RequestScan(string probes = """[{ "name": "a", "pattern": "20", "source": "s" }]""") =>
        File.WriteAllText(RequestPath, probes);

    private static ModuleMemory Module(params MemoryRun[] runs) => new(0x10_0000, runs, UnreadableBytes: 0x200, 1500);

    [Fact]
    public void DoesNothingUnlessAskedWithAProbesFile()
    {
        var read = false;

        var report = SignatureScan.ScanIfRequested(modFolder, Build, () => { read = true; return Module(); });

        Assert.Null(report);
        Assert.False(read);
        Assert.False(File.Exists(ReportPath));
    }

    [Fact]
    public void WritesWhatEachProbeFoundWhenAsked()
    {
        RequestScan();

        var report = SignatureScan.ScanIfRequested(
            modFolder, Build, () => Module(new MemoryRun(0x1000, new byte[] { 0x10, 0x20 })));

        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ReportPath));
        var root = json.RootElement;
        Assert.Equal(Build, root.GetProperty("gameBuildSha256").GetString());
        Assert.Equal(1500, root.GetProperty("millisecondsSinceGameStart").GetInt64());
        Assert.Equal(0x10_0000, root.GetProperty("moduleBytes").GetInt64());
        Assert.Equal(2, root.GetProperty("readableBytes").GetInt64());
        Assert.Equal(0x200, root.GetProperty("unreadableBytes").GetInt64());
        var probe = Assert.Single(root.GetProperty("probes").EnumerateArray());
        Assert.Equal("a", probe.GetProperty("name").GetString());
        Assert.Equal(1, probe.GetProperty("matches").GetInt32());
        Assert.Equal(0x1001, Assert.Single(probe.GetProperty("firstRvas").EnumerateArray()).GetInt64());
        Assert.Null(report!.Error);
    }

    [Fact]
    public void ScansOnlyOncePerRequest()
    {
        RequestScan();

        SignatureScan.ScanIfRequested(modFolder, Build, () => Module());

        Assert.False(File.Exists(RequestPath));
        Assert.Null(SignatureScan.ScanIfRequested(modFolder, Build, () => Module()));
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

        var report = SignatureScan.ScanIfRequested(modFolder, Build, () => { read = true; return Module(); });

        Assert.Contains("bad pattern", report!.Error, StringComparison.Ordinal);
        Assert.False(read);
    }

    [Fact]
    public void ReportsEachMatchAsAnAddressRelativeToTheModule()
    {
        MemoryRun[] runs = [new(0x1000, new byte[] { 0x00, 0x48, 0x8B, 0x00 })];

        var result = Assert.Single(SignatureScan.Scan(runs, [Probe("draw-text", "48 8B")]));

        Assert.Equal("draw-text", result.Name);
        Assert.Equal(1, result.Matches);
        Assert.Equal([0x1001L], result.FirstRvas);
    }

    [Fact]
    public void CountsMatchesInEveryRunAndListsTheFirstFive()
    {
        MemoryRun[] runs =
        [
            new(0x1000, new byte[] { 0xCC, 0xCC, 0xCC, 0xCC }),
            new(0x9000, new byte[] { 0xCC, 0x00, 0xCC }),
        ];

        var result = Assert.Single(SignatureScan.Scan(runs, [Probe("int3", "CC")]));

        Assert.Equal(6, result.Matches);
        Assert.Equal([0x1000L, 0x1001, 0x1002, 0x1003, 0x9000], result.FirstRvas);
    }

    [Fact]
    public void ReportsAProbeThatMatchesNowhere()
    {
        MemoryRun[] runs = [new(0, new byte[] { 0x10, 0x20 })];

        var results = SignatureScan.Scan(runs, [Probe("a", "20"), Probe("b", "30")]);

        Assert.Equal(["a", "b"], results.Select(result => result.Name));
        Assert.Equal(0, results[1].Matches);
        Assert.Empty(results[1].FirstRvas);
    }
}
