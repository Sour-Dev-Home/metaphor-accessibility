namespace Metaphor.Core.Tests;

/// <summary>Which hooks the loader installs (#39, ground rule 2): only on a game build each hook names, and only where
/// its signature matches exactly once.</summary>
[Trait("Category", "Unit")]
public sealed class HookPlannerTests
{
    private const string Build = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";
    private const string OtherBuild = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly HookSignature ShowLine = new("show-line", "48 8B ?? 10", [Build], "e", ".ecode");

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
        var other = new HookSignature("other", "90 90", [Build], "e", ".ecode");
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
        var getTop = new HookSignature("sq_gettop", "48 8B 41 48", [Build], "e", ".ecode");
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
        var getTop = new HookSignature("sq_gettop", "48 8B 41 48", [Build], "e", ".ecode");
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

    // A global's load, `mov rcx,[rip+disp32]`, sits 9 bytes into its signature: its disp32 3 bytes into the 7-byte
    // instruction, as in CampStatusIsOpenBody. The global is where the instruction ends plus the displacement.
    private static readonly HookSignature LoadsFlag = new("loads-flag", "4C 63 01 ?? 48 8B 0D", [Build], "e", ".ecode");

    private static GlobalSpec Flag(params string[] builds) =>
        new("menus-open flag", builds.Length == 0 ? [Build] : builds, "loads-flag", LoadOffset: 9, DisplacementOffset: 3,
            InstructionLength: 7, TurnsOff: "menu controls");

    private static Func<long, int?> Memory(long rva, int value) => at => at == rva ? value : null;

    [Theory]
    [InlineData(0x20, 0x1030)]
    [InlineData(-0x1000, 0x10)]
    public void LocatesAGlobalFromTheRipRelativeLoadAtItsSignaturesOneMatch(int displacement, long global)
    {
        var plan = HookPlanner.Plan(
            Build, [Hook()], [Flag()], [ShowLine, LoadsFlag], Found(("show-line", [0x10]), ("loads-flag", [0x1000])),
            Memory(0x1000 + 9 + 3, displacement));

        Assert.Equal(new GlobalDecision("menus-open flag", global, null), Assert.Single(plan.Globals));
        Assert.True(Assert.Single(plan.Hooks).Install);
    }

    [Theory]
    [InlineData(new long[0], "signature loads-flag matched nowhere")]
    [InlineData(new long[] { 0x1000, 0x2000 }, "signature loads-flag matched 2 times")]
    public void AGlobalNotMatchedExactlyOnceIsUnknownAndEveryHookStillGoesIn(long[] rvas, string whyNot)
    {
        var plan = HookPlanner.Plan(
            Build, [Hook()], [Flag()], [ShowLine, LoadsFlag], Found(("show-line", [0x10]), ("loads-flag", rvas)),
            _ => 0x20);

        var global = Assert.Single(plan.Globals);
        Assert.False(global.Found);
        Assert.Equal(new GlobalDecision("menus-open flag", null, whyNot), global);
        Assert.True(Assert.Single(plan.Hooks).Install);
    }

    [Fact]
    public void AGlobalForAnotherBuildIsUnknownAndNotScannedFor()
    {
        IReadOnlyList<HookSignature>? asked = null;

        var plan = HookPlanner.Plan(
            Build, [Hook()], [Flag(OtherBuild)], [ShowLine, LoadsFlag],
            wanted => { asked = wanted; return Found(("show-line", [0x10]), ("loads-flag", [0x1000]))(wanted); },
            _ => 0x20);

        Assert.Equal(new GlobalDecision("menus-open flag", null, "not checked on game build 548ddc95"), Assert.Single(plan.Globals));
        Assert.Equal([ShowLine], asked);
    }

    [Fact]
    public void AGlobalWhoseLoadCannotBeReadIsUnknown()
    {
        var plan = HookPlanner.Plan(
            Build, [], [Flag()], [LoadsFlag], Found(("loads-flag", [0x1000])), _ => null);

        Assert.Equal(new GlobalDecision("menus-open flag", null, "its load couldn't be read"), Assert.Single(plan.Globals));
    }

    [Fact]
    public void HooksAndGlobalsShareOneScan()
    {
        var scans = 0;

        HookPlanner.Plan(
            Build, [Hook()], [Flag()], [ShowLine, LoadsFlag],
            wanted => { scans++; return Found(("show-line", [0x10]), ("loads-flag", [0x1000]))(wanted); }, _ => 0);

        Assert.Equal(1, scans);
    }

    [Fact]
    public void NoHooksMeansNoScan()
    {
        var scanned = false;

        Assert.Empty(HookPlanner.Plan(Build, [], [ShowLine], _ => { scanned = true; return new(new Dictionary<string, IReadOnlyList<long>>(), true); }));
        Assert.False(scanned);
    }
}
