using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;

namespace Metaphor.Features.Updates.Tests;

/// <summary>Saying once on the title screen that a newer release exists (#576), against a fake release source.</summary>
[Trait("Category", "Unit")]
public sealed class UpdateNoticeTests
{
    private static readonly Screen Title = new("Title menu", "TitleChoice@ope@ui@app");
    private static readonly Screen Other = new("Main menu", "MainMenu@ope@ui@app");

    private readonly RecordingAnnouncer announcer = new();
    private readonly List<string> logged = [];
    private bool enabled = true;

    private UpdateNotice Notice(string running = "0.0.1-unstable.2") =>
        new(running, Title, announcer, () => enabled, logged.Add);

    private static ScreenEvent On(Screen screen, ScreenChange change = ScreenChange.Entered) =>
        new(change, screen, screen.ScreenClass, 0, 3, 1);

    private static Announcement Said(string version) =>
        new($"A newer version of the mod is available: {version}.", Interrupt: false);

    [Fact]
    public async Task ANewerReleaseIsSaidOnTheTitleScreen()
    {
        var notice = Notice();
        await notice.CheckAsync(new FakeSource("v0.0.1-unstable.3", "v0.0.1-unstable.2"), CancellationToken.None);
        Assert.Empty(announcer.Said);

        notice.Observe(On(Title));

        Assert.Equal([Said("0.0.1-unstable.3")], announcer.Said);
    }

    // The title spoke first, so the notice is queued after it rather than cutting it off.
    [Fact]
    public async Task AnAnswerAfterTheTitleScreenOpenedIsSaidAtOnce()
    {
        var notice = Notice();
        notice.Observe(On(Title));

        await notice.CheckAsync(new FakeSource("v0.0.1-unstable.3"), CancellationToken.None);

        Assert.Equal([Said("0.0.1-unstable.3")], announcer.Said);
    }

    [Fact]
    public async Task ItIsSaidOncePerLaunch()
    {
        var notice = Notice();
        await notice.CheckAsync(new FakeSource("v0.0.1-unstable.3"), CancellationToken.None);

        notice.Observe(On(Title));
        notice.Observe(On(Title, ScreenChange.FocusMoved));
        notice.Observe(On(Title, ScreenChange.Left));
        notice.Observe(On(Title));

        Assert.Single(announcer.Said);
    }

    [Theory]
    [InlineData("0.0.1-unstable.2", "v0.0.1-unstable.2")]
    [InlineData("0.0.1-unstable.2", "v0.0.1-unstable.1")]
    [InlineData("0.1.0", "v0.0.1-unstable.9")]
    public async Task TheSameOrAnOlderReleaseSaysNothing(string running, string latest)
    {
        var notice = Notice(running);
        await notice.CheckAsync(new FakeSource(latest), CancellationToken.None);

        notice.Observe(On(Title));

        Assert.Empty(announcer.Said);
    }

    // Pre-releases count, in semantic-version order: unstable.10 is after unstable.9, wherever the list puts it.
    [Fact]
    public async Task TheNewestOfAllReleasesIsSaid()
    {
        var notice = Notice();
        await notice.CheckAsync(
            new FakeSource("v0.0.1-unstable.9", "not-a-version", "v0.0.1-unstable.10", "v0.0.1-unstable.3"),
            CancellationToken.None);

        notice.Observe(On(Title));

        Assert.Equal([Said("0.0.1-unstable.10")], announcer.Said);
    }

    [Fact]
    public async Task OnlyTheTitleScreenSaysIt()
    {
        var notice = Notice();
        await notice.CheckAsync(new FakeSource("v0.0.1-unstable.3"), CancellationToken.None);

        notice.Observe(On(Other));
        notice.Observe(new ScreenEvent(ScreenChange.Entered, null, "Unknown@ope@ui@app", 0));
        Assert.Empty(announcer.Said);

        notice.Observe(On(Title));
        Assert.Single(announcer.Said);
    }

    // Left the title before the answer came: it waits for the next visit.
    [Fact]
    public async Task AnAnswerAfterTheTitleScreenClosedWaitsForTheNextVisit()
    {
        var notice = Notice();
        notice.Observe(On(Title));
        notice.Observe(On(Title, ScreenChange.Left));

        await notice.CheckAsync(new FakeSource("v0.0.1-unstable.3"), CancellationToken.None);
        Assert.Empty(announcer.Said);

        notice.Observe(On(Title));
        Assert.Single(announcer.Said);
    }

    [Fact]
    public async Task TurnedOffItMakesNoRequest()
    {
        enabled = false;
        var source = new FakeSource("v0.0.1-unstable.3");
        var notice = Notice();

        await notice.CheckAsync(source, CancellationToken.None);
        notice.Observe(On(Title));

        Assert.Equal(0, source.Calls);
        Assert.Empty(announcer.Said);
    }

    // Turned off in the settings file after the check, before the title: nothing is said.
    [Fact]
    public async Task TurnedOffAfterTheCheckItSaysNothing()
    {
        var notice = Notice();
        await notice.CheckAsync(new FakeSource("v0.0.1-unstable.3"), CancellationToken.None);
        enabled = false;

        notice.Observe(On(Title));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public async Task OneCheckPerLaunch()
    {
        var source = new FakeSource("v0.0.1-unstable.3");
        var notice = Notice();

        await notice.CheckAsync(source, CancellationToken.None);
        await notice.CheckAsync(source, CancellationToken.None);

        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task AFailedRequestIsSilentAndLogged()
    {
        var notice = Notice();
        notice.Observe(On(Title));

        await notice.CheckAsync(new FakeSource(new HttpRequestException("no route to host")), CancellationToken.None);

        Assert.Empty(announcer.Said);
        Assert.Contains("no route to host", Assert.Single(logged), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATimeoutIsSilent()
    {
        var notice = Notice();
        notice.Observe(On(Title));

        await notice.CheckAsync(new FakeSource(new TaskCanceledException()), CancellationToken.None);

        Assert.Empty(announcer.Said);
        Assert.Single(logged);
    }

    [Fact]
    public async Task ARunningVersionThatIsNotAVersionMakesNoRequest()
    {
        var source = new FakeSource("v0.0.1-unstable.3");
        var notice = Notice("dev");

        await notice.CheckAsync(source, CancellationToken.None);

        Assert.Equal(0, source.Calls);
        Assert.Single(logged);
    }

    [Fact]
    public void MissingArgumentsAreRefused()
    {
        Assert.Throws<ArgumentNullException>("titleScreen", () => new UpdateNotice("1.0.0", null!, announcer, () => true, _ => { }));
        Assert.Throws<ArgumentNullException>("announcer", () => new UpdateNotice("1.0.0", Title, null!, () => true, _ => { }));
    }

    /// <summary>Release tags from memory, or a failure, counting how often it was asked.</summary>
    private sealed class FakeSource : IReleaseSource
    {
        private readonly string[] tags;
        private readonly Exception? failure;

        public FakeSource(params string[] tags) => this.tags = tags;

        public FakeSource(Exception failure)
        {
            tags = [];
            this.failure = failure;
        }

        public int Calls { get; private set; }

        public Task<IReadOnlyList<string>> ReleaseTagsAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return failure is null ? Task.FromResult<IReadOnlyList<string>>(tags) : Task.FromException<IReadOnlyList<string>>(failure);
        }
    }
}
