using System.Text.RegularExpressions;

namespace Metaphor.Core.Tests;

[Trait("Category", "Unit")]
public sealed partial class SourceRevisionTests
{
    private const string Sha = "548ff0e4fc04c5e205ae86ae4a6e4cdd64031514";

    [Fact]
    public void AnAssemblyFromThisBuildKnowsItsFullCommit()
    {
        var revision = SourceRevision.Of(typeof(SourceRevisionTests).Assembly);

        Assert.True(revision.HasCommit);
        Assert.Matches(FullCommitSha(), revision.CommitSha);
    }

    [Fact]
    public void ReadsCommitFromCleanBuildVersion()
    {
        var revision = SourceRevision.FromInformationalVersion($"1.0.0+{Sha}");

        Assert.Equal(Sha, revision.CommitSha);
        Assert.False(revision.HasUncommittedChanges);
    }

    [Fact]
    public void ReadsUncommittedChangesMarker()
    {
        var revision = SourceRevision.FromInformationalVersion($"1.0.0+{Sha}-dirty");

        Assert.Equal(Sha, revision.CommitSha);
        Assert.True(revision.HasUncommittedChanges);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("")]
    [InlineData("1.0.0+not-a-sha")]
    [InlineData("1.0.0+not-a-sha-dirty")]
    // The commit is only what follows the '+'.
    [InlineData(Sha)]
    // Not 40 lowercase hex digits.
    [InlineData("1.0.0+548ff0e")]
    [InlineData("1.0.0+548ff0e4fc04c5e205ae86ae4a6e4cdd6403151g")]
    [InlineData("1.0.0+548FF0E4FC04C5E205AE86AE4A6E4CDD64031514")]
    public void VersionWithoutCommitHasNoCommit(string informationalVersion)
    {
        var revision = SourceRevision.FromInformationalVersion(informationalVersion);

        Assert.False(revision.HasCommit);
        Assert.Equal(string.Empty, revision.CommitSha);
        Assert.False(revision.HasUncommittedChanges);
    }

    [Fact]
    public void ACommitRightAfterALeadingPlusIsRead()
    {
        Assert.Equal(Sha, SourceRevision.FromInformationalVersion($"+{Sha}").CommitSha);
    }

    [Fact]
    public void AMissingAssemblyOrVersionIsRefused()
    {
        Assert.Throws<ArgumentNullException>("assembly", () => SourceRevision.Of(null!));
        Assert.Throws<ArgumentNullException>("informationalVersion", () => SourceRevision.FromInformationalVersion(null!));
    }

    [GeneratedRegex("^[0-9a-f]{40}$")]
    private static partial Regex FullCommitSha();
}
