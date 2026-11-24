using Metaphor.Core.Announcements;

namespace Metaphor.Core.Tests.Announcements;

/// <summary>Stopping one source's speech, and only while it is the last thing said (#676).</summary>
[Trait("Category", "Unit")]
public sealed class StoppableAnnouncerTests
{
    private readonly RecordingAnnouncer said = new();
    private int stops;

    private StoppableAnnouncer Start() => new(said, () => stops++);

    private static Announcement Line(string text) => new(text, Interrupt: true);

    [Fact]
    public void EveryAnnouncementIsPassedOnInOrder()
    {
        var speech = Start();
        var dialogue = speech.Source().Through(speech);

        speech.Announce(Line("menu"));
        dialogue.Announce(Line("line"));

        Assert.Equal(["menu", "line"], said.Said.Select(a => a.Text));
    }

    [Fact]
    public void AShownLineStillTheLastSaidIsStopped()
    {
        var speech = Start();
        var source = speech.Source();
        source.Through(speech).Announce(Line("line"));
        source.Shown();

        source.StopSpeaking();

        Assert.Equal(1, stops);
    }

    [Fact]
    public void SpeechFromElsewhereAfterTheLineIsNotStopped()
    {
        var speech = Start();
        var source = speech.Source();
        source.Through(speech).Announce(Line("line"));
        source.Shown();
        speech.Announce(new Announcement("Main menu", Interrupt: false));

        source.StopSpeaking();

        Assert.Equal(0, stops);
    }

    [Fact]
    public void ALineSaidSinceTheLastShownIsNotStopped()
    {
        var speech = Start();
        var source = speech.Source();
        var through = source.Through(speech);
        through.Announce(Line("first"));
        source.Shown();
        through.Announce(Line("second")); // Said after the window last showed a line, so it may belong to the next.

        source.StopSpeaking();

        Assert.Equal(0, stops);
    }

    [Fact]
    public void ALineTheSourceDroppedIsNotStopped()
    {
        var speech = Start();
        var source = speech.Source();
        speech.Announce(Line("menu"));
        source.Through(new SilentAnnouncer()).Announce(Line("line")); // A reader switched off.
        source.Shown();

        source.StopSpeaking();

        Assert.Equal(0, stops);
    }

    [Fact]
    public void EachLineIsStoppedOnceAndTheNextIsSaid()
    {
        var speech = Start();
        var source = speech.Source();
        var through = source.Through(speech);
        through.Announce(Line("line"));
        source.Shown();

        source.StopSpeaking();
        source.StopSpeaking();
        speech.Announce(Line("next screen"));
        source.StopSpeaking();

        Assert.Equal(1, stops);
        Assert.Equal("next screen", said.Said[^1].Text);
    }

    [Fact]
    public void AnEqualLineFromElsewhereIsNotTheSourcesLine()
    {
        var speech = Start();
        var source = speech.Source();
        source.Through(speech).Announce(Line("Yes"));
        source.Shown();
        speech.Announce(Line("Yes"));

        source.StopSpeaking();

        Assert.Equal(0, stops);
    }

    [Fact]
    public void NothingSaidYetStopsNothing()
    {
        var speech = Start();
        var source = speech.Source();
        source.Shown();

        source.StopSpeaking();

        Assert.Equal(0, stops);
    }

    // #879: the stop key cuts whatever is being said, from any source.
    [Fact]
    public void StopAllStopsSpeechFromAnySource()
    {
        var speech = Start();
        speech.Announce(Line("Main menu"));

        speech.StopAll();

        Assert.Equal(1, stops);
    }

    [Fact]
    public void AfterStopAllASourcesLineIsNotStoppedAgain()
    {
        var speech = Start();
        var source = speech.Source();
        source.Through(speech).Announce(Line("line"));
        source.Shown();
        speech.StopAll();

        source.StopSpeaking();

        Assert.Equal(1, stops);
    }
}
