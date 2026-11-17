using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Replay;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The dialogue controls (#58) against Steam Input's pad reads (#149): replays
/// <c>tests/fixtures/sessions/steam-input-pad1-noise.txt</c> through the controls with the dialogue window open, as the
/// pad read hook feeds them, and checks the player hears nothing from a pad that holds no binding. The noisy virtual pad
/// 1 returns 0 (it reports no read), so only the reads the game reports as made reach the controls; the hook doesn't
/// filter yet (#225).</summary>
[Trait("Category", "Unit")]
public sealed class SteamInputReplayTests
{
    private readonly RecordingAnnouncer announcer = new();

    private DialogueControls OpenControls()
    {
        var history = new DialogueHistory();
        history.Add("Made up line.");
        var controls = new DialogueControls(DialogueBindings.Default, history, new DialogueReader(announcer), announcer);
        controls.TrackWindow();
        controls.WindowOpened();
        return controls;
    }

    private static SessionFixture Load(string scenario) =>
        SessionFixture.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sessions", scenario)));

    [Fact]
    public void ReadsTheGameReportsAsMadeFromASteamInputSessionFireNoBinding()
    {
        var controls = OpenControls();

        Load("steam-input-pad1-noise.txt").Replay(new MessageEvents(), new CursorEvents(),
            pad: read =>
            {
                if (read.Returned != 0)
                {
                    controls.Pad(read.Pad, read.Buttons);
                }
            });

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void TheFixtureHasPadOneReadsThatReportNoReadAndHoldTheRepeatBinding()
    {
        var reads = Load("steam-input-pad1-noise.txt").Records.OfType<PadRecord>().Select(record => record.Read).ToList();

        Assert.Contains(reads, read => read.Pad == 1 && read.Returned == 0 && (read.Buttons & DialogueBindings.Default.RepeatButtons) != 0);
        Assert.DoesNotContain(reads, read => read.Pad == 1 && read.Returned != 0);
    }
}
