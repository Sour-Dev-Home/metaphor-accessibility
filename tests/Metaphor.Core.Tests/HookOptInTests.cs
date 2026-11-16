namespace Metaphor.Core.Tests;

/// <summary>Whether a build installs a hook: dev-only hooks only in Debug, opt-in hooks only when their file is in the
/// mod's folder.</summary>
[Trait("Category", "Unit")]
public sealed class HookOptInTests
{
    private static bool NoFiles(string name) => false;

    [Theory]
    [InlineData(false, true, HookInclusion.Included)]
    [InlineData(false, false, HookInclusion.Included)]
    [InlineData(true, true, HookInclusion.Included)]
    [InlineData(true, false, HookInclusion.NotInThisBuild)]
    public void ADevOnlyHookIsLeftOutOfReleaseBuilds(bool devOnly, bool devBuild, HookInclusion expected) =>
        Assert.Equal(expected, HookOptIn.Decide(devOnly, optInFile: null, devBuild, NoFiles));

    [Fact]
    public void AnOptInHookIsLeftOutWithoutItsFile() =>
        Assert.Equal(HookInclusion.NotOptedIn, HookOptIn.Decide(true, "opt-in.txt", true, NoFiles));

    [Fact]
    public void AnOptInHookIsIncludedWhenItsFileIsThere() =>
        Assert.Equal(HookInclusion.Included, HookOptIn.Decide(true, "opt-in.txt", true, name => name == "opt-in.txt"));

    [Fact]
    public void ADevOnlyOptInHookStaysOutOfReleaseEvenWithItsFile() =>
        Assert.Equal(HookInclusion.NotInThisBuild, HookOptIn.Decide(true, "opt-in.txt", false, _ => true));

    [Theory]
    [InlineData("../opt-in.txt")]
    [InlineData("sub/opt-in.txt")]
    [InlineData("sub\\opt-in.txt")]
    [InlineData("C:opt-in.txt")]
    [InlineData("..")]
    [InlineData(" ")]
    [InlineData("")]
    public void AnOptInFileOutsideTheModsFolderNeverOptsIn(string name)
    {
        var asked = false;

        var inclusion = HookOptIn.Decide(true, name, true, _ => asked = true);

        Assert.Equal(HookInclusion.NotOptedIn, inclusion);
        Assert.False(asked);
    }
}
