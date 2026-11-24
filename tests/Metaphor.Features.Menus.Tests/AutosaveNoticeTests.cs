using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Menus.Tests;

/// <summary>The field autosave notice (#543), from the layout animation four Debug session logs recorded for each
/// autosave (docs/findings/autosave-notice-is-a-layout-animation.md).</summary>
[Trait("Category", "Unit")]
public sealed class AutosaveNoticeTests
{
    private readonly RecordingAnnouncer announcer = new();

    [Fact]
    public void AnAutosaveIsSaidOnceAndQueued()
    {
        var notice = new AutosaveNotice(announcer);

        notice.Played(new LayoutAnimation("AutoSavingLayout@ui@app", 748));

        var said = Assert.Single(announcer.Said);
        Assert.Equal("Autosaving", said.Text);
        Assert.False(said.Interrupt);
    }

    [Fact]
    public void ItHearsOnlyTheAutosaveLayout() =>
        Assert.Equal(["AutoSavingLayout@ui@app"], new AutosaveNotice(announcer).Layouts);

    [Fact]
    public void AnotherAnimationOrLayoutSaysNothing()
    {
        var notice = new AutosaveNotice(announcer);

        notice.Played(new LayoutAnimation("AutoSavingLayout@ui@app", 749));
        notice.Played(new LayoutAnimation("SystemSavingLayout@ui@app", 758));
        notice.Played(new LayoutAnimation("SavingLayout@ui@app", 748));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void ThroughTheLayoutAnimationsItHearsTheAutosave()
    {
        var layouts = new LayoutAnimations();
        layouts.Observe(new AutosaveNotice(announcer));

        Assert.True(layouts.Wants("AutoSavingLayout@ui@app"));
        layouts.Played(new LayoutAnimation("AutoSavingLayout@ui@app", 748));

        Assert.Equal(["Autosaving"], announcer.Said.Select(said => said.Text));
    }
}
