using Metaphor.Core.Announcements;
using Metaphor.Core.Input;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The cutscene's "Skip?" menu says its question and buttons as its layout opens (#809): heard silent on
/// 2026-11-23, its open logged only as <c>MoviePauseLayout@ui@app</c>'s animation 4768. Animations in, announcements
/// out, through the same channel the hook hands them to.</summary>
[Trait("Category", "Unit")]
public sealed class SkipSceneQuestionTests
{
    private readonly RecordingAnnouncer announcer = new();
    private readonly LayoutAnimations layouts = new();
    private int? glyphDevice;

    public SkipSceneQuestionTests() =>
        layouts.Observe(new SkipSceneQuestion(announcer, new LastInputDevice(() => glyphDevice)));

    private void Played(string layout, int animation)
    {
        if (layouts.Wants(layout))
        {
            layouts.Played(new LayoutAnimation(layout, animation));
        }
    }

    [Fact]
    public void TheOpenSaysTheQuestionWithPadButtons()
    {
        glyphDevice = 0;

        Played("MoviePauseLayout@ui@app", 4768);

        var said = Assert.Single(announcer.Said);
        Assert.Equal("Skip scene? A to skip, B to keep watching.", said.Text);
        Assert.True(said.Interrupt);
    }

    [Fact]
    public void TheKeyboardsGlyphsNameItsKeys()
    {
        glyphDevice = 2;

        Played("MoviePauseLayout@ui@app", 4768);

        Assert.Equal("Skip scene? Space to skip, Escape to keep watching.", Assert.Single(announcer.Said).Text);
    }

    [Fact]
    public void TheLayoutsOtherAnimationsSayNothing()
    {
        Played("MoviePauseLayout@ui@app", 4769);

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AnotherLayoutIsNotAskedFor()
    {
        Assert.False(layouts.Wants("AutoSavingLayout@ui@app"));
        Assert.True(layouts.Wants("MoviePauseLayout@ui@app"));
    }
}
