using Metaphor.Core.Logging;

namespace Metaphor.Core.Tests.Logging;

/// <summary>What the log scrubber removes (#824): names the player typed and raw memory dumps, beside the folders and
/// user name the report log has always redacted (ReportLogTests). Made-up names and bytes only.</summary>
[Trait("Category", "Unit")]
public sealed class LogScrubberTests
{
    [Fact]
    public void ATypedNameIsReplacedAsAWholeWordInAnyCase()
    {
        var scrubber = LogScrubber.None();
        scrubber.AddName("Rowan");

        Assert.Equal(
            "said <name> and <name>, not Rowanberry",
            scrubber.Scrub("said Rowan and ROWAN, not Rowanberry"));
    }

    [Fact]
    public void ANameIsOnlyReplacedOnceTheModHasReadIt()
    {
        var scrubber = LogScrubber.None();

        Assert.Equal("Kit", scrubber.Scrub("Kit"));
        scrubber.AddName("Kit");
        Assert.Equal("<name>", scrubber.Scrub("Kit"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameIsIgnored(string? name)
    {
        var scrubber = LogScrubber.None();
        scrubber.AddName(name);

        Assert.Equal("hooks: 5 of 5 installed", scrubber.Scrub("hooks: 5 of 5 installed"));
    }

    [Theory]
    [InlineData("48 8B 05 11 22 33 44 55 66 77 88 99 AA BB CC DD")]
    [InlineData("48-8b-05-11-22-33-44-55-66-77-88-99-aa-bb-cc-dd")]
    [InlineData("48:8b:05:11:22:33:44:55:66:77:88:99:aa:bb:cc:dd")]
    public void SixteenBytesInARowAreAMemoryDump(string bytes)
    {
        Assert.Equal("read <memory dump, 16 bytes> at 0x10", LogScrubber.None().Scrub($"read {bytes} at 0x10"));
    }

    [Fact]
    public void AHexRunLongerThanASha256IsAMemoryDump()
    {
        Assert.Equal("<memory dump, 40 bytes>", LogScrubber.None().Scrub(new string('a', 80)));
    }

    [Theory]
    [InlineData("48 8B 05 11 22 33 44 55", 8)]
    [InlineData("48-8b-05-11-22-33-44-55-66-77-88", 11)]
    [InlineData("48:8b:05:11:22:33:44:55:66:77:88:99:aa:bb:cc", 15)]
    public void AShortSeparatedDumpIsAMemoryDump(string bytes, int count)
    {
        Assert.Equal($"read <memory dump, {count} bytes> at 0x10", LogScrubber.None().Scrub($"read {bytes} at 0x10"));
    }

    [Theory]
    [InlineData(34, 17)]
    [InlineData(48, 24)]
    [InlineData(62, 31)]
    public void AnUnseparatedRunOfSeventeenBytesOrMoreIsAMemoryDump(int digits, int bytes)
    {
        Assert.Equal($"<memory dump, {bytes} bytes>", LogScrubber.None().Scrub(new string('a', digits)));
    }

    [Fact]
    public void ALabelHashAndTheCommitAndBuildHashesAreNotDumps()
    {
        // 32 digits: a message label's MD5; 40: a commit; 64: a game build's SHA-256.
        foreach (var digits in new[] { 32, 40, 64 })
        {
            var run = new string('c', digits);
            Assert.Equal($"label {run}", LogScrubber.None().Scrub($"label {run}"));
        }
    }

    [Fact]
    public void SevenSeparatedBytesStay()
    {
        const string line = "bytes 48 8B 05 11 22 33 44 then";

        Assert.Equal(line, LogScrubber.None().Scrub(line));
    }

    [Fact]
    public void BuildHashesAndShortByteRunsStay()
    {
        var sha256 = new string('0', 64);
        var commit = new string('f', 40);
        var line = $"build {commit}: Ready on game build {sha256}, bytes 48 8B 05 11 cursor mgr=0x1a0";

        Assert.Equal(line, LogScrubber.None().Scrub(line));
    }
}
