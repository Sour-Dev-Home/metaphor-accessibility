using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The cutscene's "Skip?" menu's question and buttons (#809), said once the menu opens. Its layout's animation
/// 4768 turned out to be the close (by ear, 2026-11-23 evening), so it hears the component's show request instead;
/// <c>FeatureModulesTests</c> checks the wiring.</summary>
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
    public void TheMenusShowRequestSaysItAndItsCloseSaysNothing()
    {
        glyphDevice = 0;
        var requests = new MoviePauseRequests();
        requests.Observe(Question());

        requests.Requested(0);
        Assert.Empty(announcer.Said);
        requests.Requested(MoviePauseRequests.Show);

        Assert.Equal("Skip scene? A to skip, B to keep watching.", Assert.Single(announcer.Said).Text);
    }

    [Fact]
    public void TheKeyboardsGlyphsNameItsKeys()
    {
        glyphDevice = 2;

        Question().Opened();

        Assert.Equal("Skip scene? Space to skip, Escape to keep watching.", Assert.Single(announcer.Said).Text);
    }
}
