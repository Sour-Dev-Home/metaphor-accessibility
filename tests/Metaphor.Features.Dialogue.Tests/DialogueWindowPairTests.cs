using Metaphor.Core;
using Metaphor.Core.Announcements;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The dialogue window hooks go in as a pair (#172): the controls track the window, and hide their input from
/// the game, only once both are in. Wired as the hook loader wires them: the open hook tracks the window when told its
/// pair went in.</summary>
[Trait("Category", "Unit")]
public sealed class DialogueWindowPairTests
{
    private const string Build = "548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504";
    private const int X = 0x8000;
    private const int R = 0x52;

    private static readonly HookSignature[] Signatures =
    [
        new("MsgOpenBody", "48 8B ?? 10", [Build], "e"),
        new("MsgCloseBody", "48 8B ?? 20", [Build], "e"),
    ];

    private readonly DialogueControls controls;

    public DialogueWindowPairTests()
    {
        var announcer = new RecordingAnnouncer();
        controls = new DialogueControls(DialogueBindings.Default, new DialogueHistory(), new DialogueReader(announcer), announcer);
    }

    private sealed record WindowHook(string Name, Action? OnPaired);

    private List<string> InstallWith(IReadOnlyList<long> closeMatches)
    {
        var log = new List<string>();
        HookInstallation.Run(
            Build,
            [
                (new HookSpec("Open", [Build], "MsgOpenBody") { Partner = "Close" }, new WindowHook("Open", controls.TrackWindow)),
                (new HookSpec("Close", [Build], "MsgCloseBody") { Partner = "Open" }, new WindowHook("Close", null)),
            ],
            Signatures,
            _ => new ScanResult(
                new Dictionary<string, IReadOnlyList<long>> { ["MsgOpenBody"] = [0x100], ["MsgCloseBody"] = closeMatches },
                Complete: true),
            () => (_, _) => { },
            log.Add,
            hook => hook.OnPaired?.Invoke());
        return log;
    }

    [Fact]
    public void OnlyOneHookResolvingLeavesInputUntouchedAndSaysWhy()
    {
        var log = InstallWith(closeMatches: []);
        controls.WindowOpened();

        Assert.Equal(X, controls.Pad(0, X));
        Assert.Equal([R], controls.Keys([R]));
        Assert.Equal(
            ["hook Open skipped: its partner Close can't go in (signature MsgCloseBody matched nowhere)",
             "hook Close skipped: signature MsgCloseBody matched nowhere"],
            log);
    }

    [Fact]
    public void BothHooksResolvingHidesTheBindingsWhileTheWindowIsOpen()
    {
        InstallWith(closeMatches: [0x200]);
        controls.WindowOpened();

        Assert.Equal(0, controls.Pad(0, X));
    }
}
