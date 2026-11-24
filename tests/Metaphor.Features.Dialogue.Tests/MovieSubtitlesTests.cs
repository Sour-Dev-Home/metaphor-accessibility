using Metaphor.Core.Announcements;
using Metaphor.Core.Menus;
using Metaphor.Core.Messages;

namespace Metaphor.Features.Dialogue.Tests;

/// <summary>Animated cutscenes' subtitles (#888), with the records the 2026-11-22 and 23 Debug logs show for the opening
/// movie (docs/findings/anime-movie-subtitles-skip-dialogue-hooks.md): its event file compiles, then the subtitle layout
/// plays animation 1032 as each line fades out, 24 times for 25 lines. Message text is made up.</summary>
[Trait("Category", "Unit")]
public sealed class MovieSubtitlesTests
{
    private const string Opening = "event/e01_001_001.msg";
    private const string Carriage = "event/e01_002_001.msg";

    private readonly RecordingAnnouncer announcer = new();
    private readonly DialogueHistory history = new();
    private readonly MovieSubtitles subtitles;
    private bool on = true;

    public MovieSubtitlesTests() =>
        subtitles = new MovieSubtitles(
            new DialogueAnnouncer(announcer, new TextCleaner(TextCleanerRules.None), history), () => on);

    private static string File(string prefix, int count) => string.Concat(
        Enumerable.Range(1, count).Select(n => $"@MES_{prefix}_{n:000}\n{{\n<NS>Louis<NE>\n{prefix} line {n}.\n}}\n"));

    private static Announcement Queued(string text) => new(text, Interrupt: false);

    private void LineFades(int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            subtitles.Played(new LayoutAnimation(MovieSubtitles.Layout, MovieSubtitles.LineFadesOut));
        }
    }

    [Fact]
    public void TheFirstFadeSaysTheFirstTwoLinesThenEachFadeTheNextQueuedWithNoSpeaker()
    {
        subtitles.Observe(new MessageFileCompiled(Opening, 40, File("opening", 25)));

        LineFades();
        LineFades();

        Assert.Equal(
            [Queued("opening line 1."), Queued("opening line 2."), Queued("opening line 3.")], announcer.Said);
    }

    [Fact]
    public void TwentyFourFadesSayAllTwentyFiveLinesOnceEachInOrder()
    {
        subtitles.Observe(new MessageFileCompiled(Opening, 40, File("opening", 25)));

        LineFades(30);

        Assert.Equal(
            Enumerable.Range(1, 25).Select(n => Queued($"opening line {n}.")), announcer.Said);
    }

    [Fact]
    public void NothingIsSaidBeforeTheLayoutPlays()
    {
        subtitles.Observe(new MessageFileCompiled(Opening, 40, File("opening", 25)));

        Assert.Empty(announcer.Said);
    }

    [Fact]
    public void AnotherAnimationOrLayoutSaysNothing()
    {
        subtitles.Observe(new MessageFileCompiled(Opening, 40, File("opening", 25)));

        subtitles.Played(new LayoutAnimation(MovieSubtitles.Layout, 1031));
        subtitles.Played(new LayoutAnimation("AutoSavingLayout@ui@app", MovieSubtitles.LineFadesOut));

        Assert.Empty(announcer.Said);
    }

    // A skipped movie plays no more fades, and the next movie's file compiling starts that file at its first line.
    [Fact]
    public void ASkipStopsTheLinesAndTheNextMovieStartsAtItsFirstLine()
    {
        subtitles.Observe(new MessageFileCompiled(Opening, 40, File("opening", 25)));
        LineFades(2);
        subtitles.Observe(new MessageFileCompiled(Carriage, 41, File("carriage", 5)));

        LineFades();

        Assert.Equal(
            [Queued("opening line 1."), Queued("opening line 2."), Queued("opening line 3."),
                Queued("carriage line 1."), Queued("carriage line 2.")],
            announcer.Said);
    }

    // The opening's file compiles at launch, with other files after it before the movie plays.
    [Fact]
    public void AFileOutsideTheEventScenesDoesNotTakeTheMoviesPlace()
    {
        subtitles.Observe(new MessageFileCompiled(Opening, 40, File("opening", 25)));
        subtitles.Observe(new MessageFileCompiled("ui/Title_System.msg", 42, File("title", 3)));

        LineFades();

        Assert.Equal([Queued("opening line 1."), Queued("opening line 2.")], announcer.Said);
    }

    [Fact]
    public void AFadeWithNoEventFileSaysNothing()
    {
        LineFades();

        Assert.Empty(announcer.Said);
    }

    // Off, the lines still count, so turning it on mid-movie says the line on screen, not the movie's first.
    [Fact]
    public void OffSaysNothingButKeepsItsPlace()
    {
        subtitles.Observe(new MessageFileCompiled(Opening, 40, File("opening", 25)));
        on = false;
        LineFades(3);
        on = true;

        LineFades();

        Assert.Equal([Queued("opening line 5.")], announcer.Said);
    }

    [Fact]
    public void TheLinesGoInTheHistoryForTheRepeatKey()
    {
        subtitles.Observe(new MessageFileCompiled(Opening, 40, File("opening", 25)));

        LineFades();

        Assert.Equal("opening line 2.", history.Latest);
    }
}
