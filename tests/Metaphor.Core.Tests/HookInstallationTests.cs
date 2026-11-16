namespace Metaphor.Core.Tests;

/// <summary>How the loader installs what <see cref="HookPlanner"/> approves (#87): each failure isolated, every hook
/// kept alive before it goes in, and a count that matches what went in.</summary>
[Trait("Category", "Unit")]
public sealed class HookInstallationTests
{
    private const string Build = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";
    private const string OtherBuild = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly HookSignature[] Signatures =
    [
        new("show-line", "48 8B ?? 10", [Build], "e"),
        new("show-choice", "48 8B ?? 20", [Build], "e"),
    ];

    private sealed class FakeHook(string name)
    {
        public string Name { get; } = name;
    }

    private static (HookSpec, FakeHook?) Hook(string name, string signature, params string[] builds) =>
        (new HookSpec(name, builds.Length == 0 ? [Build] : builds, signature), new FakeHook(name));

    private static ScanResult Found(IReadOnlyList<HookSignature> wanted) =>
        new(new Dictionary<string, IReadOnlyList<long>>
        {
            ["show-line"] = [0x9e3b70],
            ["show-choice"] = [0x123abc],
        }, Complete: true);

    [Fact]
    public void InstallsEachApprovedHookAtItsOffsetAndCountsIt()
    {
        var installed = new List<(string, long?)>();
        var log = new List<string>();

        var (loaded, _) = HookInstallation.Run(
            Build, [Hook("Dialogue", "show-line"), Hook("Choices", "show-choice")], Signatures, Found,
            () => (hook, decision) => installed.Add((hook.Name, decision.Rva)), log.Add);

        Assert.Equal(2, loaded);
        Assert.Equal([("Dialogue", 0x9e3b70), ("Choices", 0x123abc)], installed);
        Assert.Equal(
            ["hook Dialogue installed at module offset 0x9e3b70", "hook Choices installed at module offset 0x123abc"],
            log);
    }

    [Fact]
    public void InstallsNothingAndNeverAsksForTheInstallerWhenNothingIsPlanned()
    {
        var asked = false;
        var log = new List<string>();

        var (loaded, kept) = HookInstallation.Run(
            Build, [Hook("Dialogue", "show-line", OtherBuild), Hook("Choices", "nowhere")], Signatures, Found,
            () =>
            {
                asked = true;
                return (_, _) => throw new InvalidOperationException("never installed");
            },
            log.Add);

        Assert.Equal(0, loaded);
        Assert.Empty(kept);
        Assert.False(asked);
        Assert.Equal(
            ["hook Dialogue skipped: not checked on game build 548ddc95",
             "hook Choices skipped: no signature nowhere in hook-signatures.json"],
            log);
    }

    [Fact]
    public void SkipsAHookForAnotherBuildThatWasNeverCreated()
    {
        var log = new List<string>();

        var (loaded, kept) = HookInstallation.Run<FakeHook>(
            OtherBuild, [(new HookSpec("Dialogue", [Build], ""), null)], Signatures, Found,
            () => (_, _) => throw new InvalidOperationException("never installed"), log.Add);

        Assert.Equal(0, loaded);
        Assert.Empty(kept);
        Assert.Equal(["hook Dialogue skipped: not checked on game build 01234567"], log);
    }

    [Fact]
    public void ALogThatThrowsOnEveryLineLeavesTheCountIntact()
    {
        var installed = 0;

        var (loaded, kept) = HookInstallation.Run(
            Build, [Hook("Dialogue", "show-line"), Hook("Choices", "show-choice"), Hook("Old", "show-line", OtherBuild)],
            Signatures, Found, () => (_, _) => installed++, _ => throw new IOException("log is gone"));

        Assert.Equal(2, loaded);
        Assert.Equal(2, installed);
        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void AnInstallerThatCannotBeHadStopsTheRunBeforeAnythingGoesIn()
    {
        Assert.Throws<InvalidOperationException>(() => HookInstallation.Run(
            Build, [Hook("Dialogue", "show-line")], Signatures, Found,
            () => throw new InvalidOperationException("no hooks controller"), _ => { }));
    }

    [Fact]
    public void AHookThatFailsToInstallIsKeptAliveAndDoesNotStopTheOthers()
    {
        var installed = new List<string>();
        var log = new List<string>();
        (HookSpec, FakeHook?)[] hooks = [Hook("Dialogue", "show-line"), Hook("Choices", "show-choice")];

        var (loaded, kept) = HookInstallation.Run(
            Build, hooks, Signatures, Found,
            () => (hook, _) =>
            {
                if (hook.Name == "Dialogue")
                {
                    throw new InvalidOperationException("went live, then failed");
                }
                installed.Add(hook.Name);
            },
            log.Add);

        Assert.Equal(1, loaded);
        Assert.Equal(["Choices"], installed);
        Assert.Equal(["Dialogue", "Choices"], kept.Select(hook => hook.Name));
        Assert.Equal(
            ["hook Dialogue failed to install: InvalidOperationException",
             "hook Choices installed at module offset 0x123abc"],
            log);
    }
}
