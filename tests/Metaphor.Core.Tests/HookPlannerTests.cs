namespace Metaphor.Core.Tests;

/// <summary>Which hooks the loader installs (#39, ground rule 2): only on a game build each hook names, and only where
/// its signature matches exactly once.</summary>
[Trait("Category", "Unit")]
public sealed class HookPlannerTests
{
    private const string Build = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";
    private const string OtherBuild = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly HookSignature ShowLine = new("show-line", "48 8B ?? 10", [Build], "e");

    private static HookSpec Hook(string name = "dialogue", string signature = "show-line", params string[] builds) =>
        new(name, builds.Length == 0 ? [Build] : builds, signature);

    private static Func<IReadOnlyList<HookSignature>, ScanResult> Found(
        params (string Name, long[] Rvas)[] matches) =>
        _ => new(matches.ToDictionary(match => match.Name, match => (IReadOnlyList<long>)match.Rvas), Complete: true);

    [Fact]
    public void InstallsAHookAtTheOneMatchOfItsSignature()
    {
        var decision = Assert.Single(HookPlanner.Plan(Build, [Hook()], [ShowLine], Found(("show-line", [0x9e3b70]))));

        Assert.Equal(new HookDecision("dialogue", 0x9e3b70, null), decision);
        Assert.True(decision.Install);
    }

    [Fact]
    public void SkipsAHookOnABuildItDoesNotNameWithoutScanning()
    {
        var scanned = false;

        var decision = Assert.Single(HookPlanner.Plan(
            OtherBuild, [Hook()], [ShowLine], _ => { scanned = true; return new(new Dictionary<string, IReadOnlyList<long>>(), true); }));

        Assert.False(decision.Install);
        Assert.Equal("not checked on game build 01234567", decision.WhyNot);
        Assert.False(scanned);
    }

    [Fact]
    public void SkipsAHookWhoseSignatureIsNotInTheSignaturesFile()
    {
        var decision = Assert.Single(HookPlanner.Plan(Build, [Hook(signature: "nowhere")], [ShowLine], Found()));

        Assert.Equal("no signature nowhere in hook-signatures.json", decision.WhyNot);
    }

    [Theory]
    [InlineData(new long[0], "signature show-line matched nowhere")]
    [InlineData(new long[] { 0x10, 0x20 }, "signature show-line matched 2 times")]
    public void SkipsAHookWhoseSignatureDoesNotMatchExactlyOnce(long[] rvas, string whyNot)
    {
        var decision = Assert.Single(HookPlanner.Plan(Build, [Hook()], [ShowLine], Found(("show-line", rvas))));

        Assert.Equal(whyNot, decision.WhyNot);
    }

    [Fact]
    public void ScansOnlyForTheSignaturesOfHooksThatPassTheBuildCheck()
    {
        var other = new HookSignature("other", "90 90", [Build], "e");
        IReadOnlyList<HookSignature>? asked = null;

        HookPlanner.Plan(
            Build,
            [Hook("dialogue"), Hook("old", "other", OtherBuild)],
            [ShowLine, other],
            wanted => { asked = wanted; return new(new Dictionary<string, IReadOnlyList<long>> { ["show-line"] = [1] }, true); });

        Assert.Equal([ShowLine], asked);
    }

    [Fact]
    public void GivesAHookTheAddressesOfTheGameFunctionsItCalls()
    {
        var getTop = new HookSignature("sq_gettop", "48 8B 41 48", [Build], "e");
        var hook = new HookSpec("registrations", [Build], "show-line", ["sq_gettop"]);
        IReadOnlyList<HookSignature>? asked = null;

        var decision = Assert.Single(HookPlanner.Plan(Build, [hook], [ShowLine, getTop], wanted =>
        {
            asked = wanted;
            return new(new Dictionary<string, IReadOnlyList<long>> { ["show-line"] = [0x10], ["sq_gettop"] = [0x20] }, true);
        }));

        Assert.True(decision.Install);
        Assert.Equal(0x10, decision.Rva);
        Assert.Equal(0x20, decision.Calls["sq_gettop"]);
        Assert.Equal([ShowLine, getTop], asked);
    }

    [Theory]
    [InlineData(new long[0], "signature sq_gettop matched nowhere")]
    [InlineData(new long[] { 1, 2, 3 }, "signature sq_gettop matched 3 times")]
    public void RefusesAHookWhenAFunctionItCallsIsNotFoundExactlyOnce(long[] rvas, string whyNot)
    {
        var getTop = new HookSignature("sq_gettop", "48 8B 41 48", [Build], "e");
        var hook = new HookSpec("registrations", [Build], "show-line", ["sq_gettop"]);

        var decision = Assert.Single(HookPlanner.Plan(Build, [hook], [ShowLine, getTop], _ =>
            new(new Dictionary<string, IReadOnlyList<long>> { ["show-line"] = [0x10], ["sq_gettop"] = rvas }, true)));

        Assert.False(decision.Install);
        Assert.Equal(whyNot, decision.WhyNot);
    }

    [Fact]
    public void RefusesAHookThatCallsASignatureMissingFromTheFile()
    {
        var hook = new HookSpec("registrations", [Build], "show-line", ["sq_gettop"]);

        var decision = Assert.Single(HookPlanner.Plan(Build, [hook], [ShowLine], Found(("show-line", [0x10]))));

        Assert.Equal("no signature sq_gettop in hook-signatures.json", decision.WhyNot);
    }

    [Fact]
    public void RefusesEveryHookWhenTheScanCouldNotReadEverything()
    {
        // A copy of the signature may sit in the part that couldn't be read, so one match isn't proof of one.
        var decision = Assert.Single(HookPlanner.Plan(
            Build, [Hook()], [ShowLine],
            _ => new(new Dictionary<string, IReadOnlyList<long>> { ["show-line"] = [0x10] }, Complete: false)));

        Assert.False(decision.Install);
        Assert.Equal("the scan couldn't read all of the game's code", decision.WhyNot);
    }

    [Fact]
    public void NoHooksMeansNoScan()
    {
        var scanned = false;

        Assert.Empty(HookPlanner.Plan(Build, [], [ShowLine], _ => { scanned = true; return new(new Dictionary<string, IReadOnlyList<long>>(), true); }));
        Assert.False(scanned);
    }
}
