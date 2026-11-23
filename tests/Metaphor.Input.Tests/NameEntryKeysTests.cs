using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;
using Metaphor.Features.Dialogue;

namespace Metaphor.Input.Tests;

/// <summary>Typing a name at new game (#807): the name entry's prompt holds the dialogue window open, and a T or an R in
/// the name fired the reader and repeat keys (the by-ear session of 2026-11-23,
/// docs/raw-sources/dialogue-by-ear-session-2026-11-23.md). Through the input frame with the real dialogue controls: while
/// the screen is up the letters reach the game and not the controls; after it, they work again.</summary>
[Trait("Category", "Unit")]
public sealed class NameEntryKeysTests
{
    private const int T = 0x54;
    private const int R = 0x52;

    private readonly RecordingAnnouncer announcer = new();
    private readonly DialogueReader reader;
    private readonly InputFrame frame;
    private bool typing = true;

    public NameEntryKeysTests()
    {
        reader = new DialogueReader(announcer);
        var controls = new DialogueControls(DialogueBindings.Default, new DialogueHistory(), reader, announcer);
        frame = new InputFrame(new OpenWindow(), new MenuScreens([]), [controls], null, null, null, () => typing);
    }

    private sealed class OpenWindow : IGameInputState
    {
        public long? DialogueWindow() => 0x5000;

        public bool MenusOpen() => false;
    }

    // One press: down, then up.
    private IReadOnlyList<int> Press(int key)
    {
        var handed = frame.Keys([key]).Keys;
        frame.Keys([]);
        return handed;
    }

    [Fact]
    public void WhileANameIsTypedTAndRDoNothingAndReachTheGame()
    {
        Assert.Equal([T], Press(T));
        Assert.Equal([R], Press(R));

        Assert.True(reader.On);
        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void OnceTheNameIsConfirmedTTurnsTheReaderOffAgain()
    {
        Press(T);
        typing = false;

        Assert.Empty(Press(T));

        Assert.False(reader.On);
        Assert.Equal(["Dialogue reader off"], announcer.Said.Select(said => said.Text));
    }
}
