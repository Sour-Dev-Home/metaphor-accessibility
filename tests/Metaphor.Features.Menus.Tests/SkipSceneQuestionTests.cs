using Metaphor.Core.Announcements;
using Metaphor.Core.Input;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The cutscene's "Skip?" menu's question and buttons (#809), said once the menu opens. Its layout's animation
/// 4768 turned out to be the close (by ear, 2026-11-23 evening), so nothing wires it until an open signal is found;
/// <c>FeatureModulesTests</c> checks that the close no longer reaches it.</summary>
[Trait("Category", "Unit")]
public sealed class SkipSceneQuestionTests
{
    private readonly RecordingAnnouncer announcer = new();
    private int? glyphDevice;

    private SkipSceneQuestion Question() => new(announcer, new LastInputDevice(() => glyphDevice));

    [Fact]
    public void TheOpenSaysTheQuestionWithPadButtons()
    {
        glyphDevice = 0;

        Question().Opened();

        var said = Assert.Single(announcer.Said);
        Assert.Equal("Skip scene? A to skip, B to keep watching.", said.Text);
        Assert.True(said.Interrupt);
    }

    [Fact]
    public void TheKeyboardsGlyphsNameItsKeys()
    {
        glyphDevice = 2;

        Question().Opened();

        Assert.Equal("Skip scene? Space to skip, Escape to keep watching.", Assert.Single(announcer.Said).Text);
    }
}
