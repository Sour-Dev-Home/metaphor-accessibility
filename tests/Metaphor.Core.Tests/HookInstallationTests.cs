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

        var (loaded, _, _) = HookInstallation.Run(
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

        var (loaded, kept, _) = HookInstallation.Run(
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

        var (loaded, kept, _) = HookInstallation.Run<FakeHook>(
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

        var (loaded, kept, _) = HookInstallation.Run(
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

        var (loaded, kept, _) = HookInstallation.Run(
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

    // #223: the load message names an opt-in as active only if its hooks are among these.
    [Fact]
    public void NamesOnlyTheHooksThatWentIn()
    {
        var (_, _, wentIn) = HookInstallation.Run(
            Build, [Hook("Dialogue", "show-line"), Hook("Choices", "show-choice"), Hook("Elsewhere", "nowhere")],
            Signatures, Found,
            () => (hook, _) =>
            {
                if (hook.Name == "Dialogue")
                {
                    throw new InvalidOperationException("failed");
                }
            },
            _ => { });

        Assert.Equal(["Choices"], wentIn);
    }

    private static (HookSpec, FakeHook?) Partnered(string name, string signature, string partner) =>
        (new HookSpec(name, [Build], signature) { Partner = partner }, new FakeHook(name));

    [Fact]
    public void InstallsNeitherHookOfAPairWhenOnlyOneResolvesAndSaysWhy()
    {
        var installed = new List<string>();
        var log = new List<string>();

        var (loaded, kept, _) = HookInstallation.Run(
            Build, [Partnered("Open", "show-line", "Close"), Partnered("Close", "nowhere", "Open")], Signatures, Found,
            () => (hook, _) => installed.Add(hook.Name), log.Add);

        Assert.Equal(0, loaded);
        Assert.Empty(kept);
        Assert.Empty(installed);
        Assert.Equal(
            ["hook Open skipped: its partner Close can't go in (no signature nowhere in hook-signatures.json)",
             "hook Close skipped: no signature nowhere in hook-signatures.json"],
            log);
    }

    [Fact]
    public void InstallsNeitherHookOfAPairWhenThePartnerWasNeverCreated()
    {
        var log = new List<string>();

        var (loaded, _, _) = HookInstallation.Run(
            Build, [Partnered("Open", "show-line", "Close")], Signatures, Found,
            () => (_, _) => throw new InvalidOperationException("never installed"), log.Add);

        Assert.Equal(0, loaded);
        Assert.Equal(["hook Open skipped: its partner Close isn't being loaded"], log);
    }

    [Fact]
    public void TellsEachHookOfAPairOnlyOnceBothWentIn()
    {
        var paired = new List<string>();

        HookInstallation.Run(
            Build,
            [Partnered("Open", "show-line", "Close"), Partnered("Close", "show-choice", "Open"), Hook("Alone", "show-line")],
            Signatures, Found, () => (_, _) => { }, _ => { }, pairInstalled: hook => paired.Add(hook.Name));

        Assert.Equal(["Open", "Close"], paired);
    }

    [Fact]
    public void TellsNeitherHookOfAPairWhenOneFailsToInstall()
    {
        var paired = new List<string>();

        var (loaded, _, _) = HookInstallation.Run(
            Build, [Partnered("Open", "show-line", "Close"), Partnered("Close", "show-choice", "Open")], Signatures, Found,
            () => (hook, _) =>
            {
                if (hook.Name == "Close")
                {
                    throw new InvalidOperationException("failed to go in");
                }
            },
            _ => { }, pairInstalled: hook => paired.Add(hook.Name));

        Assert.Equal(1, loaded);
        Assert.Empty(paired);
    }

    /// <summary>A hook that failed to install is the line a native crash report needs, so it goes to the error log,
    /// which the report log writes to disk at once (#204).</summary>
    [Fact]
    public void AnInstallFailureGoesToTheErrorLog()
    {
        var log = new List<string>();
        var errors = new List<string>();

        HookInstallation.Run(
            Build, [Hook("Dialogue", "show-line"), Hook("Choices", "show-choice")], Signatures, Found,
            () => (hook, _) =>
            {
                if (hook.Name == "Dialogue")
                {
                    throw new InvalidOperationException("went live, then failed");
                }
            },
            log.Add, errors.Add);

        Assert.Equal(["hook Dialogue failed to install: InvalidOperationException"], errors);
        Assert.Equal(["hook Choices installed at module offset 0x123abc"], log);
    }
}
