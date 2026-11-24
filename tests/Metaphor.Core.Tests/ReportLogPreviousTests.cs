namespace Metaphor.Core.Tests;

/// <summary>The last session's report log is kept beside the new one (#872).</summary>
[Trait("Category", "Unit")]
public sealed class ReportLogPreviousTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("report-log-previous").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string Current => Path.Combine(folder, ReportLog.FileName);

    private string Previous => Path.Combine(folder, ReportLog.PreviousFileName);

    [Fact]
    public void TheLastLogIsKeptAsThePreviousOneUnchanged()
    {
        File.WriteAllText(Current, "last session\n");

        ReportLog.KeepPrevious(folder);

        Assert.False(File.Exists(Current));
        Assert.Equal("last session\n", File.ReadAllText(Previous));
    }

    [Fact]
    public void AnOlderPreviousLogIsReplaced()
    {
        File.WriteAllText(Previous, "two sessions ago\n");
        File.WriteAllText(Current, "last session\n");

        ReportLog.KeepPrevious(folder);

        Assert.Equal("last session\n", File.ReadAllText(Previous));
    }

    [Fact]
    public void AMissingLogIsFineAndKeepsTheOlderPrevious()
    {
        File.WriteAllText(Previous, "older\n");

        ReportLog.KeepPrevious(folder);

        Assert.Equal("older\n", File.ReadAllText(Previous));
        Assert.False(File.Exists(Current));
    }

    [Fact]
    public void AFolderThatIsMissingDoesNotThrow() =>
        ReportLog.KeepPrevious(Path.Combine(folder, "nowhere"));
}
