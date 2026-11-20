using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Core.Replay;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>The dialogue controls driven by the window pointer (#232), replaying
/// <c>tests/fixtures/sessions/window-pointer-field-talk.txt</c>: session 5's pad reads and window reads around a
/// follower's field talk, where the <c>.text1</c> window hooks logged an open with no close and the controls then
/// swallowed RB, X and LB in the field (docs/raw-sources/field-inventory-2026-11-17.md). Driven by the pointer, every
/// press made with the window closed reaches the game, and the one X pressed in dialogue repeats the line.</summary>
[Trait("Category", "Unit")]
public sealed class WindowPointerReplayTests
{
    private const int X = 0x8000;

    private readonly RecordingAnnouncer announcer = new();
    private readonly DialogueReader reader;
    private readonly DialogueControls controls;
    private readonly List<(bool Open, int Buttons, int Handed)> reads = [];
    private bool open;

    public WindowPointerReplayTests()
    {
        var history = new DialogueHistory();
        reader = new DialogueReader(announcer);
        controls = new DialogueControls(DialogueBindings.Default, history, reader, announcer);
        var fixture = SessionFixture.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sessions", "window-pointer-field-talk.txt")));
        fixture.Replay(new MessageEvents(), new CursorEvents(),
            pad: read => reads.Add((open, read.Buttons, controls.Pad(read.Pad, read.Buttons))),
            window: current =>
            {
                var nowOpen = current is { } message && message != 0;
                // The fixture has no line records: each window shows a made-up line as it opens, since repeat speaks
                // only a line from the window it is pressed in (#526).
                if (nowOpen && !open)
                {
                    history.Add("Made up line.");
                }
                open = nowOpen;
                controls.WindowRead(current);
            });
    }

    [Fact]
    public void EveryPressWithTheWindowClosedReachesTheGame()
    {
        var closed = reads.Where(read => !read.Open).ToList();

        // 23 RB reads, 6 X, 4 LB and 4 LB + RB in the fixture, all made between windows.
        Assert.Equal(23, closed.Count(read => read.Buttons == 0x0800));
        Assert.All(closed, read => Assert.Equal(read.Buttons, read.Handed));
    }

    [Fact]
    public void TheOneXPressedInDialogueRepeatsTheLineAndIsHidden()
    {
        var x = Assert.Single(reads, read => read.Open && read.Buttons == X);

        Assert.Equal(0, x.Handed);
        Assert.Equal([new Announcement("Made up line.", Interrupt: true)], announcer.Said);
        Assert.True(reader.On);
    }
}
